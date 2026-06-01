using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Logging;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface ICodexDryRunOrchestratorService
{
    Task<CodexDryRunResult> RunAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

public sealed class CodexDryRunOrchestratorService : ICodexDryRunOrchestratorService
{
    private const long MaxHashedInputBytes = 5L * 1024L * 1024L;

    private static readonly IReadOnlyList<(string RelativePath, string Role)> RequiredInputFiles =
    [
        (WrbProjectSchema.FileName, "projectManifest"),
        (ConstructionPackageSchema.RelativePath, "constructionPackage"),
        (CodexTaskPackageSchema.RelativePath, "taskPackage"),
        (CodexTaskPackageSchema.InstructionsRelativePath, "instructions"),
        (ConstructionPackageContextSchema.ProjectBriefRelativePath, "projectBrief"),
        (ConstructionPackageContextSchema.ObservationSummaryRelativePath, "observationSummary"),
        (ConstructionPackageContextSchema.AssetIndexRelativePath, "assetIndex"),
        (ConstructionPackageContextSchema.ThemeSummaryRelativePath, "themeSummary"),
        (ConstructionPackageContextSchema.ContentMapSummaryRelativePath, "contentMapSummary"),
        (ConstructionPackageContextSchema.ConstraintsRelativePath, "constraints"),
        (ConstructionPackageContextSchema.AcceptanceChecklistRelativePath, "acceptanceChecklist"),
        (ConstructionPackageContextSchema.PackageIndexRelativePath, "contextPackageIndex"),
        (ContentMapSchema.RelativePath, "contentMap"),
        (ThemeManifestSchema.RelativePath, "theme"),
        (AssetsManifestSchema.RelativePath, "assets"),
        (ObservationPackageSchema.RelativePath, "observationPackage")
    ];

    private static readonly IReadOnlyList<string> RequiredInstructionBoundaries =
    [
        "Reading order",
        "Do not execute Codex CLI",
        "Do not call OpenAI API",
        "Only write codex-workspace/output-site/current",
        "Do not modify source code",
        "Do not write .git",
        "Do not copy reference-site copyrighted assets"
    ];

    private static readonly IReadOnlyList<string> ExpectedAllowedWriteRoots =
    [
        ProjectDirectoryV2.CodexWorkspace,
        ProjectDirectoryV2.OutputCurrent
    ];

    private static readonly IReadOnlyList<string> DryRunStepIds =
    [
        "load-project-manifest",
        "run-readiness-gate",
        "load-task-package",
        "validate-instructions",
        "collect-input-files",
        "validate-allowed-write-roots",
        "validate-forbidden-roots",
        "verify-rollback-availability",
        "prepare-codex-workspace",
        "simulate-codex-command",
        "simulate-output-validation",
        "write-dry-run-reports",
        "create-dry-run-record",
        "write-logs"
    ];

    private readonly IConstructionReadinessGateService readinessGateService;
    private readonly ICodexTaskPackageService taskPackageService;
    private readonly IConstructionPackageService constructionPackageService;
    private readonly IProjectLogService logService;

    public CodexDryRunOrchestratorService()
        : this(
            new ConstructionReadinessGateService(),
            new CodexTaskPackageService(),
            new ConstructionPackageService(),
            new ProjectLogService())
    {
    }

    public CodexDryRunOrchestratorService(
        IConstructionReadinessGateService readinessGateService,
        ICodexTaskPackageService taskPackageService,
        IConstructionPackageService constructionPackageService,
        IProjectLogService logService)
    {
        this.readinessGateService = readinessGateService;
        this.taskPackageService = taskPackageService;
        this.constructionPackageService = constructionPackageService;
        this.logService = logService;
    }

    public async Task<CodexDryRunResult> RunAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var dryRunId = CreateDryRunId(startedAt);
        var plan = CreatePlan(dryRunId, startedAt);
        var result = new CodexDryRunResult
        {
            SchemaVersion = CodexDryRunSchema.CurrentSchemaVersion,
            DryRunId = dryRunId,
            StartedAt = startedAt,
            ExecutedCodexCli = false,
            CalledOpenAiApi = false,
            GeneratedWebsite = false
        };

        string root;
        try
        {
            root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        }
        catch (InvalidOperationException ex)
        {
            AddBlockingSafetyCheck(
                plan,
                "project-root",
                SanitizeForArtifact(ex.Message),
                TaskFailureCategory.SandboxViolation);
            CompleteResultFromPlan(result, plan);
            return result;
        }

        Directory.CreateDirectory(GetDryRunDirectoryPath(root, dryRunId));
        await WriteStartLogsAsync(root, cancellationToken);

        WrbProjectManifest? projectManifest = null;
        ConstructionReadinessResult? readiness = null;
        CodexTaskPackage? taskPackage = null;
        ConstructionPackageManifest? constructionPackage = null;

        try
        {
            projectManifest = await LoadProjectManifestAsync(root, plan, cancellationToken);
            if (projectManifest is not null)
            {
                plan.ProjectId = SanitizeForArtifact(projectManifest.ProjectId);
                result.ProjectId = plan.ProjectId;
            }

            readiness = await RunReadinessGateAsync(root, plan, cancellationToken);
            if (string.IsNullOrWhiteSpace(plan.ProjectId) && !string.IsNullOrWhiteSpace(readiness.ProjectId))
            {
                plan.ProjectId = SanitizeForArtifact(readiness.ProjectId);
                result.ProjectId = plan.ProjectId;
            }

            taskPackage = await LoadTaskPackageAsync(root, plan, cancellationToken);
            if (taskPackage is not null)
            {
                plan.ProjectId = string.IsNullOrWhiteSpace(plan.ProjectId)
                    ? SanitizeForArtifact(taskPackage.ProjectId)
                    : plan.ProjectId;
                plan.TaskPackageId = SanitizeForArtifact(taskPackage.TaskPackageId);
                result.ProjectId = plan.ProjectId;
                result.TaskPackageId = plan.TaskPackageId;
            }

            await ValidateInstructionsAsync(root, plan, cancellationToken);
            constructionPackage = await LoadConstructionPackageAsync(root, plan, cancellationToken);
            CollectInputFiles(root, plan, taskPackage, constructionPackage);
            ValidateAllowedWriteRoots(root, plan, taskPackage);
            ValidateForbiddenRoots(root, plan, taskPackage);
            VerifyRollbackAvailability(plan, readiness);
            ValidateWorkspacePreparation(root, plan);
            SimulateCodexCommand(plan);
            SimulateOutputValidation(root, plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AddBlockingSafetyCheck(
                plan,
                "internal-error",
                $"Dry-run failed while preparing artifacts: {ex.Message}",
                TaskFailureCategory.InternalError);
        }

        CompleteResultFromPlan(result, plan);
        result.CompletedAt = DateTimeOffset.UtcNow;

        try
        {
            MarkStep(plan, "write-dry-run-reports", "ok", "Dry-run plan, result, and Markdown report were written.");
            MarkStep(plan, "create-dry-run-record", "ok", "Dry-run record was written separately from real task-run records.");
            MarkStep(plan, "write-logs", "ok", "Project, security, and codex-task logs were written.");
            FinalizeArtifacts(plan, result);
            await SavePlanAsync(root, dryRunId, plan, cancellationToken);
            await SaveResultAsync(root, dryRunId, result, cancellationToken);
            await SaveReportAsync(root, dryRunId, plan, result, cancellationToken);
            await SaveRecordAsync(root, dryRunId, plan, result, cancellationToken);
            await WriteCompletionLogsAsync(root, result, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            result.IsOk = false;
            result.IsReadyForFutureExecution = false;
            result.BlockingReasons.Add(SanitizeForArtifact($"dry-run artifact write failed: {ex.Message}"));
        }

        result.ExecutedCodexCli = false;
        result.CalledOpenAiApi = false;
        result.GeneratedWebsite = false;
        return result;
    }

    public static string GetDryRunDirectoryRelativePath(string dryRunId)
    {
        var safeDryRunId = ProjectPackagePathHelpers.NormalizeRelativeToken(dryRunId, "dry-run id");
        return $"{CodexDryRunSchema.DryRunsRootRelativePath}/{safeDryRunId}";
    }

    public static string GetDryRunDirectoryPath(string projectRoot, string dryRunId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetDryRunDirectoryRelativePath(dryRunId),
            "dry-run directory");
    }

    public static string GetPlanPath(string projectRoot, string dryRunId)
    {
        return ResolveDryRunFilePath(projectRoot, dryRunId, CodexDryRunSchema.PlanFileName, "dry-run plan");
    }

    public static string GetResultPath(string projectRoot, string dryRunId)
    {
        return ResolveDryRunFilePath(projectRoot, dryRunId, CodexDryRunSchema.ResultFileName, "dry-run result");
    }

    public static string GetReportPath(string projectRoot, string dryRunId)
    {
        return ResolveDryRunFilePath(projectRoot, dryRunId, CodexDryRunSchema.ReportFileName, "dry-run report");
    }

    public static string GetRecordPath(string projectRoot, string dryRunId)
    {
        return ResolveDryRunFilePath(projectRoot, dryRunId, CodexDryRunSchema.RecordFileName, "dry-run record");
    }

    private static string ResolveDryRunFilePath(
        string projectRoot,
        string dryRunId,
        string fileName,
        string fieldName)
    {
        var safeFileName = ProjectPackagePathHelpers.NormalizeRelativeToken(fileName, fieldName);
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            $"{GetDryRunDirectoryRelativePath(dryRunId)}/{safeFileName}",
            fieldName);
    }

    private async Task<WrbProjectManifest?> LoadProjectManifestAsync(
        string projectRoot,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            if (string.IsNullOrWhiteSpace(manifest.ProjectId))
            {
                AddBlockingSafetyCheck(
                    plan,
                    "project-id",
                    "project.wrbproj has no projectId.",
                    TaskFailureCategory.ValidationError);
            }

            MarkStep(plan, "load-project-manifest", "ok", "project.wrbproj loaded.", WrbProjectSchema.FileName);
            return manifest;
        }
        catch (FileNotFoundException)
        {
            AddBlockingSafetyCheck(
                plan,
                "project-manifest",
                "project.wrbproj is missing.",
                TaskFailureCategory.MissingInput);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddBlockingSafetyCheck(
                plan,
                "project-manifest",
                $"project.wrbproj could not be loaded: {ex.Message}",
                ClassifyFailure(ex.Message));
        }

        MarkStep(plan, "load-project-manifest", "error", "project.wrbproj could not be loaded.", WrbProjectSchema.FileName);
        return null;
    }

    private async Task<ConstructionReadinessResult> RunReadinessGateAsync(
        string projectRoot,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await readinessGateService.CheckAsync(
                projectRoot,
                ConstructionReadinessMode.PreCodexDryRun,
                cancellationToken);

            plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
            {
                Key = "readiness-gate",
                Status = readiness.IsReady ? "ok" : "error",
                Message = readiness.IsReady
                    ? "PreCodexDryRun readiness gate passed."
                    : "PreCodexDryRun readiness gate reported blockers.",
                FailureCategory = readiness.IsReady ? null : TaskFailureCategory.ValidationError.ToString(),
                BlocksFutureExecution = !readiness.IsReady
            });

            foreach (var warning in readiness.Warnings)
            {
                AddWarning(plan, $"readiness: {warning}");
            }

            foreach (var reason in readiness.BlockingReasons)
            {
                AddBlockingReason(plan, $"readiness: {reason}");
            }

            MarkStep(
                plan,
                "run-readiness-gate",
                readiness.IsReady ? "ok" : "error",
                readiness.IsReady
                    ? "ConstructionReadinessGateService PreCodexDryRun mode passed."
                    : "ConstructionReadinessGateService PreCodexDryRun mode blocked future execution.",
                ConstructionReadinessSchema.ReportJsonRelativePath);
            return readiness;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddBlockingSafetyCheck(
                plan,
                "readiness-gate",
                $"Readiness gate could not run: {ex.Message}",
                ClassifyFailure(ex.Message));
            MarkStep(plan, "run-readiness-gate", "error", "Readiness gate could not run.");
            return new ConstructionReadinessResult
            {
                Mode = ConstructionReadinessMode.PreCodexDryRun,
                IsReady = false
            };
        }
    }

    private async Task<CodexTaskPackage?> LoadTaskPackageAsync(
        string projectRoot,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            var package = await taskPackageService.LoadAsync(projectRoot, cancellationToken);
            if (string.IsNullOrWhiteSpace(package.TaskPackageId))
            {
                AddBlockingSafetyCheck(
                    plan,
                    "task-package-id",
                    "task-package.json has no taskPackageId.",
                    TaskFailureCategory.ValidationError);
            }

            MarkStep(plan, "load-task-package", "ok", "task-package.json loaded.", CodexTaskPackageSchema.RelativePath);
            return package;
        }
        catch (FileNotFoundException)
        {
            AddBlockingSafetyCheck(
                plan,
                "task-package",
                "codex-task/task-package.json is missing.",
                TaskFailureCategory.MissingInput);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddBlockingSafetyCheck(
                plan,
                "task-package",
                $"task-package.json could not be loaded: {ex.Message}",
                ClassifyFailure(ex.Message));
        }

        MarkStep(plan, "load-task-package", "error", "task-package.json could not be loaded.", CodexTaskPackageSchema.RelativePath);
        return null;
    }

    private async Task ValidateInstructionsAsync(
        string projectRoot,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                projectRoot,
                CodexTaskPackageSchema.InstructionsRelativePath,
                "dry-run instructions");
        }
        catch (InvalidOperationException ex)
        {
            AddBlockingSafetyCheck(
                plan,
                "instructions-path",
                ex.Message,
                TaskFailureCategory.SandboxViolation);
            MarkStep(plan, "validate-instructions", "error", "instructions.md path was rejected.");
            return;
        }

        if (!File.Exists(path))
        {
            AddBlockingSafetyCheck(
                plan,
                "instructions",
                "codex-task/instructions.md is missing.",
                TaskFailureCategory.MissingInput);
            MarkStep(plan, "validate-instructions", "error", "instructions.md is missing.", CodexTaskPackageSchema.InstructionsRelativePath);
            return;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        foreach (var boundary in RequiredInstructionBoundaries)
        {
            if (!content.Contains(boundary, StringComparison.OrdinalIgnoreCase))
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"instructions-boundary-{NormalizeKey(boundary)}",
                    $"instructions.md is missing required safety boundary: {boundary}",
                    TaskFailureCategory.ValidationError);
            }
        }

        if (ProjectPackagePathHelpers.ContainsSecretOrLocalPath(content))
        {
            AddBlockingSafetyCheck(
                plan,
                "instructions-sensitive-marker",
                "instructions.md contains a sensitive marker or local-path marker.",
                TaskFailureCategory.SecretDetected);
        }

        MarkStep(
            plan,
            "validate-instructions",
            plan.SafetyChecks.Any(check => check.Key.StartsWith("instructions-", StringComparison.OrdinalIgnoreCase)
                                           && check.BlocksFutureExecution)
                ? "error"
                : "ok",
            "instructions.md safety boundaries were checked.",
            CodexTaskPackageSchema.InstructionsRelativePath);
    }

    private async Task<ConstructionPackageManifest?> LoadConstructionPackageAsync(
        string projectRoot,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            return await constructionPackageService.LoadAsync(projectRoot, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            AddBlockingSafetyCheck(
                plan,
                "construction-package",
                "codex-task/construction-package.json is missing.",
                TaskFailureCategory.MissingInput);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddBlockingSafetyCheck(
                plan,
                "construction-package",
                $"construction-package.json could not be loaded: {ex.Message}",
                ClassifyFailure(ex.Message));
        }

        return null;
    }

    private static void CollectInputFiles(
        string projectRoot,
        CodexDryRunPlan plan,
        CodexTaskPackage? taskPackage,
        ConstructionPackageManifest? constructionPackage)
    {
        var inputs = new Dictionary<string, (string Role, bool Required)>(StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredInputFiles)
        {
            inputs[required.RelativePath] = (required.Role, true);
        }

        if (taskPackage is not null)
        {
            foreach (var input in taskPackage.InputFiles)
            {
                inputs[input.RelativePath] = (input.Role, input.Required);
            }
        }

        if (constructionPackage is not null)
        {
            foreach (var required in constructionPackage.RequiredProjectFiles)
            {
                inputs[required] = ("constructionRequiredProjectFile", true);
            }

            foreach (var input in constructionPackage.Inputs)
            {
                inputs[input.RelativePath] = (input.Kind, input.Required);
            }
        }

        foreach (var input in inputs.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, input.Key, "dry-run input file");
                var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, "dry-run input file");
                var exists = File.Exists(fullPath);
                string? sha256 = null;
                if (exists)
                {
                    var info = new FileInfo(fullPath);
                    if (info.Length <= MaxHashedInputBytes)
                    {
                        sha256 = ProjectPackagePathHelpers.ComputeSha256(fullPath);
                    }
                    else
                    {
                        AddWarning(plan, $"input file hash skipped because file is above dry-run hash size limit: {normalized}");
                    }
                }

                plan.InputFiles.Add(new CodexDryRunInputFile
                {
                    RelativePath = SanitizeForArtifact(normalized),
                    Role = SanitizeForArtifact(input.Value.Role),
                    Exists = exists,
                    Required = input.Value.Required,
                    Sha256 = sha256
                });

                if (!exists && input.Value.Required)
                {
                    AddBlockingSafetyCheck(
                        plan,
                        $"input-missing-{NormalizeKey(normalized)}",
                        $"Required dry-run input is missing: {normalized}",
                        TaskFailureCategory.MissingInput);
                }
            }
            catch (InvalidOperationException ex)
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"input-path-{NormalizeKey(input.Key)}",
                    ex.Message,
                    TaskFailureCategory.SandboxViolation);
            }
        }

        MarkStep(
            plan,
            "collect-input-files",
            plan.InputFiles.Any(file => file.Required && !file.Exists) ? "error" : "ok",
            "Required dry-run input files were collected with relative paths and hashes.");
    }

    private static void ValidateAllowedWriteRoots(
        string projectRoot,
        CodexDryRunPlan plan,
        CodexTaskPackage? taskPackage)
    {
        if (taskPackage is null)
        {
            AddBlockingSafetyCheck(
                plan,
                "allowed-write-roots",
                "Allowed write roots could not be checked because task-package.json was not loaded.",
                TaskFailureCategory.MissingInput);
            MarkStep(plan, "validate-allowed-write-roots", "error", "Allowed write roots were not available.");
            return;
        }

        var allowed = new List<string>();
        foreach (var root in taskPackage.Sandbox.AllowedWriteRoots)
        {
            try
            {
                allowed.Add(NormalizeSlash(ProjectPackagePathHelpers.NormalizeProjectRelativePath(
                    projectRoot,
                    root,
                    "dry-run allowed write root")));
            }
            catch (InvalidOperationException ex)
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"allowed-write-root-{NormalizeKey(root)}",
                    ex.Message,
                    TaskFailureCategory.SandboxViolation);
            }
        }

        var expected = ExpectedAllowedWriteRoots.Select(NormalizeSlash).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        var actual = allowed.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            AddBlockingSafetyCheck(
                plan,
                "allowed-write-roots-exact",
                "Future Codex execution allowed write roots must be exactly codex-workspace/ and output-site/current/.",
                TaskFailureCategory.SandboxViolation);
        }

        foreach (var outputRoot in ExpectedAllowedWriteRoots)
        {
            var isAllowed = actual.Contains(NormalizeSlash(outputRoot), StringComparer.OrdinalIgnoreCase);
            plan.OutputTargets.Add(new CodexDryRunOutputTarget
            {
                RelativePath = outputRoot,
                IsAllowedWriteTarget = isAllowed,
                Message = isAllowed
                    ? "Allowed future Codex write target."
                    : "Missing from future Codex allowed write roots."
            });
        }

        MarkStep(
            plan,
            "validate-allowed-write-roots",
            actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase) ? "ok" : "error",
            "Future Codex allowed write roots were checked.");
    }

    private static void ValidateForbiddenRoots(
        string projectRoot,
        CodexDryRunPlan plan,
        CodexTaskPackage? taskPackage)
    {
        if (taskPackage is null)
        {
            AddBlockingSafetyCheck(
                plan,
                "forbidden-roots",
                "Forbidden roots could not be checked because task-package.json was not loaded.",
                TaskFailureCategory.MissingInput);
            MarkStep(plan, "validate-forbidden-roots", "error", "Forbidden roots were not available.");
            return;
        }

        foreach (var root in taskPackage.Sandbox.ForbiddenRoots)
        {
            try
            {
                ProjectPackagePathHelpers.NormalizeRelativeToken(root, "dry-run forbidden root");
            }
            catch (InvalidOperationException ex)
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"forbidden-root-{NormalizeKey(root)}",
                    ex.Message,
                    TaskFailureCategory.SandboxViolation);
            }
        }

        if (taskPackage.Sandbox.ForbiddenRoots.Count == 0)
        {
            AddBlockingSafetyCheck(
                plan,
                "forbidden-roots-empty",
                "Task package forbidden roots are missing.",
                TaskFailureCategory.SandboxViolation);
        }

        if (!taskPackage.Sandbox.ForbiddenRoots.Any(root => root.Contains(".git", StringComparison.OrdinalIgnoreCase)))
        {
            AddBlockingSafetyCheck(
                plan,
                "forbidden-root-git",
                "Task package forbidden roots must include .git.",
                TaskFailureCategory.SandboxViolation);
        }

        foreach (var forbidden in new[] { ".git/config", "../outside.txt", ".ssh/id_rsa", ".codex/config", ".openai/config" })
        {
            var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, forbidden, "dry-run forbidden path");
            if (validation.IsAllowed)
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"sandbox-forbidden-{NormalizeKey(forbidden)}",
                    "Sandbox policy unexpectedly allowed a forbidden path.",
                    TaskFailureCategory.SandboxViolation);
            }
        }

        MarkStep(
            plan,
            "validate-forbidden-roots",
            plan.SafetyChecks.Any(check => check.Key.StartsWith("forbidden-", StringComparison.OrdinalIgnoreCase)
                                           && check.BlocksFutureExecution)
                ? "error"
                : "ok",
            "Forbidden roots and sandbox-denied paths were checked.");
    }

    private static void VerifyRollbackAvailability(
        CodexDryRunPlan plan,
        ConstructionReadinessResult? readiness)
    {
        var rollbackOk = readiness?.Items.Any(item =>
            item.Key == "rollback.snapshotValidation"
            && item.Status == ConstructionReadinessStatus.Ok) == true;
        if (rollbackOk)
        {
            plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
            {
                Key = "rollback-availability",
                Status = "ok",
                Message = "Readiness gate validated rollback availability.",
                BlocksFutureExecution = false
            });
        }
        else
        {
            AddBlockingSafetyCheck(
                plan,
                "rollback-availability",
                "Rollback availability was not verified by the readiness gate.",
                TaskFailureCategory.ValidationError);
        }

        MarkStep(
            plan,
            "verify-rollback-availability",
            rollbackOk ? "ok" : "error",
            rollbackOk
                ? "Rollback readiness is available."
                : "Rollback readiness is missing or blocked.");
    }

    private static void ValidateWorkspacePreparation(
        string projectRoot,
        CodexDryRunPlan plan)
    {
        foreach (var relativePath in new[] { ProjectDirectoryV2.CodexWorkspace, ProjectDirectoryV2.OutputCurrent })
        {
            try
            {
                var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "dry-run workspace path");
                if (!Directory.Exists(fullPath))
                {
                    AddBlockingSafetyCheck(
                        plan,
                        $"workspace-missing-{NormalizeKey(relativePath)}",
                        $"Required future execution directory is missing: {relativePath}",
                        TaskFailureCategory.EnvironmentMissing);
                }
            }
            catch (InvalidOperationException ex)
            {
                AddBlockingSafetyCheck(
                    plan,
                    $"workspace-path-{NormalizeKey(relativePath)}",
                    ex.Message,
                    TaskFailureCategory.SandboxViolation);
            }
        }

        MarkStep(
            plan,
            "prepare-codex-workspace",
            plan.SafetyChecks.Any(check => check.Key.StartsWith("workspace-", StringComparison.OrdinalIgnoreCase)
                                           && check.BlocksFutureExecution)
                ? "error"
                : "ok",
            "Dry-run verified future workspace/output roots without writing website files.");
    }

    private static void SimulateCodexCommand(CodexDryRunPlan plan)
    {
        var step = FindStep(plan, "simulate-codex-command");
        step.Status = "ok";
        step.WouldRunExternalProcess = false;
        step.Message = "Codex CLI not executed. This dry-run only simulates command readiness.";
        plan.WouldExecuteCodexCli = false;
        plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
        {
            Key = "codex-cli-not-executed",
            Status = "ok",
            Message = "Codex CLI not executed.",
            BlocksFutureExecution = false
        });
        plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
        {
            Key = "openai-api-not-called",
            Status = "ok",
            Message = "OpenAI API not called.",
            BlocksFutureExecution = false
        });
    }

    private static void SimulateOutputValidation(
        string projectRoot,
        CodexDryRunPlan plan)
    {
        var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
        var exists = File.Exists(indexPath);
        MarkStep(
            plan,
            "simulate-output-validation",
            "ok",
            exists
                ? "Existing output-site/current/index.html was not modified by dry-run."
                : "Website not generated; output-site/current/index.html is absent.",
            $"{ProjectDirectoryV2.OutputCurrent}/index.html");
        plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
        {
            Key = "website-not-generated",
            Status = "ok",
            Message = "Website not generated.",
            BlocksFutureExecution = false
        });
    }

    private static CodexDryRunPlan CreatePlan(string dryRunId, DateTimeOffset createdAt)
    {
        var plan = new CodexDryRunPlan
        {
            SchemaVersion = CodexDryRunSchema.CurrentSchemaVersion,
            DryRunId = dryRunId,
            CreatedAt = createdAt,
            Mode = "preCodexDryRun",
            WouldExecuteCodexCli = false
        };

        var order = 1;
        foreach (var stepId in DryRunStepIds)
        {
            plan.Steps.Add(new CodexDryRunStep
            {
                StepId = stepId,
                Order = order++,
                Name = stepId,
                Action = stepId,
                Status = "pending",
                Message = "Pending.",
                WouldRunExternalProcess = false
            });
        }

        return plan;
    }

    private static void CompleteResultFromPlan(CodexDryRunResult result, CodexDryRunPlan plan)
    {
        plan.BlockingReasons = plan.BlockingReasons.Select(SanitizeForArtifact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        plan.Warnings = plan.Warnings.Select(SanitizeForArtifact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        plan.SafetyChecks = plan.SafetyChecks
            .Select(check =>
            {
                check.Key = SanitizeForArtifact(check.Key);
                check.Status = SanitizeForArtifact(check.Status);
                check.Message = SanitizeForArtifact(check.Message);
                check.FailureCategory = check.FailureCategory is null ? null : SanitizeForArtifact(check.FailureCategory);
                return check;
            })
            .ToList();

        var ready = plan.BlockingReasons.Count == 0 && plan.SafetyChecks.All(check => !check.BlocksFutureExecution);
        plan.IsReadyForFutureExecution = ready;
        plan.WouldExecuteCodexCli = false;
        result.ProjectId = SanitizeForArtifact(plan.ProjectId);
        result.TaskPackageId = SanitizeForArtifact(plan.TaskPackageId);
        result.IsOk = ready;
        result.IsReadyForFutureExecution = ready;
        result.BlockingReasons = plan.BlockingReasons.ToList();
        result.Warnings = plan.Warnings.ToList();
        result.ExecutedCodexCli = false;
        result.CalledOpenAiApi = false;
        result.GeneratedWebsite = false;
    }

    private static void FinalizeArtifacts(CodexDryRunPlan plan, CodexDryRunResult result)
    {
        CompleteResultFromPlan(result, plan);
        result.CompletedAt = DateTimeOffset.UtcNow;
        foreach (var step in plan.Steps)
        {
            if (step.Status == "pending")
            {
                step.Status = "ok";
                step.Message = "Dry-run step completed without external execution.";
            }

            step.Message = SanitizeForArtifact(step.Message);
            step.RelativePath = step.RelativePath is null ? null : SanitizeForArtifact(NormalizeSlash(step.RelativePath));
            step.WouldRunExternalProcess = false;
        }

        foreach (var file in plan.InputFiles)
        {
            file.RelativePath = SanitizeForArtifact(NormalizeSlash(file.RelativePath));
            file.Role = SanitizeForArtifact(file.Role);
        }

        foreach (var target in plan.OutputTargets)
        {
            target.RelativePath = SanitizeForArtifact(NormalizeSlash(target.RelativePath));
            target.Message = SanitizeForArtifact(target.Message);
        }
    }

    private static void AddBlockingSafetyCheck(
        CodexDryRunPlan plan,
        string key,
        string message,
        TaskFailureCategory category)
    {
        var safeMessage = SanitizeForArtifact(message);
        plan.SafetyChecks.Add(new CodexDryRunSafetyCheck
        {
            Key = SanitizeForArtifact(key),
            Status = "error",
            Message = safeMessage,
            FailureCategory = SanitizeForArtifact(category.ToString()),
            BlocksFutureExecution = true
        });
        AddBlockingReason(plan, $"{key}: {safeMessage}");
    }

    private static void AddBlockingReason(CodexDryRunPlan plan, string reason)
    {
        var safe = SanitizeForArtifact(reason);
        if (!plan.BlockingReasons.Contains(safe, StringComparer.OrdinalIgnoreCase))
        {
            plan.BlockingReasons.Add(safe);
        }
    }

    private static void AddWarning(CodexDryRunPlan plan, string warning)
    {
        var safe = SanitizeForArtifact(warning);
        if (!plan.Warnings.Contains(safe, StringComparer.OrdinalIgnoreCase))
        {
            plan.Warnings.Add(safe);
        }
    }

    private static void MarkStep(
        CodexDryRunPlan plan,
        string stepId,
        string status,
        string message,
        string? relativePath = null)
    {
        var step = FindStep(plan, stepId);
        step.Status = SanitizeForArtifact(status);
        step.Message = SanitizeForArtifact(message);
        step.RelativePath = relativePath is null ? null : SanitizeForArtifact(NormalizeSlash(relativePath));
        step.WouldRunExternalProcess = false;
    }

    private static CodexDryRunStep FindStep(CodexDryRunPlan plan, string stepId)
    {
        return plan.Steps.Single(step => string.Equals(step.StepId, stepId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteStartLogsAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        await logService.WriteAsync(projectRoot, "project", "P1.6 dry-run started", ProjectLogLevel.Info, cancellationToken);
        await logService.WriteAsync(projectRoot, "security", "P1.6 dry-run started; Codex CLI not executed", ProjectLogLevel.Info, cancellationToken);
        await logService.WriteAsync(projectRoot, "codex-task", "P1.6 dry-run started; OpenAI API not called", ProjectLogLevel.Info, cancellationToken);
    }

    private async Task WriteCompletionLogsAsync(
        string projectRoot,
        CodexDryRunResult result,
        CancellationToken cancellationToken)
    {
        var level = result.IsOk ? ProjectLogLevel.Info : ProjectLogLevel.Warning;
        await logService.WriteAsync(
            projectRoot,
            "project",
            $"P1.6 dry-run completed; ready={result.IsReadyForFutureExecution}; Codex CLI not executed; Website not generated",
            level,
            cancellationToken);
        await logService.WriteAsync(
            projectRoot,
            "security",
            "P1.6 dry-run completed; Codex CLI not executed; OpenAI API not called; Website not generated",
            level,
            cancellationToken);
        await logService.WriteAsync(
            projectRoot,
            "codex-task",
            "P1.6 dry-run completed; Codex CLI not executed; OpenAI API not called; Website not generated",
            level,
            cancellationToken);
    }

    private static async Task SavePlanAsync(
        string projectRoot,
        string dryRunId,
        CodexDryRunPlan plan,
        CancellationToken cancellationToken)
    {
        var path = GetPlanPath(projectRoot, dryRunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, plan, WrbJsonOptions.Default, cancellationToken);
    }

    private static async Task SaveResultAsync(
        string projectRoot,
        string dryRunId,
        CodexDryRunResult result,
        CancellationToken cancellationToken)
    {
        var path = GetResultPath(projectRoot, dryRunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result, WrbJsonOptions.Default, cancellationToken);
    }

    private static async Task SaveRecordAsync(
        string projectRoot,
        string dryRunId,
        CodexDryRunPlan plan,
        CodexDryRunResult result,
        CancellationToken cancellationToken)
    {
        var record = new CodexDryRunRecord
        {
            SchemaVersion = CodexDryRunSchema.CurrentSchemaVersion,
            ProjectId = result.ProjectId,
            TaskPackageId = result.TaskPackageId,
            DryRunId = dryRunId,
            CreatedAt = plan.CreatedAt,
            CompletedAt = result.CompletedAt,
            IsDryRun = true,
            Status = result.IsOk ? "succeeded" : "blocked",
            ExecutedCodexCli = false,
            CalledOpenAiApi = false,
            GeneratedWebsite = false,
            PlanRelativePath = $"{GetDryRunDirectoryRelativePath(dryRunId)}/{CodexDryRunSchema.PlanFileName}",
            ResultRelativePath = $"{GetDryRunDirectoryRelativePath(dryRunId)}/{CodexDryRunSchema.ResultFileName}",
            ReportRelativePath = $"{GetDryRunDirectoryRelativePath(dryRunId)}/{CodexDryRunSchema.ReportFileName}",
            BlockingReasons = result.BlockingReasons.ToList(),
            Warnings =
            [
                .. result.Warnings,
                "dry-run only",
                "no Codex CLI process executed",
                "no website generated"
            ]
        };

        record.Warnings = record.Warnings
            .Select(SanitizeForArtifact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var path = GetRecordPath(projectRoot, dryRunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, record, WrbJsonOptions.Default, cancellationToken);
    }

    private static async Task SaveReportAsync(
        string projectRoot,
        string dryRunId,
        CodexDryRunPlan plan,
        CodexDryRunResult result,
        CancellationToken cancellationToken)
    {
        var path = GetReportPath(projectRoot, dryRunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, BuildMarkdownReport(plan, result), Encoding.UTF8, cancellationToken);
    }

    private static string BuildMarkdownReport(
        CodexDryRunPlan plan,
        CodexDryRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Codex CLI Dry-Run Report");
        builder.AppendLine();
        builder.AppendLine($"- DryRunId: `{EscapeInline(result.DryRunId)}`");
        builder.AppendLine($"- ProjectId: `{EscapeInline(result.ProjectId)}`");
        builder.AppendLine($"- TaskPackageId: `{EscapeInline(result.TaskPackageId)}`");
        builder.AppendLine($"- StartedAt: `{result.StartedAt:O}`");
        builder.AppendLine($"- CompletedAt: `{result.CompletedAt:O}`");
        builder.AppendLine($"- IsOk: `{result.IsOk}`");
        builder.AppendLine($"- IsReadyForFutureExecution: `{result.IsReadyForFutureExecution}`");
        builder.AppendLine("- Codex CLI executed: false");
        builder.AppendLine("- OpenAI API called: false");
        builder.AppendLine("- Website generated: false");
        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        AppendList(builder, result.BlockingReasons);
        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        AppendList(builder, result.Warnings);
        builder.AppendLine();
        builder.AppendLine("## Simulated Steps");
        builder.AppendLine();
        builder.AppendLine("| Order | Step | Status | Would run process | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var step in plan.Steps.OrderBy(step => step.Order))
        {
            builder.AppendLine($"| {step.Order} | {EscapeTable(step.StepId)} | {EscapeTable(step.Status)} | {step.WouldRunExternalProcess.ToString().ToLowerInvariant()} | {EscapeTable(step.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Input Files");
        builder.AppendLine();
        builder.AppendLine("| Relative path | Role | Required | Exists | Sha256 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var file in plan.InputFiles)
        {
            builder.AppendLine($"| {EscapeTable(file.RelativePath)} | {EscapeTable(file.Role)} | {file.Required.ToString().ToLowerInvariant()} | {file.Exists.ToString().ToLowerInvariant()} | {EscapeTable(file.Sha256 ?? string.Empty)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Output Targets");
        builder.AppendLine();
        builder.AppendLine("| Relative path | Allowed | Message |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var target in plan.OutputTargets)
        {
            builder.AppendLine($"| {EscapeTable(target.RelativePath)} | {target.IsAllowedWriteTarget.ToString().ToLowerInvariant()} | {EscapeTable(target.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Safety Checks");
        builder.AppendLine();
        builder.AppendLine("| Key | Status | Blocks | Failure category | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var check in plan.SafetyChecks)
        {
            builder.AppendLine($"| {EscapeTable(check.Key)} | {EscapeTable(check.Status)} | {check.BlocksFutureExecution.ToString().ToLowerInvariant()} | {EscapeTable(check.FailureCategory ?? string.Empty)} | {EscapeTable(check.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Explicit Non-Execution Statement");
        builder.AppendLine();
        builder.AppendLine("Dry-run only.");
        builder.AppendLine();
        builder.AppendLine("Codex CLI not executed.");
        builder.AppendLine("OpenAI API not called.");
        builder.AppendLine("Website not generated.");
        builder.AppendLine();
        builder.AppendLine("This was a dry-run only. It did not start Codex CLI, did not call OpenAI API, did not call local model engines, and did not generate a website.");
        return SanitizeForArtifact(builder.ToString());
    }

    private static void AppendList(StringBuilder builder, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            builder.AppendLine("- None.");
            return;
        }

        foreach (var value in values)
        {
            builder.AppendLine($"- {SanitizeForArtifact(value)}");
        }
    }

    private static TaskFailureCategory ClassifyFailure(string message)
    {
        var text = message ?? string.Empty;
        if (text.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.MissingInput;
        }

        if (text.Contains("absolute", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outside", StringComparison.OrdinalIgnoreCase)
            || text.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            || text.Contains("..", StringComparison.Ordinal)
            || text.Contains(".git", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".ssh", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".codex", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".openai", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.SandboxViolation;
        }

        if (ProjectPackagePathHelpers.ContainsSecretOrLocalPath(text))
        {
            return TaskFailureCategory.SecretDetected;
        }

        return TaskFailureCategory.ValidationError;
    }

    private static string CreateDryRunId(DateTimeOffset now)
    {
        return $"dry-run-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..50];
    }

    private static string NormalizeKey(string value)
    {
        var safe = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9]+", "-", RegexOptions.CultureInvariant).Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "item" : safe.ToLowerInvariant();
    }

    private static string NormalizeSlash(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Replace('\\', '/')
            .Trim('/');
    }

    private static string EscapeInline(string value)
    {
        return SanitizeForArtifact(value).Replace("`", "'", StringComparison.Ordinal);
    }

    private static string EscapeTable(string value)
    {
        return SanitizeForArtifact(value)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
    }

    private static string SanitizeForArtifact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.ReplaceLineEndings(" ").Trim();
        sanitized = Regex.Replace(sanitized, @"(?<![A-Za-z0-9_])sk-[A-Za-z0-9_\-]{3,}", "<redacted-sensitive>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)OPENAI_API_KEY", "<redacted-sensitive-marker>");
        sanitized = Regex.Replace(sanitized, @"(?i)(token|password|secret|cookie)", "sensitive-marker");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:\\[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:/[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"/home/[^\s\)""']+", "<redacted-local-path>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)\.(ssh|codex|openai)", "<redacted-credential-dir>");
        return sanitized;
    }
}
