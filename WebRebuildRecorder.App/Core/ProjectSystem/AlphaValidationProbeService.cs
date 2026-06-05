using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class AlphaValidationProbeService
{
    private static readonly string[] ManualFallbackEvidenceFiles =
    [
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        ObservationPackageSchema.RelativePath,
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ConstructionPackageContextSchema.PackageIndexRelativePath
    ];

    private readonly IConstructionReadinessGateService readinessGateService;
    private readonly ICodexDryRunOrchestratorService dryRunService;
    private readonly ProofCheckPackageService proofCheckPackageService;
    private readonly ExecutionPreconditionService executionPreconditionService;

    public AlphaValidationProbeService()
        : this(
            new ConstructionReadinessGateService(),
            new CodexDryRunOrchestratorService(),
            new ProofCheckPackageService(),
            new ExecutionPreconditionService())
    {
    }

    public AlphaValidationProbeService(
        IConstructionReadinessGateService readinessGateService,
        ICodexDryRunOrchestratorService dryRunService,
        ProofCheckPackageService proofCheckPackageService,
        ExecutionPreconditionService executionPreconditionService)
    {
        this.readinessGateService = readinessGateService;
        this.dryRunService = dryRunService;
        this.proofCheckPackageService = proofCheckPackageService;
        this.executionPreconditionService = executionPreconditionService;
    }

    public async Task<AlphaValidationProbeReport> RunAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var report = CreateReport(createdAt);
        string root;

        try
        {
            root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
            report.StoredRelativePath = GetReportJsonRelativePath(report.ProbeId);
            report.MarkdownRelativePath = GetReportMarkdownRelativePath(report.ProbeId);
            Directory.CreateDirectory(GetProbeDirectoryPath(root, report.ProbeId));

            var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(root, cancellationToken);
            report.ProjectId = SanitizeForArtifact(string.IsNullOrWhiteSpace(identity.ProjectId)
                ? "unknown-project"
                : identity.ProjectId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            AddStep(
                report,
                "project.root.valid",
                AlphaValidationStepStatus.Blocked,
                $"Project root could not be validated for alpha probe: {ex.Message}",
                null,
                blocksAlphaEvidence: true);
            FinalizeReport(report);
            return report;
        }

        var outputIndexPath = Path.Combine(root, ProjectDirectoryV2.OutputCurrent, "index.html");
        var outputIndexExistedBefore = File.Exists(outputIndexPath);
        ExecutionPreconditionReport? executionReport = null;

        try
        {
            CheckV2Structure(root, report);
            await CheckManifestAsync(root, report, cancellationToken);
            CheckRequiredFile(root, report, "assets.manifest.exists", AssetsManifestSchema.RelativePath, "assets manifest");
            CheckRequiredFile(root, report, "theme.exists", ThemeManifestSchema.RelativePath, "theme manifest");
            CheckRequiredFile(root, report, "contentMap.exists", ContentMapSchema.RelativePath, "content map");
            CheckRequiredFile(root, report, "observation.package.exists", ObservationPackageSchema.RelativePath, "observation package");
            CheckRequiredFile(root, report, "construction.package.exists", ConstructionPackageSchema.RelativePath, "construction package");
            CheckRequiredFile(root, report, "task.package.exists", CodexTaskPackageSchema.RelativePath, "Codex task package");
            CheckRequiredFile(root, report, "instructions.exists", CodexTaskPackageSchema.InstructionsRelativePath, "Codex task instructions");

            await CheckReadinessAsync(root, report, cancellationToken);
            await CheckDryRunAsync(root, report, cancellationToken);
            await CheckProofPackageAsync(root, report, cancellationToken);
            CheckApprovalArtifacts(root, report);

            executionReport = await CheckExecutionPreconditionAsync(root, report, cancellationToken);
            CheckExecutionPreconditionBlocks(report, executionReport);
            CheckManualFallbackEvidence(root, report);
            CheckRuntimeArtifactsIgnored(report);
            AddKnownDeferredScopeSteps(report);
            CheckNonExecutionBoundary(report, executionReport, outputIndexPath, outputIndexExistedBefore);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddStep(
                report,
                "alphaValidation.unhandledProbeError",
                AlphaValidationStepStatus.Blocked,
                $"Alpha validation probe stopped before completing all checks: {ex.Message}",
                null,
                blocksAlphaEvidence: true);
        }

        FinalizeReport(report);
        await SaveReportsAsync(root, report, cancellationToken);
        return report;
    }

    public async Task<AlphaValidationProbeReport> LoadLatestAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var alphaRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            AlphaValidationProbeSchema.AlphaValidationRootRelativePath,
            "alpha validation root");
        if (!Directory.Exists(alphaRoot))
        {
            throw new FileNotFoundException("No alpha validation reports were found.", alphaRoot);
        }

        var latest = Directory
            .EnumerateFiles(alphaRoot, AlphaValidationProbeSchema.ReportJsonFileName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(latest))
        {
            throw new FileNotFoundException("No alpha validation JSON report was found.", alphaRoot);
        }

        try
        {
            await using var stream = File.OpenRead(latest);
            var report = await JsonSerializer.DeserializeAsync<AlphaValidationProbeReport>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);
            if (report is null)
            {
                throw new InvalidDataException("Alpha validation report is empty.");
            }

            if (!string.Equals(
                    report.SchemaVersion,
                    AlphaValidationProbeSchema.CurrentSchemaVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsupported alpha validation schemaVersion '{report.SchemaVersion}'.");
            }

            return report;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Alpha validation report JSON is invalid: {SanitizeRelativePath(latest)}. {ex.Message}", ex);
        }
    }

    public static string GetProbeDirectoryRelativePath(string probeId)
    {
        var safeProbeId = ProjectPackagePathHelpers.NormalizeRelativeToken(probeId, "probe id");
        return $"{AlphaValidationProbeSchema.AlphaValidationRootRelativePath}/{safeProbeId}";
    }

    public static string GetProbeDirectoryPath(string projectRoot, string probeId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetProbeDirectoryRelativePath(probeId),
            "alpha validation probe directory");
    }

    public static string GetReportJsonRelativePath(string probeId)
    {
        return $"{GetProbeDirectoryRelativePath(probeId)}/{AlphaValidationProbeSchema.ReportJsonFileName}";
    }

    public static string GetReportMarkdownRelativePath(string probeId)
    {
        return $"{GetProbeDirectoryRelativePath(probeId)}/{AlphaValidationProbeSchema.ReportMarkdownFileName}";
    }

    public static string GetReportJsonPath(string projectRoot, string probeId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetReportJsonRelativePath(probeId),
            "alpha validation json report");
    }

    public static string GetReportMarkdownPath(string projectRoot, string probeId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetReportMarkdownRelativePath(probeId),
            "alpha validation markdown report");
    }

    private static AlphaValidationProbeReport CreateReport(DateTimeOffset createdAt)
    {
        return new AlphaValidationProbeReport
        {
            SchemaVersion = AlphaValidationProbeSchema.CurrentSchemaVersion,
            ProbeId = CreateProbeId(createdAt),
            CreatedAt = createdAt,
            ExecutesCodexCli = false,
            CallsOpenAiApi = false,
            CallsLocalModel = false,
            GeneratesWebsite = false
        };
    }

    private static void CheckV2Structure(string projectRoot, AlphaValidationProbeReport report)
    {
        var missing = ProjectDirectoryV2.RequiredDirectories
            .Where(relativePath => !Directory.Exists(Resolve(projectRoot, relativePath, "V2 directory")))
            .ToList();

        AddStep(
            report,
            "project.v2Structure.exists",
            missing.Count == 0 ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            missing.Count == 0
                ? "All Project Directory V2 required directories exist."
                : $"Project Directory V2 is missing: {string.Join(", ", missing)}",
            ".",
            blocksAlphaEvidence: false);
    }

    private static async Task CheckManifestAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        var manifestPath = Resolve(projectRoot, WrbProjectSchema.FileName, "project manifest");
        if (!File.Exists(manifestPath))
        {
            AddStep(
                report,
                "project.manifest.exists",
                AlphaValidationStepStatus.Blocked,
                "project.wrbproj is missing.",
                WrbProjectSchema.FileName,
                blocksAlphaEvidence: false);
            return;
        }

        try
        {
            await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            AddStep(
                report,
                "project.manifest.exists",
                AlphaValidationStepStatus.Passed,
                "project.wrbproj exists and can be loaded.",
                WrbProjectSchema.FileName,
                blocksAlphaEvidence: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddStep(
                report,
                "project.manifest.exists",
                AlphaValidationStepStatus.Blocked,
                $"project.wrbproj exists but could not be loaded: {ex.Message}",
                WrbProjectSchema.FileName,
                blocksAlphaEvidence: false);
        }
    }

    private static void CheckRequiredFile(
        string projectRoot,
        AlphaValidationProbeReport report,
        string key,
        string relativePath,
        string label)
    {
        var exists = File.Exists(Resolve(projectRoot, relativePath, label));
        AddStep(
            report,
            key,
            exists ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            exists ? $"{label} exists." : $"{label} is missing.",
            relativePath,
            blocksAlphaEvidence: false);
    }

    private async Task CheckReadinessAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await readinessGateService.CheckAsync(
                projectRoot,
                ConstructionReadinessMode.PreCodexDryRun,
                cancellationToken);
            AddStep(
                report,
                "readiness.preCodexDryRun.runs",
                readiness.IsReady ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Warning,
                readiness.IsReady
                    ? "P1.5 PreCodexDryRun readiness ran and reported ready."
                    : $"P1.5 PreCodexDryRun readiness ran with blockers: {Describe(readiness.BlockingReasons)}",
                ConstructionReadinessSchema.ReportJsonRelativePath,
                blocksAlphaEvidence: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddStep(
                report,
                "readiness.preCodexDryRun.runs",
                AlphaValidationStepStatus.Blocked,
                $"P1.5 PreCodexDryRun readiness could not run: {ex.Message}",
                ConstructionReadinessSchema.ReportJsonRelativePath,
                blocksAlphaEvidence: false);
        }
    }

    private async Task CheckDryRunAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dryRunService.RunAsync(projectRoot, cancellationToken);
            var status = result.IsOk && result.IsReadyForFutureExecution
                ? AlphaValidationStepStatus.Passed
                : AlphaValidationStepStatus.Warning;
            AddStep(
                report,
                "dryRun.runsOrLatestExists",
                status,
                status == AlphaValidationStepStatus.Passed
                    ? "P1.6 dry-run ran and produced a ready local dry-run report."
                    : $"P1.6 dry-run ran but reported blockers: {Describe(result.BlockingReasons)}",
                $"{CodexDryRunSchema.DryRunsRootRelativePath}/{SanitizeRelativePath(result.DryRunId)}/{CodexDryRunSchema.ResultFileName}",
                blocksAlphaEvidence: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            var latest = await TryLoadLatestDryRunAsync(projectRoot, cancellationToken);
            AddStep(
                report,
                "dryRun.runsOrLatestExists",
                latest.Exists ? AlphaValidationStepStatus.Warning : AlphaValidationStepStatus.Blocked,
                latest.Exists
                    ? $"P1.6 dry-run could not run in this probe, but a latest dry-run result exists: {ex.Message}"
                    : $"P1.6 dry-run could not run and no latest result was found: {ex.Message}",
                latest.RelativePath ?? CodexDryRunSchema.DryRunsRootRelativePath,
                blocksAlphaEvidence: false);
        }
    }

    private async Task CheckProofPackageAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var validation = await proofCheckPackageService.ValidateAsync(projectRoot, cancellationToken);
            AddStep(
                report,
                "proof.package.valid",
                validation.IsOk ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
                validation.IsOk
                    ? "P1.7.1 proof-check package validation passed."
                    : $"P1.7.1 proof-check package validation reported errors: {DescribeProofErrors(validation)}",
                ProofCheckPackageSchema.ManifestRelativePath,
                blocksAlphaEvidence: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddStep(
                report,
                "proof.package.valid",
                AlphaValidationStepStatus.Blocked,
                $"P1.7.1 proof-check package validation could not run: {ex.Message}",
                ProofCheckPackageSchema.ManifestRelativePath,
                blocksAlphaEvidence: false);
        }
    }

    private static void CheckApprovalArtifacts(string projectRoot, AlphaValidationProbeReport report)
    {
        var approvalsRoot = Resolve(projectRoot, ApprovalGateSchema.ApprovalsRootRelativePath, "approval root");
        var hasRequestOrResult = Directory.Exists(approvalsRoot)
                                 && Directory.EnumerateFiles(approvalsRoot, "*", SearchOption.AllDirectories)
                                     .Any(path =>
                                         string.Equals(Path.GetFileName(path), ApprovalGateSchema.ApprovalRequestFileName, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(Path.GetFileName(path), ApprovalGateSchema.ApprovalResultFileName, StringComparison.OrdinalIgnoreCase));

        AddStep(
            report,
            "approval.requestOrResult.exists",
            hasRequestOrResult ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            hasRequestOrResult
                ? "P1.7.2 approval request or result artifact exists."
                : "No P1.7.2 approval request or result artifact was found.",
            ApprovalGateSchema.ApprovalsRootRelativePath,
            blocksAlphaEvidence: false);
    }

    private async Task<ExecutionPreconditionReport?> CheckExecutionPreconditionAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var executionReport = await executionPreconditionService.EvaluateAsync(projectRoot, cancellationToken);
            AddStep(
                report,
                "executionPrecondition.runs",
                AlphaValidationStepStatus.Passed,
                $"P1.7.3 execution precondition service ran and wrote decision {executionReport.Decision}.",
                executionReport.StoredRelativePath,
                blocksAlphaEvidence: false);
            return executionReport;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddStep(
                report,
                "executionPrecondition.runs",
                AlphaValidationStepStatus.Blocked,
                $"P1.7.3 execution precondition service could not run: {ex.Message}",
                ExecutionPreconditionSchema.ExecutionRootRelativePath,
                blocksAlphaEvidence: true);
            return null;
        }
    }

    private static void CheckExecutionPreconditionBlocks(
        AlphaValidationProbeReport report,
        ExecutionPreconditionReport? executionReport)
    {
        var blocksRealExecution = executionReport is not null
                                  && !executionReport.AllowsRealCodexExecution
                                  && !executionReport.ExecutesCodexCli
                                  && !executionReport.CallsOpenAiApi
                                  && !executionReport.CallsLocalModel
                                  && !executionReport.GeneratesWebsite;
        AddStep(
            report,
            "executionPrecondition.blocksRealExecution",
            blocksRealExecution ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            blocksRealExecution
                ? "P1.7.3 execution precondition correctly blocks real execution in the current phase."
                : "P1.7.3 execution precondition did not clearly block real execution.",
            executionReport?.StoredRelativePath ?? ExecutionPreconditionSchema.ExecutionRootRelativePath,
            blocksAlphaEvidence: !blocksRealExecution);
    }

    private static void CheckManualFallbackEvidence(string projectRoot, AlphaValidationProbeReport report)
    {
        var missing = ManualFallbackEvidenceFiles
            .Where(relativePath => !File.Exists(Resolve(projectRoot, relativePath, "manual fallback evidence")))
            .ToList();

        var contextRoot = Resolve(projectRoot, ConstructionPackageContextSchema.ContextRootRelativePath, "manual fallback context");
        if (!Directory.Exists(contextRoot))
        {
            missing.Add(ConstructionPackageContextSchema.ContextRootRelativePath);
        }

        AddStep(
            report,
            "manualFallback.evidenceExists",
            missing.Count == 0 ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            missing.Count == 0
                ? "Manual fallback evidence inputs exist; no manual export fallback writer was implemented or run."
                : $"Manual fallback evidence is missing: {string.Join(", ", missing)}",
            ProjectDirectoryV2.CodexTask,
            blocksAlphaEvidence: false);
    }

    private static void CheckRuntimeArtifactsIgnored(AlphaValidationProbeReport report)
    {
        var sourceRoot = FindSourceRoot();
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            AddStep(
                report,
                "runtimeArtifacts.ignored",
                AlphaValidationStepStatus.Warning,
                "Source root was not found, so alpha-validation .gitignore coverage could not be checked here.",
                ".gitignore",
                blocksAlphaEvidence: false);
            return;
        }

        var projectIgnorePath = Path.Combine(sourceRoot, ".gitignore");
        var repositoryIgnorePath = Path.Combine(Directory.GetParent(sourceRoot)?.FullName ?? string.Empty, ".gitignore");
        var projectHasRule = File.Exists(projectIgnorePath)
                             && File.ReadAllText(projectIgnorePath).Contains("codex-task/alpha-validation", StringComparison.OrdinalIgnoreCase);
        var repositoryHasRule = File.Exists(repositoryIgnorePath)
                                && File.ReadAllText(repositoryIgnorePath).Contains("codex-task/alpha-validation", StringComparison.OrdinalIgnoreCase);

        AddStep(
            report,
            "runtimeArtifacts.ignored",
            projectHasRule && repositoryHasRule ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            projectHasRule && repositoryHasRule
                ? "Project and repository .gitignore files cover codex-task/alpha-validation runtime artifacts."
                : "Missing .gitignore coverage for codex-task/alpha-validation runtime artifacts.",
            ".gitignore",
            blocksAlphaEvidence: false);
    }

    private static void AddKnownDeferredScopeSteps(AlphaValidationProbeReport report)
    {
        AddStep(
            report,
            "alphaValidation.earlyAlpha.warning",
            AlphaValidationStepStatus.Warning,
            "This is an early Alpha 0.1 validation probe, not production readiness and not P2 UI.",
            null,
            blocksAlphaEvidence: false);

        AddStep(
            report,
            "failureRecovery.p1.7.4.postponed",
            AlphaValidationStepStatus.NotImplemented,
            "P1.7.4 Failure recovery policy service is postponed for this probe, not cancelled.",
            null,
            blocksAlphaEvidence: false);
    }

    private static void CheckNonExecutionBoundary(
        AlphaValidationProbeReport report,
        ExecutionPreconditionReport? executionReport,
        string outputIndexPath,
        bool outputIndexExistedBefore)
    {
        var outputIndexCreated = File.Exists(outputIndexPath) && !outputIndexExistedBefore;
        var executionReportSafe = executionReport is null
                                  || (!executionReport.ExecutesCodexCli
                                      && !executionReport.CallsOpenAiApi
                                      && !executionReport.CallsLocalModel
                                      && !executionReport.GeneratesWebsite);
        var passed = !report.ExecutesCodexCli
                     && !report.CallsOpenAiApi
                     && !report.CallsLocalModel
                     && !report.GeneratesWebsite
                     && executionReportSafe
                     && !outputIndexCreated;

        AddStep(
            report,
            "nonExecutionBoundary.enforced",
            passed ? AlphaValidationStepStatus.Passed : AlphaValidationStepStatus.Blocked,
            passed
                ? "This probe did not execute Codex CLI, run any codex command, call AI APIs or local models, generate a website, or create output-site/current/index.html."
                : "The alpha validation non-execution boundary was violated.",
            null,
            blocksAlphaEvidence: !passed);
    }

    private static async Task<(bool Exists, string? RelativePath)> TryLoadLatestDryRunAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var dryRunsRoot = Resolve(projectRoot, CodexDryRunSchema.DryRunsRootRelativePath, "dry-runs root");
        if (!Directory.Exists(dryRunsRoot))
        {
            return (false, CodexDryRunSchema.DryRunsRootRelativePath);
        }

        foreach (var path in Directory
                     .EnumerateFiles(dryRunsRoot, CodexDryRunSchema.ResultFileName, SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(path);
                var result = await JsonSerializer.DeserializeAsync<CodexDryRunResult>(
                    stream,
                    WrbJsonOptions.Default,
                    cancellationToken);
                if (result is not null)
                {
                    return (true, ToProjectRelativePath(projectRoot, path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        return (false, CodexDryRunSchema.DryRunsRootRelativePath);
    }

    private static void FinalizeReport(AlphaValidationProbeReport report)
    {
        report.ExecutesCodexCli = false;
        report.CallsOpenAiApi = false;
        report.CallsLocalModel = false;
        report.GeneratesWebsite = false;

        foreach (var step in report.Steps)
        {
            step.Key = SanitizeForArtifact(step.Key);
            step.Message = SanitizeForArtifact(step.Message);
            step.RelativePath = step.RelativePath is null ? null : SanitizeRelativePath(step.RelativePath);
        }

        report.BlockingReasons = report.Steps
            .Where(step => step.Status == AlphaValidationStepStatus.Blocked)
            .Select(step => $"{step.Key}: {step.Message}")
            .Select(SanitizeForArtifact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.Warnings = report.Steps
            .Where(step => step.Status is AlphaValidationStepStatus.Warning or AlphaValidationStepStatus.NotImplemented)
            .Select(step => $"{step.Key}: {step.Message}")
            .Select(SanitizeForArtifact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var criticalBoundaryBlocked = report.Steps.Any(step =>
            step.BlocksAlphaEvidence
            && step.Status is AlphaValidationStepStatus.Blocked or AlphaValidationStepStatus.NotImplemented);
        var nonExecutionPassed = report.Steps.Any(step =>
            string.Equals(step.Key, "nonExecutionBoundary.enforced", StringComparison.OrdinalIgnoreCase)
            && step.Status == AlphaValidationStepStatus.Passed);
        var executionBlockedPassed = report.Steps.Any(step =>
            string.Equals(step.Key, "executionPrecondition.blocksRealExecution", StringComparison.OrdinalIgnoreCase)
            && step.Status == AlphaValidationStepStatus.Passed);

        report.IsUsableAsAlphaEvidence = !criticalBoundaryBlocked && nonExecutionPassed && executionBlockedPassed;
        report.NextRecommendedAction = report.IsUsableAsAlphaEvidence
            ? "Use this report as Alpha 0.1 local pipeline evidence, then continue with P1.7.4-A Failure recovery models + static policy table."
            : "Fix critical alpha probe blockers first, then rerun P1.8-0 before continuing P1.7.4-A.";
    }

    private static void AddStep(
        AlphaValidationProbeReport report,
        string key,
        AlphaValidationStepStatus status,
        string message,
        string? relativePath,
        bool blocksAlphaEvidence)
    {
        report.Steps.Add(new AlphaValidationStep
        {
            Key = SanitizeForArtifact(key),
            Status = status,
            Message = SanitizeForArtifact(message),
            RelativePath = relativePath is null ? null : SanitizeRelativePath(relativePath),
            BlocksAlphaEvidence = blocksAlphaEvidence
        });
    }

    private static async Task SaveReportsAsync(
        string projectRoot,
        AlphaValidationProbeReport report,
        CancellationToken cancellationToken)
    {
        var jsonPath = GetReportJsonPath(projectRoot, report.ProbeId);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, report, WrbJsonOptions.Default, cancellationToken);
        }

        var markdownPath = GetReportMarkdownPath(projectRoot, report.ProbeId);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdownReport(report), Encoding.UTF8, cancellationToken);
    }

    private static string BuildMarkdownReport(AlphaValidationProbeReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Alpha Validation Probe Report");
        builder.AppendLine();
        builder.AppendLine($"ProjectId: `{EscapeInline(report.ProjectId)}`");
        builder.AppendLine($"ProbeId: `{EscapeInline(report.ProbeId)}`");
        builder.AppendLine($"CreatedAt: `{report.CreatedAt:O}`");
        builder.AppendLine($"IsUsableAsAlphaEvidence: `{report.IsUsableAsAlphaEvidence.ToString().ToLowerInvariant()}`");
        builder.AppendLine();
        builder.AppendLine("This probe does not execute Codex CLI.");
        builder.AppendLine("This probe does not run any codex command.");
        builder.AppendLine("This probe does not call OpenAI API.");
        builder.AppendLine("This probe does not call local model engines.");
        builder.AppendLine("This probe does not generate a website.");
        builder.AppendLine("This probe does not write output-site/current/index.html.");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Steps: {report.Steps.Count}");
        builder.AppendLine($"- Blocking reasons: {report.BlockingReasons.Count}");
        builder.AppendLine($"- Warnings: {report.Warnings.Count}");
        builder.AppendLine($"- ExecutesCodexCli: {report.ExecutesCodexCli.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- CallsOpenAiApi: {report.CallsOpenAiApi.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- CallsLocalModel: {report.CallsLocalModel.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- GeneratesWebsite: {report.GeneratesWebsite.ToString().ToLowerInvariant()}");
        builder.AppendLine();
        builder.AppendLine("## Steps");
        builder.AppendLine();
        builder.AppendLine("| Key | Status | BlocksAlphaEvidence | RelativePath | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var step in report.Steps)
        {
            builder.AppendLine($"| {EscapeTable(step.Key)} | {step.Status} | {step.BlocksAlphaEvidence.ToString().ToLowerInvariant()} | {EscapeTable(step.RelativePath ?? string.Empty)} | {EscapeTable(step.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        AppendList(builder, report.BlockingReasons);
        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        AppendList(builder, report.Warnings);
        builder.AppendLine();
        builder.AppendLine("## Next Recommended Action");
        builder.AppendLine();
        builder.AppendLine(SanitizeForArtifact(report.NextRecommendedAction));
        return SanitizeMarkdown(builder.ToString());
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

    private static string Describe(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "none" : string.Join("; ", values.Take(5).Select(SanitizeForArtifact));
    }

    private static string DescribeProofErrors(ProofCheckValidationResult result)
    {
        return string.Join(
            "; ",
            result.Items
                .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase) || item.BlocksExecution)
                .Take(5)
                .Select(item => $"{SanitizeForArtifact(item.Key)}:{SanitizeRelativePath(item.RelativePath ?? string.Empty)}:{SanitizeForArtifact(item.Message)}"));
    }

    private static string Resolve(string projectRoot, string relativePath, string fieldName)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, fieldName);
    }

    private static string ToProjectRelativePath(string projectRoot, string fullPath)
    {
        return Path.GetRelativePath(projectRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string CreateProbeId(DateTimeOffset now)
    {
        var raw = $"alpha-probe-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(raw.Length, 64)];
    }

    private static string FindSourceRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WebRebuildRecorder.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return string.Empty;
    }

    private static string SanitizeRelativePath(string path)
    {
        var normalized = (path ?? string.Empty)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Replace('\\', '/')
            .Trim('/');
        return SanitizeForArtifact(Path.IsPathRooted(normalized) ? "<redacted-local-path>" : normalized);
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
        sanitized = Regex.Replace(sanitized, @"(?i)(api[_-]?key|token|password|secret|cookie)\s*[:=]\s*[^,\s;]+", "$1=<redacted-sensitive>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:\\[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:/[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"/home/[^\s\)""']+", "<redacted-local-path>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)\.(ssh|codex|openai)(?=$|[\\/ .:;""'`,\)\]])", "<redacted-credential-dir>");
        return sanitized;
    }

    private static string SanitizeMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value;
        sanitized = Regex.Replace(sanitized, @"(?<![A-Za-z0-9_])sk-[A-Za-z0-9_\-]{3,}", "<redacted-sensitive>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)OPENAI_API_KEY", "<redacted-sensitive-marker>");
        sanitized = Regex.Replace(sanitized, @"(?i)(api[_-]?key|token|password|secret|cookie)\s*[:=]\s*[^,\s;]+", "$1=<redacted-sensitive>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:\\[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:/[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"/home/[^\s\)""']+", "<redacted-local-path>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)\.(ssh|codex|openai)(?=$|[\\/ .:;""'`,\)\]])", "<redacted-credential-dir>");
        return sanitized.TrimEnd() + Environment.NewLine;
    }
}
