using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ApprovalGateService
{
    private const string ExecutionPlanRelativePath = $"{ProjectDirectoryV2.CodexTask}/execution-plan.json";

    private static readonly Regex ApprovalIdPattern = new(
        @"\A[a-z0-9][a-z0-9\-]{0,95}\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveOrLocalPathPattern = new(
        @"(?i)((?<![A-Za-z0-9_])sk-[A-Za-z0-9_\-]{3,}|OPENAI_API_KEY|api[_-]?key|\bsecret\b|\btoken\b|\bpassword\b|\bcookie\b|[A-Za-z]:\\+|[A-Za-z]:/|/home/|\.ssh|\.codex|\.openai)");

    public async Task<ApprovalGateRequest> CreatePendingAsync(
        string projectRoot,
        ApprovalGateType gateType,
        string purpose,
        string requiredSummary,
        string riskWarning,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        if (!Enum.IsDefined(gateType))
        {
            throw new InvalidOperationException($"Approval gate type is not supported: {gateType}.");
        }

        purpose = NormalizeSafeText(purpose, "purpose");
        requiredSummary = NormalizeSafeText(requiredSummary, "required summary");
        riskWarning = NormalizeSafeText(riskWarning, "risk warning");

        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(root, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var approvalId = CreateApprovalId(now);
        var projectId = string.IsNullOrWhiteSpace(identity.ProjectId)
            ? "unknown-project"
            : identity.ProjectId.Trim();
        var binding = CreateCurrentBinding(root, now);

        var request = new ApprovalGateRequest
        {
            SchemaVersion = ApprovalGateSchema.CurrentSchemaVersion,
            ProjectId = projectId,
            ApprovalId = approvalId,
            GateId = approvalId,
            GateType = gateType,
            CreatedAt = now,
            Purpose = purpose,
            RequiredSummary = requiredSummary,
            RiskWarning = riskWarning,
            CannotBeBypassedByAi = true,
            StoredRelativePath = GetRequestRelativePath(approvalId),
            Binding = CloneBinding(binding)
        };

        var result = new ApprovalGateResult
        {
            SchemaVersion = ApprovalGateSchema.CurrentSchemaVersion,
            ProjectId = projectId,
            ApprovalId = approvalId,
            GateId = approvalId,
            GateType = gateType,
            CreatedAt = now,
            UpdatedAt = now,
            Decision = ApprovalDecision.Pending,
            CannotBeBypassedByAi = true,
            StoredRelativePath = GetResultRelativePath(approvalId),
            RequestRelativePath = GetRequestRelativePath(approvalId),
            Binding = CloneBinding(binding)
        };

        Directory.CreateDirectory(GetApprovalDirectoryPath(root, approvalId));
        await WriteJsonAsync(GetRequestPath(root, approvalId), request, cancellationToken);
        await WriteJsonAsync(GetResultPath(root, approvalId), result, cancellationToken);
        await ValidateAsync(root, approvalId, cancellationToken);
        return request;
    }

    public async Task<ApprovalGateRequest> LoadRequestAsync(
        string projectRoot,
        string approvalId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var path = GetRequestPath(root, approvalId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Approval request was not found.", path);
        }

        try
        {
            var request = await ReadJsonAsync<ApprovalGateRequest>(path, cancellationToken);
            if (request is null)
            {
                throw new InvalidDataException("Approval request is empty or invalid.");
            }

            ValidateRequestSchema(request);
            NormalizeAndValidateRequest(root, approvalId, request);
            return request;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Approval request JSON is invalid. {ex.Message}", ex);
        }
    }

    public async Task<ApprovalGateResult> LoadResultAsync(
        string projectRoot,
        string approvalId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var path = GetResultPath(root, approvalId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Approval result was not found.", path);
        }

        try
        {
            var result = await ReadJsonAsync<ApprovalGateResult>(path, cancellationToken);
            if (result is null)
            {
                throw new InvalidDataException("Approval result is empty or invalid.");
            }

            ValidateResultSchema(result);
            NormalizeAndValidateResult(root, approvalId, result);
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Approval result JSON is invalid. {ex.Message}", ex);
        }
    }

    public async Task<ApprovalGateResult> ApproveAsync(
        string projectRoot,
        string approvalId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        reason = NormalizeSafeOptionalText(reason, "approval reason");
        var result = await LoadResultAsync(root, approvalId, cancellationToken);
        EnsureTransitionAllowed(result.Decision, ApprovalDecision.Approved);

        var validation = await ValidateAsync(root, approvalId, cancellationToken);
        if (!validation.IsBindingCurrent
            || validation.Items.Any(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
        {
            var errors = string.Join(
                "; ",
                validation.Items
                    .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
                    .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
            throw new InvalidOperationException($"Approval cannot be approved because its bound task package or instruction files are stale or invalid. {errors}");
        }

        return await SetDecisionAsync(root, result, ApprovalDecision.Approved, reason, cancellationToken);
    }

    public async Task<ApprovalGateResult> RejectAsync(
        string projectRoot,
        string approvalId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        return await SetDecisionAsync(projectRoot, approvalId, ApprovalDecision.Rejected, reason, cancellationToken);
    }

    public async Task<ApprovalGateResult> CancelAsync(
        string projectRoot,
        string approvalId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        return await SetDecisionAsync(projectRoot, approvalId, ApprovalDecision.Cancelled, reason, cancellationToken);
    }

    public async Task<ApprovalGateResult> ExpireAsync(
        string projectRoot,
        string approvalId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        return await SetDecisionAsync(projectRoot, approvalId, ApprovalDecision.Expired, reason, cancellationToken);
    }

    public async Task<ApprovalGateResult> SupersedeAsync(
        string projectRoot,
        string approvalId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        return await SetDecisionAsync(projectRoot, approvalId, ApprovalDecision.Superseded, reason, cancellationToken);
    }

    public async Task<ApprovalGateValidationResult> ValidateAsync(
        string projectRoot,
        string approvalId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        approvalId = NormalizeApprovalId(approvalId);
        Directory.CreateDirectory(GetApprovalDirectoryPath(root, approvalId));

        var validation = new ApprovalGateValidationResult
        {
            ApprovalId = approvalId,
            Decision = ApprovalDecision.Pending
        };

        var requestPath = GetRequestPath(root, approvalId);
        var resultPath = GetResultPath(root, approvalId);

        AddFileExists(validation, "approval.request.exists", GetRequestRelativePath(approvalId), requestPath);
        AddFileExists(validation, "approval.result.exists", GetResultRelativePath(approvalId), resultPath);

        ApprovalGateRequest? request = null;
        ApprovalGateResult? result = null;
        if (File.Exists(requestPath))
        {
            request = await TryLoadRequestForValidationAsync(root, approvalId, requestPath, validation, cancellationToken);
        }

        if (File.Exists(resultPath))
        {
            result = await TryLoadResultForValidationAsync(root, approvalId, resultPath, validation, cancellationToken);
        }

        if (request is not null)
        {
            ValidateRequestContent(root, approvalId, request, validation);
        }

        if (result is not null)
        {
            validation.Decision = result.Decision;
            ValidateResultContent(root, approvalId, result, validation);
        }

        if (request is not null && result is not null)
        {
            AddComparison(validation, "approval.projectId.matches", request.ProjectId, result.ProjectId, GetResultRelativePath(approvalId));
            AddComparison(validation, "approval.approvalId.matches", request.ApprovalId, result.ApprovalId, GetResultRelativePath(approvalId));
            AddComparison(validation, "approval.gateId.matches", request.GateId, result.GateId, GetResultRelativePath(approvalId));
            if (!EqualityComparer<ApprovalGateType>.Default.Equals(request.GateType, result.GateType))
            {
                validation.Items.Add(Error("approval.gateType.matches", "Approval request and result gate types differ.", GetResultRelativePath(approvalId)));
            }

            ValidateBindingPair(request.Binding, result.Binding, validation, approvalId);
        }

        await ScanApprovalFileContentAsync(requestPath, GetRequestRelativePath(approvalId), validation, cancellationToken);
        await ScanApprovalFileContentAsync(resultPath, GetResultRelativePath(approvalId), validation, cancellationToken);

        var binding = result?.Binding ?? request?.Binding;
        validation.IsBindingCurrent = binding is not null
                                      && IsBindingCurrent(root, binding, approvalId, validation);
        AddDecisionStatus(validation, result);

        var hasErrors = validation.Items.Any(item =>
            string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
        validation.IsExecutable = result?.Decision == ApprovalDecision.Approved
                                  && validation.IsBindingCurrent
                                  && !hasErrors;
        validation.IsOk = validation.IsExecutable;

        await WriteValidationReportsAsync(root, approvalId, validation, cancellationToken);
        return validation;
    }

    public static string GetApprovalsRootPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ApprovalGateSchema.ApprovalsRootRelativePath,
            "approval root");
    }

    public static string GetApprovalDirectoryRelativePath(string approvalId)
    {
        var safeApprovalId = NormalizeApprovalId(approvalId);
        return $"{ApprovalGateSchema.ApprovalsRootRelativePath}/{safeApprovalId}";
    }

    public static string GetApprovalDirectoryPath(string projectRoot, string approvalId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetApprovalDirectoryRelativePath(approvalId),
            "approval directory");
    }

    public static string GetRequestRelativePath(string approvalId)
    {
        return $"{GetApprovalDirectoryRelativePath(approvalId)}/{ApprovalGateSchema.ApprovalRequestFileName}";
    }

    public static string GetResultRelativePath(string approvalId)
    {
        return $"{GetApprovalDirectoryRelativePath(approvalId)}/{ApprovalGateSchema.ApprovalResultFileName}";
    }

    public static string GetValidationReportJsonRelativePath(string approvalId)
    {
        return $"{GetApprovalDirectoryRelativePath(approvalId)}/{ApprovalGateSchema.ApprovalValidationReportJsonFileName}";
    }

    public static string GetValidationReportMarkdownRelativePath(string approvalId)
    {
        return $"{GetApprovalDirectoryRelativePath(approvalId)}/{ApprovalGateSchema.ApprovalValidationReportMarkdownFileName}";
    }

    public static string GetRequestPath(string projectRoot, string approvalId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetRequestRelativePath(approvalId),
            "approval request");
    }

    public static string GetResultPath(string projectRoot, string approvalId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetResultRelativePath(approvalId),
            "approval result");
    }

    public static string GetValidationReportJsonPath(string projectRoot, string approvalId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetValidationReportJsonRelativePath(approvalId),
            "approval validation report json");
    }

    public static string GetValidationReportMarkdownPath(string projectRoot, string approvalId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetValidationReportMarkdownRelativePath(approvalId),
            "approval validation report markdown");
    }

    private async Task<ApprovalGateResult> SetDecisionAsync(
        string projectRoot,
        string approvalId,
        ApprovalDecision decision,
        string reason,
        CancellationToken cancellationToken)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        reason = NormalizeSafeOptionalText(reason, "approval reason");
        var result = await LoadResultAsync(root, approvalId, cancellationToken);
        EnsureTransitionAllowed(result.Decision, decision);
        return await SetDecisionAsync(root, result, decision, reason, cancellationToken);
    }

    private async Task<ApprovalGateResult> SetDecisionAsync(
        string projectRoot,
        ApprovalGateResult result,
        ApprovalDecision decision,
        string reason,
        CancellationToken cancellationToken)
    {
        result.Decision = decision;
        result.DecidedAt = DateTimeOffset.UtcNow;
        result.UpdatedAt = result.DecidedAt.Value;
        result.Reason = reason;
        result.CannotBeBypassedByAi = true;
        NormalizeAndValidateResult(projectRoot, result.ApprovalId, result);

        await WriteJsonAsync(GetResultPath(projectRoot, result.ApprovalId), result, cancellationToken);
        await ValidateAsync(projectRoot, result.ApprovalId, cancellationToken);
        return result;
    }

    private static void EnsureTransitionAllowed(ApprovalDecision current, ApprovalDecision next)
    {
        var allowed = (current, next) switch
        {
            (ApprovalDecision.Pending, ApprovalDecision.Approved) => true,
            (ApprovalDecision.Pending, ApprovalDecision.Rejected) => true,
            (ApprovalDecision.Pending, ApprovalDecision.Cancelled) => true,
            (ApprovalDecision.Pending, ApprovalDecision.Expired) => true,
            (ApprovalDecision.Pending, ApprovalDecision.Superseded) => true,
            (ApprovalDecision.Approved, ApprovalDecision.Superseded) => true,
            (ApprovalDecision.Approved, ApprovalDecision.Expired) => true,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Approval transition is not allowed: {current} -> {next}.");
        }
    }

    private static ApprovalBinding CreateCurrentBinding(string projectRoot, DateTimeOffset now)
    {
        var binding = new ApprovalBinding
        {
            BoundAt = now
        };

        BindRequiredFile(
            projectRoot,
            CodexTaskPackageSchema.RelativePath,
            "task package",
            (relativePath, sha256) =>
            {
                binding.TaskPackageRelativePath = relativePath;
                binding.TaskPackageSha256 = sha256;
            });

        BindRequiredFile(
            projectRoot,
            CodexTaskPackageSchema.InstructionsRelativePath,
            "task instructions",
            (relativePath, sha256) =>
            {
                binding.InstructionsRelativePath = relativePath;
                binding.InstructionsSha256 = sha256;
            });

        BindOptionalFile(
            projectRoot,
            FindLatestDryRunPlanRelativePath(projectRoot),
            "dry-run plan",
            (relativePath, sha256) =>
            {
                binding.DryRunPlanRelativePath = relativePath;
                binding.DryRunPlanSha256 = sha256;
            });

        BindOptionalFile(
            projectRoot,
            ProofCheckPackageSchema.ManifestRelativePath,
            "proof manifest",
            (relativePath, sha256) =>
            {
                binding.ProofManifestRelativePath = relativePath;
                binding.ProofManifestSha256 = sha256;
            });

        BindOptionalFile(
            projectRoot,
            ExecutionPlanRelativePath,
            "execution plan",
            (relativePath, sha256) =>
            {
                binding.ExecutionPlanRelativePath = relativePath;
                binding.ExecutionPlanSha256 = sha256;
            });

        return binding;
    }

    private static void BindRequiredFile(
        string projectRoot,
        string relativePath,
        string fieldName,
        Action<string, string> assign)
    {
        var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, fieldName);
        var path = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, fieldName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required approval binding file is missing: {normalized}.");
        }

        assign(normalized, ProjectPackagePathHelpers.ComputeSha256(path));
    }

    private static void BindOptionalFile(
        string projectRoot,
        string? relativePath,
        string fieldName,
        Action<string, string> assign)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, fieldName);
        var path = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, fieldName);
        if (File.Exists(path))
        {
            assign(normalized, ProjectPackagePathHelpers.ComputeSha256(path));
        }
    }

    private static string? FindLatestDryRunPlanRelativePath(string projectRoot)
    {
        var dryRunsRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexDryRunSchema.DryRunsRootRelativePath,
            "dry-runs root");
        if (!Directory.Exists(dryRunsRoot))
        {
            return null;
        }

        var planPath = Directory
            .EnumerateFiles(dryRunsRoot, CodexDryRunSchema.PlanFileName, SearchOption.AllDirectories)
            .Where(path => !HasForbiddenPathSegment(path))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        return planPath is null
            ? null
            : Path.GetRelativePath(projectRoot, planPath.FullName)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool IsBindingCurrent(
        string projectRoot,
        ApprovalBinding binding,
        string approvalId,
        ApprovalGateValidationResult validation)
    {
        var isCurrent = true;
        isCurrent &= ValidateBoundFile(
            projectRoot,
            "approval.binding.taskPackage",
            binding.TaskPackageRelativePath,
            binding.TaskPackageSha256,
            required: true,
            validation,
            approvalId);
        isCurrent &= ValidateBoundFile(
            projectRoot,
            "approval.binding.instructions",
            binding.InstructionsRelativePath,
            binding.InstructionsSha256,
            required: true,
            validation,
            approvalId);
        isCurrent &= ValidateBoundFile(
            projectRoot,
            "approval.binding.dryRunPlan",
            binding.DryRunPlanRelativePath,
            binding.DryRunPlanSha256,
            required: false,
            validation,
            approvalId);
        isCurrent &= ValidateBoundFile(
            projectRoot,
            "approval.binding.proofManifest",
            binding.ProofManifestRelativePath,
            binding.ProofManifestSha256,
            required: false,
            validation,
            approvalId);
        isCurrent &= ValidateBoundFile(
            projectRoot,
            "approval.binding.executionPlan",
            binding.ExecutionPlanRelativePath,
            binding.ExecutionPlanSha256,
            required: false,
            validation,
            approvalId);
        return isCurrent;
    }

    private static bool ValidateBoundFile(
        string projectRoot,
        string key,
        string? relativePath,
        string? expectedSha256,
        bool required,
        ApprovalGateValidationResult validation,
        string approvalId)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(expectedSha256))
        {
            if (required)
            {
                validation.Items.Add(Error($"{key}.missingBinding", "Required approval binding field is missing.", GetRequestRelativePath(approvalId)));
                return false;
            }

            validation.Items.Add(Ok($"{key}.notBound", "Optional approval binding file is not present in this approval.", null));
            return true;
        }

        string normalized;
        string path;
        try
        {
            normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, key);
            path = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, key);
        }
        catch (InvalidOperationException ex)
        {
            validation.Items.Add(Error($"{key}.path", SanitizeForArtifact(ex.Message), SanitizeForArtifact(relativePath)));
            return false;
        }

        if (!File.Exists(path))
        {
            validation.Items.Add(Error($"{key}.missingFile", "Bound approval file no longer exists.", normalized));
            return false;
        }

        var currentHash = ProjectPackagePathHelpers.ComputeSha256(path);
        if (!string.Equals(currentHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            validation.Items.Add(Error($"{key}.hashChanged", "Bound approval file hash changed after approval request creation.", normalized));
            return false;
        }

        validation.Items.Add(Ok($"{key}.hashCurrent", "Bound approval file hash is current.", normalized));
        return true;
    }

    private static void ValidateBindingPair(
        ApprovalBinding requestBinding,
        ApprovalBinding resultBinding,
        ApprovalGateValidationResult validation,
        string approvalId)
    {
        AddComparison(validation, "approval.binding.taskPackagePath.matches", requestBinding.TaskPackageRelativePath, resultBinding.TaskPackageRelativePath, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.taskPackageHash.matches", requestBinding.TaskPackageSha256, resultBinding.TaskPackageSha256, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.instructionsPath.matches", requestBinding.InstructionsRelativePath, resultBinding.InstructionsRelativePath, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.instructionsHash.matches", requestBinding.InstructionsSha256, resultBinding.InstructionsSha256, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.dryRunPlanPath.matches", requestBinding.DryRunPlanRelativePath ?? string.Empty, resultBinding.DryRunPlanRelativePath ?? string.Empty, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.dryRunPlanHash.matches", requestBinding.DryRunPlanSha256 ?? string.Empty, resultBinding.DryRunPlanSha256 ?? string.Empty, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.proofManifestPath.matches", requestBinding.ProofManifestRelativePath ?? string.Empty, resultBinding.ProofManifestRelativePath ?? string.Empty, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.proofManifestHash.matches", requestBinding.ProofManifestSha256 ?? string.Empty, resultBinding.ProofManifestSha256 ?? string.Empty, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.executionPlanPath.matches", requestBinding.ExecutionPlanRelativePath ?? string.Empty, resultBinding.ExecutionPlanRelativePath ?? string.Empty, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.binding.executionPlanHash.matches", requestBinding.ExecutionPlanSha256 ?? string.Empty, resultBinding.ExecutionPlanSha256 ?? string.Empty, GetResultRelativePath(approvalId));
    }

    private static async Task<ApprovalGateRequest?> TryLoadRequestForValidationAsync(
        string projectRoot,
        string approvalId,
        string path,
        ApprovalGateValidationResult validation,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await ReadJsonAsync<ApprovalGateRequest>(path, cancellationToken);
            if (request is null)
            {
                validation.Items.Add(Error("approval.request.json", "Approval request is empty or invalid.", GetRequestRelativePath(approvalId)));
                return null;
            }

            NormalizeAndValidateRequest(projectRoot, approvalId, request);
            return request;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or IOException)
        {
            validation.Items.Add(Error("approval.request.json", $"Approval request could not be loaded: {SanitizeForArtifact(ex.Message)}", GetRequestRelativePath(approvalId)));
            return null;
        }
    }

    private static async Task<ApprovalGateResult?> TryLoadResultForValidationAsync(
        string projectRoot,
        string approvalId,
        string path,
        ApprovalGateValidationResult validation,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ReadJsonAsync<ApprovalGateResult>(path, cancellationToken);
            if (result is null)
            {
                validation.Items.Add(Error("approval.result.json", "Approval result is empty or invalid.", GetResultRelativePath(approvalId)));
                return null;
            }

            NormalizeAndValidateResult(projectRoot, approvalId, result);
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or IOException)
        {
            validation.Items.Add(Error("approval.result.json", $"Approval result could not be loaded: {SanitizeForArtifact(ex.Message)}", GetResultRelativePath(approvalId)));
            return null;
        }
    }

    private static void ValidateRequestContent(
        string projectRoot,
        string approvalId,
        ApprovalGateRequest request,
        ApprovalGateValidationResult validation)
    {
        AddSchemaCheck(validation, "approval.request.schemaVersion", request.SchemaVersion, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.projectId", request.ProjectId, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.approvalId", request.ApprovalId, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.gateId", request.GateId, GetRequestRelativePath(approvalId));
        AddEnumCheck(validation, "approval.request.gateType", request.GateType, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.purpose", request.Purpose, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.requiredSummary", request.RequiredSummary, GetRequestRelativePath(approvalId));
        AddRequiredText(validation, "approval.request.riskWarning", request.RiskWarning, GetRequestRelativePath(approvalId));
        AddCannotBypassCheck(validation, "approval.request.cannotBeBypassedByAi", request.CannotBeBypassedByAi, GetRequestRelativePath(approvalId));
        AddSafeRelativePathCheck(projectRoot, validation, "approval.request.storedRelativePath", request.StoredRelativePath, GetRequestRelativePath(approvalId));
        AddComparison(validation, "approval.request.approvalId.expected", request.ApprovalId, approvalId, GetRequestRelativePath(approvalId));
        AddComparison(validation, "approval.request.gateId.expected", request.GateId, approvalId, GetRequestRelativePath(approvalId));
    }

    private static void ValidateResultContent(
        string projectRoot,
        string approvalId,
        ApprovalGateResult result,
        ApprovalGateValidationResult validation)
    {
        AddSchemaCheck(validation, "approval.result.schemaVersion", result.SchemaVersion, GetResultRelativePath(approvalId));
        AddRequiredText(validation, "approval.result.projectId", result.ProjectId, GetResultRelativePath(approvalId));
        AddRequiredText(validation, "approval.result.approvalId", result.ApprovalId, GetResultRelativePath(approvalId));
        AddRequiredText(validation, "approval.result.gateId", result.GateId, GetResultRelativePath(approvalId));
        AddEnumCheck(validation, "approval.result.gateType", result.GateType, GetResultRelativePath(approvalId));
        AddDecisionEnumCheck(validation, "approval.result.decision", result.Decision, GetResultRelativePath(approvalId));
        AddCannotBypassCheck(validation, "approval.result.cannotBeBypassedByAi", result.CannotBeBypassedByAi, GetResultRelativePath(approvalId));
        AddSafeRelativePathCheck(projectRoot, validation, "approval.result.storedRelativePath", result.StoredRelativePath, GetResultRelativePath(approvalId));
        AddSafeRelativePathCheck(projectRoot, validation, "approval.result.requestRelativePath", result.RequestRelativePath, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.result.approvalId.expected", result.ApprovalId, approvalId, GetResultRelativePath(approvalId));
        AddComparison(validation, "approval.result.gateId.expected", result.GateId, approvalId, GetResultRelativePath(approvalId));
    }

    private static void AddDecisionStatus(
        ApprovalGateValidationResult validation,
        ApprovalGateResult? result)
    {
        if (result is null)
        {
            validation.Items.Add(Error("approval.decision.missing", "Approval result is missing; execution must remain blocked.", null));
            return;
        }

        switch (result.Decision)
        {
            case ApprovalDecision.Pending:
                validation.Items.Add(Warning("approval.decision.pending", "Pending approval does not authorize execution.", GetResultRelativePath(result.ApprovalId), blocksExecution: true));
                break;
            case ApprovalDecision.Approved:
                if (validation.IsBindingCurrent)
                {
                    validation.Items.Add(Ok("approval.decision.approved", "Approval is approved and bound hashes are current.", GetResultRelativePath(result.ApprovalId)));
                }
                else
                {
                    validation.Items.Add(Error("approval.decision.approvedStaleBinding", "Approved approval has stale bound hashes and cannot authorize execution.", GetResultRelativePath(result.ApprovalId)));
                }

                break;
            case ApprovalDecision.Rejected:
            case ApprovalDecision.Cancelled:
            case ApprovalDecision.Expired:
            case ApprovalDecision.Superseded:
                validation.Items.Add(Error(
                    $"approval.decision.{result.Decision.ToString().ToLowerInvariant()}",
                    $"Approval decision is {result.Decision}; execution must remain blocked.",
                    GetResultRelativePath(result.ApprovalId)));
                break;
            default:
                validation.Items.Add(Error("approval.decision.invalid", "Approval decision is invalid.", GetResultRelativePath(result.ApprovalId)));
                break;
        }
    }

    private static void NormalizeAndValidateRequest(
        string projectRoot,
        string approvalId,
        ApprovalGateRequest request)
    {
        ValidateRequestSchema(request);
        request.ProjectId = request.ProjectId?.Trim() ?? string.Empty;
        request.ApprovalId = NormalizeApprovalId(request.ApprovalId);
        request.GateId = NormalizeApprovalId(string.IsNullOrWhiteSpace(request.GateId) ? request.ApprovalId : request.GateId);
        if (!string.Equals(request.ApprovalId, approvalId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Approval request id does not match the requested approval id.");
        }

        if (!Enum.IsDefined(request.GateType))
        {
            throw new InvalidDataException("Approval request gate type is not supported.");
        }

        request.Purpose = NormalizeSafeText(request.Purpose, "purpose");
        request.RequiredSummary = NormalizeSafeText(request.RequiredSummary, "required summary");
        request.RiskWarning = NormalizeSafeText(request.RiskWarning, "risk warning");
        request.CannotBeBypassedByAi = request.CannotBeBypassedByAi;
        request.StoredRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, request.StoredRelativePath, "approval request stored path");
        request.Binding ??= new ApprovalBinding();
    }

    private static void NormalizeAndValidateResult(
        string projectRoot,
        string approvalId,
        ApprovalGateResult result)
    {
        ValidateResultSchema(result);
        result.ProjectId = result.ProjectId?.Trim() ?? string.Empty;
        result.ApprovalId = NormalizeApprovalId(result.ApprovalId);
        result.GateId = NormalizeApprovalId(string.IsNullOrWhiteSpace(result.GateId) ? result.ApprovalId : result.GateId);
        if (!string.Equals(result.ApprovalId, approvalId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Approval result id does not match the requested approval id.");
        }

        if (!Enum.IsDefined(result.GateType))
        {
            throw new InvalidDataException("Approval result gate type is not supported.");
        }

        if (!Enum.IsDefined(result.Decision))
        {
            throw new InvalidDataException("Approval result decision is not supported.");
        }

        result.Reason = NormalizeSafeOptionalText(result.Reason, "approval reason");
        result.CannotBeBypassedByAi = result.CannotBeBypassedByAi;
        result.StoredRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, result.StoredRelativePath, "approval result stored path");
        result.RequestRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, result.RequestRelativePath, "approval result request path");
        result.Binding ??= new ApprovalBinding();
    }

    private static void ValidateRequestSchema(ApprovalGateRequest request)
    {
        if (!string.Equals(request.SchemaVersion, ApprovalGateSchema.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Approval request schemaVersion '{request.SchemaVersion}' is not supported. Expected '{ApprovalGateSchema.CurrentSchemaVersion}'.");
        }
    }

    private static void ValidateResultSchema(ApprovalGateResult result)
    {
        if (!string.Equals(result.SchemaVersion, ApprovalGateSchema.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Approval result schemaVersion '{result.SchemaVersion}' is not supported. Expected '{ApprovalGateSchema.CurrentSchemaVersion}'.");
        }
    }

    private static void AddFileExists(
        ApprovalGateValidationResult result,
        string key,
        string relativePath,
        string fullPath)
    {
        result.Items.Add(File.Exists(fullPath)
            ? Ok(key, "Approval file exists.", relativePath)
            : Error(key, "Approval file is missing.", relativePath));
    }

    private static void AddSchemaCheck(
        ApprovalGateValidationResult result,
        string key,
        string schemaVersion,
        string relativePath)
    {
        result.Items.Add(string.Equals(schemaVersion, ApprovalGateSchema.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase)
            ? Ok(key, "Approval schema version is supported.", relativePath)
            : Error(key, $"Unsupported approval schema version: {SanitizeForArtifact(schemaVersion)}.", relativePath));
    }

    private static void AddRequiredText(
        ApprovalGateValidationResult result,
        string key,
        string value,
        string relativePath)
    {
        result.Items.Add(string.IsNullOrWhiteSpace(value)
            ? Error(key, "Required approval field is empty.", relativePath)
            : Ok(key, "Required approval field is present.", relativePath));
    }

    private static void AddEnumCheck<T>(
        ApprovalGateValidationResult result,
        string key,
        T value,
        string relativePath)
        where T : struct, Enum
    {
        result.Items.Add(Enum.IsDefined(value)
            ? Ok(key, "Approval enum value is supported.", relativePath)
            : Error(key, "Approval enum value is invalid.", relativePath));
    }

    private static void AddDecisionEnumCheck(
        ApprovalGateValidationResult result,
        string key,
        ApprovalDecision value,
        string relativePath)
    {
        result.Items.Add(Enum.IsDefined(value)
            ? Ok(key, "Approval decision is supported.", relativePath)
            : Error(key, "Approval decision is invalid.", relativePath));
    }

    private static void AddCannotBypassCheck(
        ApprovalGateValidationResult result,
        string key,
        bool cannotBeBypassedByAi,
        string relativePath)
    {
        result.Items.Add(cannotBeBypassedByAi
            ? Ok(key, "Approval cannot be bypassed by AI.", relativePath)
            : Error(key, "Approval must set cannotBeBypassedByAi=true.", relativePath));
    }

    private static void AddSafeRelativePathCheck(
        string projectRoot,
        ApprovalGateValidationResult result,
        string key,
        string relativePath,
        string itemPath)
    {
        try
        {
            var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, key);
            if (IsForbiddenApprovalPath(normalized))
            {
                result.Items.Add(Error(key, "Approval file path points to a forbidden source, output index, or sensitive path.", normalized));
                return;
            }

            result.Items.Add(Ok(key, "Approval path is project-relative and safe.", normalized));
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error(key, SanitizeForArtifact(ex.Message), SanitizeForArtifact(itemPath)));
        }
    }

    private static void AddComparison(
        ApprovalGateValidationResult result,
        string key,
        string left,
        string right,
        string relativePath)
    {
        result.Items.Add(string.Equals(left, right, StringComparison.Ordinal)
            ? Ok(key, "Approval fields match.", relativePath)
            : Error(key, "Approval fields do not match.", relativePath));
    }

    private static async Task ScanApprovalFileContentAsync(
        string path,
        string relativePath,
        ApprovalGateValidationResult validation,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        validation.Items.Add(SensitiveOrLocalPathPattern.IsMatch(content)
            ? Error("approval.fileContent.sensitiveOrLocalPath", "Approval file contains a sensitive marker or local absolute path.", relativePath)
            : Ok("approval.fileContent.safe", "Approval file does not contain known sensitive markers or local absolute paths.", relativePath));
    }

    private static async Task WriteValidationReportsAsync(
        string projectRoot,
        string approvalId,
        ApprovalGateValidationResult result,
        CancellationToken cancellationToken)
    {
        var sanitized = new ApprovalGateValidationResult
        {
            IsOk = result.IsOk,
            IsBindingCurrent = result.IsBindingCurrent,
            IsExecutable = result.IsExecutable,
            ApprovalId = SanitizeForArtifact(result.ApprovalId),
            Decision = result.Decision,
            Items = result.Items.Select(SanitizeItem).ToList()
        };

        await WriteJsonAsync(GetValidationReportJsonPath(projectRoot, approvalId), sanitized, cancellationToken);
        await File.WriteAllTextAsync(
            GetValidationReportMarkdownPath(projectRoot, approvalId),
            BuildValidationReportMarkdown(sanitized),
            Encoding.UTF8,
            cancellationToken);
    }

    private static string BuildValidationReportMarkdown(ApprovalGateValidationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Approval Gate Validation Report");
        builder.AppendLine();
        builder.AppendLine($"ApprovalId: `{EscapeInline(result.ApprovalId)}`");
        builder.AppendLine($"Decision: `{result.Decision}`");
        builder.AppendLine($"Binding current: {result.IsBindingCurrent.ToString().ToLowerInvariant()}");
        builder.AppendLine($"Executable: {result.IsExecutable.ToString().ToLowerInvariant()}");
        builder.AppendLine();
        builder.AppendLine("P1.7.2 validates approval gate persistence only. It does not execute Codex CLI, call OpenAI API, call local model engines, generate websites, or write output-site/current/index.html.");
        builder.AppendLine();
        builder.AppendLine("## Items");
        builder.AppendLine();
        builder.AppendLine("| Key | Severity | Blocks | Path | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var item in result.Items)
        {
            builder.AppendLine($"| {EscapeTable(item.Key)} | {EscapeTable(item.Severity)} | {item.BlocksExecution.ToString().ToLowerInvariant()} | {EscapeTable(item.RelativePath ?? string.Empty)} | {EscapeTable(item.Message)} |");
        }

        return builder.ToString();
    }

    private static ApprovalGateValidationItem SanitizeItem(ApprovalGateValidationItem item)
    {
        return new ApprovalGateValidationItem
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

    private static ApprovalBinding CloneBinding(ApprovalBinding binding)
    {
        var json = JsonSerializer.Serialize(binding, WrbJsonOptions.Default);
        return JsonSerializer.Deserialize<ApprovalBinding>(json, WrbJsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to clone approval binding.");
    }

    private static string NormalizeApprovalId(string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            throw new InvalidOperationException("Approval id cannot be empty.");
        }

        var normalized = approvalId.Trim().ToLowerInvariant();
        if (Path.IsPathRooted(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal)
            || !ApprovalIdPattern.IsMatch(normalized)
            || IsForbiddenApprovalPath(normalized))
        {
            throw new InvalidOperationException($"Approval id is not safe: {SanitizeForArtifact(approvalId)}.");
        }

        return normalized;
    }

    private static string NormalizeSafeText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Approval {fieldName} cannot be empty.");
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (SensitiveOrLocalPathPattern.IsMatch(normalized))
        {
            throw new InvalidOperationException($"Approval {fieldName} contains a sensitive marker or local path.");
        }

        return normalized;
    }

    private static string NormalizeSafeOptionalText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return NormalizeSafeText(value, fieldName);
    }

    private static bool IsForbiddenApprovalPath(string value)
    {
        var normalized = value.Replace('\\', '/');
        return normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/.ssh/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".ssh/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/.codex/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".codex/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/.openai/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".openai/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("WebRebuildRecorder.App/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("WebRebuildRecorder.FoundationSelfTest/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, $"{ProjectDirectoryV2.OutputCurrent}/index.html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasForbiddenPathSegment(string path)
    {
        var parts = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part =>
            string.Equals(part, ".git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".ssh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".codex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".openai", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateApprovalId(DateTimeOffset now)
    {
        var raw = $"approval-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(58, raw.Length)];
    }

    private static ApprovalGateValidationItem Ok(string key, string message, string? relativePath = null)
    {
        return new ApprovalGateValidationItem
        {
            Key = key,
            Severity = "ok",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = false
        };
    }

    private static ApprovalGateValidationItem Warning(
        string key,
        string message,
        string? relativePath = null,
        bool blocksExecution = false)
    {
        return new ApprovalGateValidationItem
        {
            Key = key,
            Severity = "warning",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = blocksExecution
        };
    }

    private static ApprovalGateValidationItem Error(string key, string message, string? relativePath = null)
    {
        return new ApprovalGateValidationItem
        {
            Key = key,
            Severity = "error",
            Message = message,
            RelativePath = relativePath,
            BlocksExecution = true,
            FailureCategory = TaskFailureCategory.ValidationError.ToString()
        };
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
        sanitized = Regex.Replace(sanitized, @"(?i)(api[_-]?key|token|password|secret|cookie)", "sensitive-marker");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:\\[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:/[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"/home/[^\s\)""']+", "<redacted-local-path>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)\.(ssh|codex|openai)", "<redacted-credential-dir>");
        return sanitized;
    }
}
