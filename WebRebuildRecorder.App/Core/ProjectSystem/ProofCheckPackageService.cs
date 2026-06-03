using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ProofCheckPackageService
{
    private static readonly IReadOnlyList<ProofCheckRequiredCheck> DefaultRequiredChecks =
    [
        new() { Key = "create-file-in-codex-workspace", Description = "Future executor creates one proof file inside the allowed Codex workspace." },
        new() { Key = "deny-write-dot-git", Description = "Future executor must be unable to write .git." },
        new() { Key = "deny-write-outside-project-root", Description = "Future executor must be unable to write outside the project root." },
        new() { Key = "deny-write-system-directory", Description = "Future executor must be unable to write system directories." },
        new() { Key = "deny-access-credential-directory", Description = "Future executor must be unable to access credential directories." },
        new() { Key = "read-task-package", Description = "Future executor can read the task package." },
        new() { Key = "write-codex-workspace", Description = "Future executor can write the proof workspace under codex-workspace." },
        new() { Key = "write-output-site-proof-subdirectory-only", Description = "Future executor can write only the proof subdirectory under output-site/current." },
        new() { Key = "hash-check-created-file", Description = "Future proof file can be hash checked." },
        new() { Key = "record-proof-logs", Description = "Future proof logs can be recorded." }
    ];

    private static readonly IReadOnlyList<string> RequiredInstructionPhrases =
    [
        "This is a future proof-check instruction package.",
        "P1.7.1 does not execute this instruction.",
        "Do not execute Codex CLI in P1.7.1.",
        "Do not call OpenAI API in P1.7.1.",
        "Do not call local model engines in P1.7.1.",
        "Do not call Ollama or LM Studio in P1.7.1.",
        "Do not generate a website.",
        "Do not write output-site/current/index.html.",
        "Only future approved proof-check execution may create proof-created-file.txt."
    ];

    private static readonly Regex SensitiveOrLocalPathPattern = new(
        @"(?i)(sk-[A-Za-z0-9_\-]{3,}|OPENAI_API_KEY|api[_-]?key|\bsecret\b|\btoken\b|\bpassword\b|\bcookie\b|[A-Za-z]:\\+|[A-Za-z]:/|/home/|\.ssh|\.codex|\.openai)");

    public async Task<ProofCheckPackageManifest> CreateNewAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(root, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var proofPackageId = CreatePackageId(now);
        var projectId = string.IsNullOrWhiteSpace(identity.ProjectId)
            ? "unknown-project"
            : identity.ProjectId.Trim();

        var manifest = CreateManifest(projectId, proofPackageId, now);
        var request = CreateRequest(manifest);
        NormalizeAndValidateManifest(root, manifest);
        NormalizeAndValidateRequest(root, request);

        Directory.CreateDirectory(GetProofRootPath(root));
        await WriteJsonAsync(GetManifestPath(root), manifest, cancellationToken);
        await WriteJsonAsync(GetRequestPath(root), request, cancellationToken);
        await File.WriteAllTextAsync(GetInstructionsPath(root), BuildInstructionsMarkdown(request), Encoding.UTF8, cancellationToken);

        await ValidateAsync(root, cancellationToken);
        return manifest;
    }

    public async Task<ProofCheckPackageManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var path = GetManifestPath(root);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Proof-check package manifest was not found.", path);
        }

        try
        {
            var manifest = await ReadJsonAsync<ProofCheckPackageManifest>(path, cancellationToken);
            if (manifest is null)
            {
                throw new InvalidDataException("Proof-check package manifest is empty or invalid.");
            }

            ValidateManifestSchema(manifest);
            NormalizeAndValidateManifest(root, manifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Proof-check package manifest JSON is invalid. {ex.Message}", ex);
        }
    }

    public async Task<ProofCheckValidationResult> ValidateAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        Directory.CreateDirectory(GetProofRootPath(root));

        var result = new ProofCheckValidationResult();
        var manifestPath = GetManifestPath(root);
        var requestPath = GetRequestPath(root);
        var instructionsPath = GetInstructionsPath(root);

        AddFileExists(result, "proof.manifest", ProofCheckPackageSchema.ManifestRelativePath, manifestPath);
        AddFileExists(result, "proof.request", ProofCheckPackageSchema.RequestRelativePath, requestPath);
        AddFileExists(result, "proof.instructions", ProofCheckPackageSchema.InstructionsRelativePath, instructionsPath);

        ProofCheckPackageManifest? manifest = null;
        ProofCheckRequest? request = null;

        if (File.Exists(manifestPath))
        {
            manifest = await TryLoadManifestForValidationAsync(root, manifestPath, result, cancellationToken);
        }

        if (File.Exists(requestPath))
        {
            request = await TryLoadRequestForValidationAsync(root, requestPath, result, cancellationToken);
        }

        if (manifest is not null)
        {
            result.ProofPackageId = manifest.ProofPackageId;
            ValidateManifestContent(root, manifest, result);
        }

        if (request is not null)
        {
            result.ProofPackageId = string.IsNullOrWhiteSpace(result.ProofPackageId)
                ? request.ProofPackageId
                : result.ProofPackageId;
            ValidateRequestContent(root, request, result);
        }

        if (manifest is not null && request is not null)
        {
            AddComparisonCheck(
                result,
                "proof.requestMatchesManifest",
                manifest.ProofPackageId,
                request.ProofPackageId,
                ProofCheckPackageSchema.RequestRelativePath);
        }

        await ValidateInstructionsAsync(instructionsPath, result, cancellationToken);
        AddPlannedRuntimeFileAbsent(result, "proof.plannedCreatedFileAbsent", ProofCheckPackageSchema.PlannedCreatedFileRelativePath, GetPlannedCreatedFilePath(root));
        AddPlannedRuntimeFileAbsent(result, "proof.plannedResultAbsent", ProofCheckPackageSchema.PlannedResultRelativePath, GetPlannedResultPath(root));
        AddPlannedRuntimeFileAbsent(result, "proof.plannedReportAbsent", ProofCheckPackageSchema.PlannedReportRelativePath, GetPlannedReportPath(root));
        AddPlannedRuntimeFileAbsent(result, "proof.outputIndexAbsent", $"{ProjectDirectoryV2.OutputCurrent}/index.html", Path.Combine(root, ProjectDirectoryV2.OutputCurrent, "index.html"));

        result.IsOk = result.Items.All(item => !string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
        await WriteValidationReportsAsync(root, result, manifest, cancellationToken);
        return result;
    }

    public static string GetProofRootPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.ProofRootRelativePath,
            "proof root");
    }

    public static string GetManifestPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.ManifestRelativePath,
            "proof manifest");
    }

    public static string GetRequestPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.RequestRelativePath,
            "proof request");
    }

    public static string GetInstructionsPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.InstructionsRelativePath,
            "proof instructions");
    }

    public static string GetValidationReportJsonPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.ValidationReportJsonRelativePath,
            "proof package validation report json");
    }

    public static string GetValidationReportMarkdownPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.ValidationReportMarkdownRelativePath,
            "proof package validation report markdown");
    }

    public static string GetPlannedCreatedFilePath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.PlannedCreatedFileRelativePath,
            "planned proof created file");
    }

    public static string GetPlannedResultPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.PlannedResultRelativePath,
            "planned proof result");
    }

    public static string GetPlannedReportPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProofCheckPackageSchema.PlannedReportRelativePath,
            "planned proof report");
    }

    private static ProofCheckPackageManifest CreateManifest(
        string projectId,
        string proofPackageId,
        DateTimeOffset now)
    {
        return new ProofCheckPackageManifest
        {
            SchemaVersion = ProofCheckPackageSchema.CurrentSchemaVersion,
            ProjectId = projectId,
            ProofPackageId = proofPackageId,
            CreatedAt = now,
            Mode = "packageOnly",
            ExecutesCodexCli = false,
            CallsOpenAiApi = false,
            GeneratesWebsite = false,
            ProofRequestRelativePath = ProofCheckPackageSchema.RequestRelativePath,
            ProofInstructionsRelativePath = ProofCheckPackageSchema.InstructionsRelativePath,
            PlannedProofResultRelativePath = ProofCheckPackageSchema.PlannedResultRelativePath,
            PlannedProofReportRelativePath = ProofCheckPackageSchema.PlannedReportRelativePath,
            PlannedCreatedFileRelativePath = ProofCheckPackageSchema.PlannedCreatedFileRelativePath,
            RequiredChecks = CloneRequiredChecks(),
            AllowedWriteTargets = CreateAllowedWriteTargets(proofPackageId),
            DeniedWriteTargets = CreateDeniedWriteTargets(),
            InputReferences = CreateInputReferences()
        };
    }

    private static ProofCheckRequest CreateRequest(ProofCheckPackageManifest manifest)
    {
        return new ProofCheckRequest
        {
            SchemaVersion = ProofCheckPackageSchema.CurrentSchemaVersion,
            ProjectId = manifest.ProjectId,
            ProofPackageId = manifest.ProofPackageId,
            CreatedAt = manifest.CreatedAt,
            ExecutionMode = "futureProofCheckPackageOnly",
            MustNotExecuteInThisRound = true,
            RequiredChecks = CloneRequiredChecks(),
            AllowedWriteTargets = manifest.AllowedWriteTargets.Select(CloneTarget).ToList(),
            DeniedWriteTargets = manifest.DeniedWriteTargets.Select(CloneTarget).ToList(),
            InputReferences = manifest.InputReferences.Select(CloneInputReference).ToList()
        };
    }

    private static List<ProofCheckRequiredCheck> CloneRequiredChecks()
    {
        return DefaultRequiredChecks
            .Select(check => new ProofCheckRequiredCheck
            {
                Key = check.Key,
                Description = check.Description,
                Required = check.Required
            })
            .ToList();
    }

    private static List<ProofCheckPathTarget> CreateAllowedWriteTargets(string proofPackageId)
    {
        return
        [
            new()
            {
                RelativePath = $"{ProjectDirectoryV2.CodexWorkspace}/proof/{proofPackageId}",
                Purpose = "Future proof workspace file creation only.",
                IsAllowed = true
            },
            new()
            {
                RelativePath = $"{ProjectDirectoryV2.OutputCurrent}/__proofcheck/{proofPackageId}",
                Purpose = "Future isolated proof output subdirectory only.",
                IsAllowed = true
            }
        ];
    }

    private static List<ProofCheckPathTarget> CreateDeniedWriteTargets()
    {
        return
        [
            new() { RelativePath = ".git/", Purpose = "Repository metadata must not be writable.", IsAllowed = false },
            new() { RelativePath = "../outside", Purpose = "Outside-project path traversal must be denied.", IsAllowed = false },
            new() { RelativePath = "<system-dir>", Purpose = "System directories must be denied.", IsAllowed = false },
            new() { RelativePath = "<credential-dir>/.ssh", Purpose = "SSH credentials must be denied.", IsAllowed = false },
            new() { RelativePath = "<credential-dir>/.codex", Purpose = "Codex credentials must be denied.", IsAllowed = false },
            new() { RelativePath = "<credential-dir>/.openai", Purpose = "OpenAI credentials must be denied.", IsAllowed = false },
            new() { RelativePath = "<app-base-dir>", Purpose = "Application runtime directory must be denied.", IsAllowed = false },
            new() { RelativePath = "WebRebuildRecorder.App/", Purpose = "Application source directory must be denied.", IsAllowed = false },
            new() { RelativePath = "WebRebuildRecorder.FoundationSelfTest/", Purpose = "Self-test source directory must be denied.", IsAllowed = false }
        ];
    }

    private static List<ProofCheckInputReference> CreateInputReferences()
    {
        return
        [
            new() { RelativePath = CodexTaskPackageSchema.RelativePath, Role = "taskPackage", Required = true },
            new() { RelativePath = CodexTaskPackageSchema.InstructionsRelativePath, Role = "taskInstructions", Required = true },
            new() { RelativePath = ConstructionPackageSchema.RelativePath, Role = "constructionPackage", Required = false },
            new() { RelativePath = ConstructionPackageContextSchema.PackageIndexRelativePath, Role = "constructionContextIndex", Required = false }
        ];
    }

    private static ProofCheckPathTarget CloneTarget(ProofCheckPathTarget target)
    {
        return new ProofCheckPathTarget
        {
            RelativePath = target.RelativePath,
            Purpose = target.Purpose,
            IsAllowed = target.IsAllowed
        };
    }

    private static ProofCheckInputReference CloneInputReference(ProofCheckInputReference input)
    {
        return new ProofCheckInputReference
        {
            RelativePath = input.RelativePath,
            Role = input.Role,
            Required = input.Required,
            Sha256 = input.Sha256
        };
    }

    private static async Task<ProofCheckPackageManifest?> TryLoadManifestForValidationAsync(
        string projectRoot,
        string path,
        ProofCheckValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await ReadJsonAsync<ProofCheckPackageManifest>(path, cancellationToken);
            if (manifest is null)
            {
                result.Items.Add(Error("proof.manifestInvalid", "Proof manifest is empty or invalid.", ProofCheckPackageSchema.ManifestRelativePath));
                return null;
            }

            ValidateManifestSchema(manifest);
            NormalizeAndValidateManifest(projectRoot, manifest);
            result.Items.Add(Ok("proof.schemaVersion", "schemaVersion is supported.", ProofCheckPackageSchema.ManifestRelativePath));
            return manifest;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or IOException)
        {
            result.Items.Add(Error("proof.manifestInvalid", SanitizeForArtifact(ex.Message), ProofCheckPackageSchema.ManifestRelativePath));
            return null;
        }
    }

    private static async Task<ProofCheckRequest?> TryLoadRequestForValidationAsync(
        string projectRoot,
        string path,
        ProofCheckValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await ReadJsonAsync<ProofCheckRequest>(path, cancellationToken);
            if (request is null)
            {
                result.Items.Add(Error("proof.requestInvalid", "Proof request is empty or invalid.", ProofCheckPackageSchema.RequestRelativePath));
                return null;
            }

            ValidateRequestSchema(request);
            NormalizeAndValidateRequest(projectRoot, request);
            result.Items.Add(Ok("proof.requestSchemaVersion", "request schemaVersion is supported.", ProofCheckPackageSchema.RequestRelativePath));
            return request;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or IOException)
        {
            result.Items.Add(Error("proof.requestInvalid", SanitizeForArtifact(ex.Message), ProofCheckPackageSchema.RequestRelativePath));
            return null;
        }
    }

    private static void ValidateManifestContent(
        string projectRoot,
        ProofCheckPackageManifest manifest,
        ProofCheckValidationResult result)
    {
        AddRequiredText(result, "proof.projectId", manifest.ProjectId, ProofCheckPackageSchema.ManifestRelativePath);
        AddRequiredText(result, "proof.proofPackageId", manifest.ProofPackageId, ProofCheckPackageSchema.ManifestRelativePath);
        AddFalseFlag(result, "proof.executesCodexCli", manifest.ExecutesCodexCli, ProofCheckPackageSchema.ManifestRelativePath);
        AddFalseFlag(result, "proof.callsOpenAiApi", manifest.CallsOpenAiApi, ProofCheckPackageSchema.ManifestRelativePath);
        AddFalseFlag(result, "proof.generatesWebsite", manifest.GeneratesWebsite, ProofCheckPackageSchema.ManifestRelativePath);
        AddSafeProjectPath(result, projectRoot, "proof.requestPath", manifest.ProofRequestRelativePath);
        AddSafeProjectPath(result, projectRoot, "proof.instructionsPath", manifest.ProofInstructionsRelativePath);
        AddSafeProjectPath(result, projectRoot, "proof.plannedResultPath", manifest.PlannedProofResultRelativePath);
        AddSafeProjectPath(result, projectRoot, "proof.plannedReportPath", manifest.PlannedProofReportRelativePath);
        AddSafeProjectPath(result, projectRoot, "proof.plannedCreatedFilePath", manifest.PlannedCreatedFileRelativePath);
        ValidateRequiredChecks(result, "proof.requiredChecks", manifest.RequiredChecks, ProofCheckPackageSchema.ManifestRelativePath);
        ValidateAllowedTargets(result, projectRoot, manifest.ProofPackageId, manifest.AllowedWriteTargets, ProofCheckPackageSchema.ManifestRelativePath);
        ValidateDeniedTargets(result, manifest.DeniedWriteTargets, ProofCheckPackageSchema.ManifestRelativePath);
        ValidateInputReferences(result, projectRoot, manifest.InputReferences, ProofCheckPackageSchema.ManifestRelativePath);
    }

    private static void ValidateRequestContent(
        string projectRoot,
        ProofCheckRequest request,
        ProofCheckValidationResult result)
    {
        AddRequiredText(result, "proof.requestProjectId", request.ProjectId, ProofCheckPackageSchema.RequestRelativePath);
        AddRequiredText(result, "proof.requestProofPackageId", request.ProofPackageId, ProofCheckPackageSchema.RequestRelativePath);
        if (!request.MustNotExecuteInThisRound)
        {
            result.Items.Add(Error("proof.requestMustNotExecute", "proof-request.json must state that P1.7.1 does not execute.", ProofCheckPackageSchema.RequestRelativePath));
        }
        else
        {
            result.Items.Add(Ok("proof.requestMustNotExecute", "P1.7.1 non-execution flag is set.", ProofCheckPackageSchema.RequestRelativePath));
        }

        ValidateRequiredChecks(result, "proof.requestRequiredChecks", request.RequiredChecks, ProofCheckPackageSchema.RequestRelativePath);
        ValidateAllowedTargets(result, projectRoot, request.ProofPackageId, request.AllowedWriteTargets, ProofCheckPackageSchema.RequestRelativePath);
        ValidateDeniedTargets(result, request.DeniedWriteTargets, ProofCheckPackageSchema.RequestRelativePath);
        ValidateInputReferences(result, projectRoot, request.InputReferences, ProofCheckPackageSchema.RequestRelativePath);
    }

    private static void ValidateRequiredChecks(
        ProofCheckValidationResult result,
        string keyPrefix,
        List<ProofCheckRequiredCheck> checks,
        string relativePath)
    {
        if (checks.Count == 0)
        {
            result.Items.Add(Error(keyPrefix, "Proof package must define required checks.", relativePath));
            return;
        }

        foreach (var required in DefaultRequiredChecks)
        {
            var found = checks.Any(check =>
                string.Equals(check.Key, required.Key, StringComparison.OrdinalIgnoreCase)
                && check.Required);
            result.Items.Add(found
                ? Ok($"{keyPrefix}.{required.Key}", "Required proof check is present.", relativePath)
                : Error($"{keyPrefix}.{required.Key}", $"Required proof check is missing: {required.Key}", relativePath));
        }
    }

    private static void ValidateAllowedTargets(
        ProofCheckValidationResult result,
        string projectRoot,
        string proofPackageId,
        List<ProofCheckPathTarget> targets,
        string relativePath)
    {
        if (targets.Count == 0)
        {
            result.Items.Add(Error("proof.allowedWriteTargets", "Proof package must define allowed write targets.", relativePath));
            return;
        }

        foreach (var target in targets)
        {
            if (!target.IsAllowed)
            {
                result.Items.Add(Error("proof.allowedWriteTarget.flag", "Allowed write target must have isAllowed=true.", target.RelativePath));
                continue;
            }

            try
            {
                var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, target.RelativePath, "proof allowed write target");
                if (IsForbiddenSourceOrIndexTarget(normalized))
                {
                    result.Items.Add(Error("proof.allowedWriteTarget.forbidden", "Allowed proof target points to a forbidden source or output index path.", normalized));
                    continue;
                }

                if (!IsExpectedAllowedTarget(normalized, proofPackageId))
                {
                    result.Items.Add(Error("proof.allowedWriteTarget.scope", "Allowed proof target is outside the two P1.7.1 proof-only roots.", normalized));
                    continue;
                }

                result.Items.Add(Ok($"proof.allowedWriteTarget.{normalized}", "Allowed proof target is scoped to an approved proof-only directory.", normalized));
            }
            catch (InvalidOperationException ex)
            {
                result.Items.Add(Error("proof.allowedWriteTarget.path", SanitizeForArtifact(ex.Message), target.RelativePath));
            }
        }
    }

    private static void ValidateDeniedTargets(
        ProofCheckValidationResult result,
        List<ProofCheckPathTarget> targets,
        string relativePath)
    {
        if (targets.Count == 0)
        {
            result.Items.Add(Error("proof.deniedWriteTargets", "Proof package must define denied write targets.", relativePath));
            return;
        }

        foreach (var target in targets)
        {
            if (target.IsAllowed)
            {
                result.Items.Add(Error("proof.deniedWriteTarget.flag", "Denied write target must have isAllowed=false.", target.RelativePath));
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.RelativePath) || ContainsConcreteSecretOrLocalPath(target.RelativePath))
            {
                result.Items.Add(Error("proof.deniedWriteTarget.path", "Denied target must be a safe placeholder or relative denial sample, not a real local path.", SanitizeForArtifact(target.RelativePath)));
                continue;
            }

            result.Items.Add(Ok($"proof.deniedWriteTarget.{SanitizeKey(target.RelativePath)}", "Denied target is represented as a non-writable placeholder or denial sample.", target.RelativePath));
        }

        AddDeniedTargetPresence(result, targets, ".git", "proof.denied.includesGit");
        AddDeniedTargetPresence(result, targets, "../outside", "proof.denied.includesOutsideProject");
        AddDeniedTargetPresence(result, targets, "<system-dir>", "proof.denied.includesSystemDir");
        AddDeniedTargetPresence(result, targets, "<credential-dir>/.ssh", "proof.denied.includesCredentialDir");
        AddDeniedTargetPresence(result, targets, "<credential-dir>/.codex", "proof.denied.includesCodexCredentialDir");
        AddDeniedTargetPresence(result, targets, "<credential-dir>/.openai", "proof.denied.includesOpenAiCredentialDir");
        AddDeniedTargetPresence(result, targets, "<app-base-dir>", "proof.denied.includesAppBaseDir");
        AddDeniedTargetPresence(result, targets, "WebRebuildRecorder.App", "proof.denied.includesAppSource");
        AddDeniedTargetPresence(result, targets, "WebRebuildRecorder.FoundationSelfTest", "proof.denied.includesSelfTestSource");
    }

    private static void ValidateInputReferences(
        ProofCheckValidationResult result,
        string projectRoot,
        List<ProofCheckInputReference> inputs,
        string relativePath)
    {
        if (inputs.Count == 0)
        {
            result.Items.Add(Error("proof.inputReferences", "Proof package must define input references.", relativePath));
            return;
        }

        foreach (var input in inputs)
        {
            try
            {
                var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, input.RelativePath, "proof input reference");
                var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, "proof input reference");
                if (File.Exists(fullPath))
                {
                    input.Sha256 = ProjectPackagePathHelpers.ComputeSha256(fullPath);
                    result.Items.Add(Ok($"proof.input.{input.Role}", "Proof input reference is safe and readable.", normalized));
                }
                else if (input.Required)
                {
                    result.Items.Add(Error($"proof.input.{input.Role}", "Required proof input reference is missing.", normalized));
                }
                else
                {
                    result.Items.Add(Warning($"proof.input.{input.Role}", "Optional proof input reference is missing.", normalized));
                }
            }
            catch (InvalidOperationException ex)
            {
                result.Items.Add(Error($"proof.input.{input.Role}", SanitizeForArtifact(ex.Message), input.RelativePath));
            }
        }
    }

    private static async Task ValidateInstructionsAsync(
        string instructionsPath,
        ProofCheckValidationResult result,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(instructionsPath))
        {
            return;
        }

        var content = await File.ReadAllTextAsync(instructionsPath, cancellationToken);
        foreach (var phrase in RequiredInstructionPhrases)
        {
            result.Items.Add(content.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                ? Ok($"proof.instructions.{SanitizeKey(phrase)}", "Required non-execution boundary phrase is present.", ProofCheckPackageSchema.InstructionsRelativePath)
                : Error($"proof.instructions.{SanitizeKey(phrase)}", $"proof-instructions.md is missing required phrase: {phrase}", ProofCheckPackageSchema.InstructionsRelativePath));
        }
    }

    private static void NormalizeAndValidateManifest(
        string projectRoot,
        ProofCheckPackageManifest manifest)
    {
        manifest.SchemaVersion = manifest.SchemaVersion?.Trim() ?? string.Empty;
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.ProofPackageId = manifest.ProofPackageId?.Trim() ?? string.Empty;
        manifest.CreatedAt = manifest.CreatedAt == default ? DateTimeOffset.UtcNow : manifest.CreatedAt;
        manifest.Mode = NormalizeText(manifest.Mode, "packageOnly");
        manifest.ProofRequestRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, manifest.ProofRequestRelativePath, "proof request path");
        manifest.ProofInstructionsRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, manifest.ProofInstructionsRelativePath, "proof instructions path");
        manifest.PlannedProofResultRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, manifest.PlannedProofResultRelativePath, "planned proof result path");
        manifest.PlannedProofReportRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, manifest.PlannedProofReportRelativePath, "planned proof report path");
        manifest.PlannedCreatedFileRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, manifest.PlannedCreatedFileRelativePath, "planned proof created file path");
        manifest.RequiredChecks ??= [];
        manifest.AllowedWriteTargets ??= [];
        manifest.DeniedWriteTargets ??= [];
        manifest.InputReferences ??= [];
        manifest.Warnings = NormalizeStrings(manifest.Warnings);
    }

    private static void NormalizeAndValidateRequest(
        string projectRoot,
        ProofCheckRequest request)
    {
        request.SchemaVersion = request.SchemaVersion?.Trim() ?? string.Empty;
        request.ProjectId = request.ProjectId?.Trim() ?? string.Empty;
        request.ProofPackageId = request.ProofPackageId?.Trim() ?? string.Empty;
        request.CreatedAt = request.CreatedAt == default ? DateTimeOffset.UtcNow : request.CreatedAt;
        request.ExecutionMode = NormalizeText(request.ExecutionMode, "futureProofCheckPackageOnly");
        request.RequiredChecks ??= [];
        request.AllowedWriteTargets ??= [];
        request.DeniedWriteTargets ??= [];
        request.InputReferences ??= [];

        foreach (var target in request.AllowedWriteTargets)
        {
            target.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, target.RelativePath, "proof request allowed write target");
        }

        foreach (var input in request.InputReferences)
        {
            input.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, input.RelativePath, "proof request input reference");
        }
    }

    private static void ValidateManifestSchema(ProofCheckPackageManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, ProofCheckPackageSchema.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported proof manifest schemaVersion '{manifest.SchemaVersion}'.");
        }
    }

    private static void ValidateRequestSchema(ProofCheckRequest request)
    {
        if (!string.Equals(request.SchemaVersion, ProofCheckPackageSchema.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported proof request schemaVersion '{request.SchemaVersion}'.");
        }
    }

    private static void AddFileExists(
        ProofCheckValidationResult result,
        string key,
        string relativePath,
        string fullPath)
    {
        result.Items.Add(File.Exists(fullPath)
            ? Ok(key, "File exists.", relativePath)
            : Error(key, "Required proof package file is missing.", relativePath));
    }

    private static void AddSafeProjectPath(
        ProofCheckValidationResult result,
        string projectRoot,
        string key,
        string relativePath)
    {
        try
        {
            var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, key);
            result.Items.Add(Ok(key, "Path is project-relative and safe.", normalized));
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error(key, SanitizeForArtifact(ex.Message), relativePath));
        }
    }

    private static void AddRequiredText(
        ProofCheckValidationResult result,
        string key,
        string value,
        string relativePath)
    {
        result.Items.Add(string.IsNullOrWhiteSpace(value)
            ? Error(key, "Required value is missing.", relativePath)
            : Ok(key, "Required value is present.", relativePath));
    }

    private static void AddFalseFlag(
        ProofCheckValidationResult result,
        string key,
        bool value,
        string relativePath)
    {
        result.Items.Add(value
            ? Error(key, "P1.7.1 proof package must not enable execution/API/website flags.", relativePath)
            : Ok(key, "Non-execution flag is false as required.", relativePath));
    }

    private static void AddComparisonCheck(
        ProofCheckValidationResult result,
        string key,
        string expected,
        string actual,
        string relativePath)
    {
        result.Items.Add(string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
            ? Ok(key, "Manifest and request values match.", relativePath)
            : Error(key, "Manifest and request values do not match.", relativePath));
    }

    private static void AddPlannedRuntimeFileAbsent(
        ProofCheckValidationResult result,
        string key,
        string relativePath,
        string fullPath)
    {
        result.Items.Add(File.Exists(fullPath)
            ? Error(key, "Runtime proof/result/output file exists but P1.7.1 must not create it.", relativePath)
            : Ok(key, "Runtime proof/result/output file is absent as required.", relativePath));
    }

    private static void AddDeniedTargetPresence(
        ProofCheckValidationResult result,
        List<ProofCheckPathTarget> targets,
        string requiredToken,
        string key)
    {
        var found = targets.Any(target =>
            (target.RelativePath ?? string.Empty).TrimEnd('/', '\\').Contains(requiredToken.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase));
        result.Items.Add(found
            ? Ok(key, "Required denied target placeholder is present.", requiredToken)
            : Error(key, $"Required denied target placeholder is missing: {requiredToken}", ProofCheckPackageSchema.ManifestRelativePath));
    }

    private static bool IsExpectedAllowedTarget(string relativePath, string proofPackageId)
    {
        var expectedWorkspace = $"{ProjectDirectoryV2.CodexWorkspace}/proof/{proofPackageId}";
        var expectedOutputProof = $"{ProjectDirectoryV2.OutputCurrent}/__proofcheck/{proofPackageId}";
        return string.Equals(relativePath, expectedWorkspace, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(expectedWorkspace + "/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(relativePath, expectedOutputProof, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(expectedOutputProof + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenSourceOrIndexTarget(string relativePath)
    {
        return string.Equals(relativePath, $"{ProjectDirectoryV2.OutputCurrent}/index.html", StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith("WebRebuildRecorder.App/", StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith("WebRebuildRecorder.FoundationSelfTest/", StringComparison.OrdinalIgnoreCase)
               || relativePath.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsConcreteSecretOrLocalPath(string value)
    {
        if (value.StartsWith("<", StringComparison.Ordinal) && value.Contains('>', StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = value.Trim();
        return Path.IsPathRooted(normalized) || SensitiveOrLocalPathPattern.IsMatch(normalized);
    }

    private static async Task WriteValidationReportsAsync(
        string projectRoot,
        ProofCheckValidationResult result,
        ProofCheckPackageManifest? manifest,
        CancellationToken cancellationToken)
    {
        var report = new ProofCheckReport
        {
            SchemaVersion = ProofCheckPackageSchema.CurrentSchemaVersion,
            ProjectId = manifest?.ProjectId ?? string.Empty,
            ProofPackageId = result.ProofPackageId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsPackageValid = result.IsOk,
            ExecutedCodexCli = false,
            CalledOpenAiApi = false,
            GeneratedWebsite = false,
            Items = result.Items.Select(SanitizeItem).ToList()
        };

        report.BlockingReasons = report.Items
            .Where(item => item.BlocksExecution)
            .Select(item => $"{item.Key}: {item.Message}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.Warnings = report.Items
            .Where(item => string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Key}: {item.Message}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await WriteJsonAsync(GetValidationReportJsonPath(projectRoot), report, cancellationToken);
        await File.WriteAllTextAsync(GetValidationReportMarkdownPath(projectRoot), BuildValidationReportMarkdown(report), Encoding.UTF8, cancellationToken);
    }

    private static string BuildInstructionsMarkdown(ProofCheckRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Proof-check Instructions Package");
        builder.AppendLine();
        foreach (var phrase in RequiredInstructionPhrases)
        {
            builder.AppendLine(phrase);
        }

        builder.AppendLine();
        builder.AppendLine("This package records future intended checks only. P1.7.1 generates package files and validation reports, but it does not run the checks.");
        builder.AppendLine();
        builder.AppendLine("## Future Intended Checks");
        foreach (var check in request.RequiredChecks)
        {
            builder.AppendLine($"- `{check.Key}`: {check.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("## Future Allowed Write Targets");
        foreach (var target in request.AllowedWriteTargets)
        {
            builder.AppendLine($"- `{target.RelativePath}` - {target.Purpose}");
        }

        builder.AppendLine();
        builder.AppendLine("## Must Remain Denied");
        foreach (var target in request.DeniedWriteTargets)
        {
            builder.AppendLine($"- `{target.RelativePath}` - {target.Purpose}");
        }

        builder.AppendLine();
        builder.AppendLine("## P1.7.1 Boundary");
        builder.AppendLine("- Codex CLI executed: false");
        builder.AppendLine("- OpenAI API called: false");
        builder.AppendLine("- Local model engines called: false");
        builder.AppendLine("- Website generated: false");
        builder.AppendLine("- `proof-created-file.txt` generated: false");
        builder.AppendLine("- `proof-result.json` generated: false");
        builder.AppendLine("- `proof-report.md` generated: false");
        return builder.ToString();
    }

    private static string BuildValidationReportMarkdown(ProofCheckReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Proof-check Package Validation Report");
        builder.AppendLine();
        builder.AppendLine($"Package valid: {report.IsPackageValid.ToString().ToLowerInvariant()}");
        builder.AppendLine($"Codex CLI executed: {report.ExecutedCodexCli.ToString().ToLowerInvariant()}");
        builder.AppendLine($"OpenAI API called: {report.CalledOpenAiApi.ToString().ToLowerInvariant()}");
        builder.AppendLine($"Website generated: {report.GeneratedWebsite.ToString().ToLowerInvariant()}");
        builder.AppendLine();
        builder.AppendLine("P1.7.1 does not execute Codex CLI, does not call OpenAI API, does not call local model engines, and does not generate a website.");
        builder.AppendLine();
        builder.AppendLine("## Items");
        builder.AppendLine();
        builder.AppendLine("| Key | Severity | Blocks | Path | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var item in report.Items)
        {
            builder.AppendLine($"| {EscapeTable(item.Key)} | {EscapeTable(item.Severity)} | {item.BlocksExecution.ToString().ToLowerInvariant()} | {EscapeTable(item.RelativePath ?? string.Empty)} | {EscapeTable(item.Message)} |");
        }

        return builder.ToString();
    }

    private static ProofCheckValidationItem SanitizeItem(ProofCheckValidationItem item)
    {
        return new ProofCheckValidationItem
        {
            Key = SanitizeForArtifact(item.Key),
            Severity = SanitizeForArtifact(item.Severity),
            Message = SanitizeForArtifact(item.Message),
            RelativePath = item.RelativePath is null ? null : SanitizeForArtifact(item.RelativePath),
            BlocksExecution = item.BlocksExecution,
            FailureCategory = item.FailureCategory is null ? null : SanitizeForArtifact(item.FailureCategory)
        };
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, WrbJsonOptions.Default, cancellationToken);
    }

    private static async Task<T?> ReadJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, WrbJsonOptions.Default, cancellationToken);
    }

    private static string CreatePackageId(DateTimeOffset now)
    {
        var raw = $"proof-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(48, raw.Length)];
    }

    private static string NormalizeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static List<string> NormalizeStrings(List<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static ProofCheckValidationItem Ok(string key, string message, string? relativePath = null)
    {
        return new ProofCheckValidationItem
        {
            Key = key,
            Severity = "ok",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = false
        };
    }

    private static ProofCheckValidationItem Warning(string key, string message, string? relativePath = null)
    {
        return new ProofCheckValidationItem
        {
            Key = key,
            Severity = "warning",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = false
        };
    }

    private static ProofCheckValidationItem Error(string key, string message, string? relativePath = null)
    {
        return new ProofCheckValidationItem
        {
            Key = key,
            Severity = "error",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = true,
            FailureCategory = TaskFailureCategory.SandboxViolation.ToString()
        };
    }

    private static string SanitizeKey(string value)
    {
        var safe = Regex.Replace(value, @"[^A-Za-z0-9]+", "-", RegexOptions.CultureInvariant).Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "value" : safe;
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
