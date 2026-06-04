using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Logging;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ExecutionPreconditionService
{
    private const long MaxScannedFileBytes = 2L * 1024L * 1024L;

    private static readonly Regex SecretPattern = new(
        @"(?i)((?<![A-Za-z0-9_])sk-[A-Za-z0-9_\-]{3,}|OPENAI_API_KEY\s*[:=]\s*(?!<safe|<redacted|\[redacted)|api[_-]?key\s*[:=]\s*(?!<safe|<redacted|\[redacted)|\b(token|secret|password|cookie)\b\s*[:=]\s*(?!<safe|<redacted|\[redacted))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LocalPathPattern = new(
        @"(?i)((?<![A-Za-z])[A-Za-z]:\\+Users\\+|(?<![A-Za-z])[A-Za-z]:\\+|(?<![A-Za-z])[A-Za-z]:/+|/home/|\.(ssh|codex|openai)(?=$|[\\/ .:;""'`,\)\]]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ContextFreshnessRequiredFiles =
    [
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ConstructionPackageContextSchema.PackageIndexRelativePath
    ];

    private static readonly string[] ManualFallbackRequiredFiles =
    [
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        ObservationPackageSchema.RelativePath
    ];

    private readonly IConstructionReadinessGateService readinessGateService;
    private readonly ProofCheckPackageService proofCheckPackageService;
    private readonly ApprovalGateService approvalGateService;
    private readonly SnapshotRestoreService snapshotRestoreService;
    private readonly SecretAndLocalPathScanService secretScanService;

    public ExecutionPreconditionService()
        : this(
            new ConstructionReadinessGateService(),
            new ProofCheckPackageService(),
            new ApprovalGateService(),
            new SnapshotRestoreService(),
            new SecretAndLocalPathScanService())
    {
    }

    public ExecutionPreconditionService(
        IConstructionReadinessGateService readinessGateService,
        ProofCheckPackageService proofCheckPackageService,
        ApprovalGateService approvalGateService,
        SnapshotRestoreService snapshotRestoreService,
        SecretAndLocalPathScanService secretScanService)
    {
        this.readinessGateService = readinessGateService;
        this.proofCheckPackageService = proofCheckPackageService;
        this.approvalGateService = approvalGateService;
        this.snapshotRestoreService = snapshotRestoreService;
        this.secretScanService = secretScanService;
    }

    public Task<ExecutionPreconditionReport> EvaluateAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        return EvaluateAsync(projectRoot, new ExecutionPreconditionOptions(), cancellationToken);
    }

    public async Task<ExecutionPreconditionReport> EvaluateAsync(
        string projectRoot,
        ExecutionPreconditionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var createdAt = DateTimeOffset.UtcNow;
        var report = CreateReport(createdAt);
        string root;
        try
        {
            root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
            report.StoredRelativePath = GetPreconditionsRelativePath(report.ExecutionId);
            report.MarkdownRelativePath = GetPreconditionsMarkdownRelativePath(report.ExecutionId);
            Directory.CreateDirectory(GetExecutionDirectoryPath(root, report.ExecutionId));
        }
        catch (InvalidOperationException ex)
        {
            AddItem(
                report,
                "project.root.valid",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                ex.Message,
                null,
                true,
                TaskFailureCategory.SandboxViolation.ToString());
            FinalizeReport(report);
            return report;
        }

        var outputIndexPath = Path.Combine(root, ProjectDirectoryV2.OutputCurrent, "index.html");
        var outputIndexExistedBefore = File.Exists(outputIndexPath);
        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(root, cancellationToken);
        report.ProjectId = SanitizeForArtifact(string.IsNullOrWhiteSpace(identity.ProjectId)
            ? "unknown-project"
            : identity.ProjectId);

        ConstructionReadinessResult? readiness = null;
        ApprovalGateResult? latestApproval = null;
        ApprovalGateValidationResult? latestApprovalValidation = null;

        readiness = await CheckReadinessAsync(root, report, cancellationToken);
        await CheckDryRunAsync(root, report, cancellationToken);
        await CheckProofPackageAsync(root, report, cancellationToken);
        CheckProofExecution(report, options);
        (latestApproval, latestApprovalValidation) = await CheckApprovalAsync(root, report, options, cancellationToken);
        await CheckRollbackAsync(root, report, options, cancellationToken);
        CheckSandboxAllowedRoots(root, report);
        CheckSandboxForbiddenRoots(root, report);
        await CheckSecretAndLocalPathsAsync(root, report, cancellationToken);
        CheckOutputSite(root, report, outputIndexExistedBefore);
        CheckCodexWorkspace(root, report);
        CheckLogsWritable(root, report);
        CheckTaskPackageHashStability(root, report, latestApproval, latestApprovalValidation);
        CheckContextFreshness(root, report, readiness);
        CheckManualFallback(root, report, options);
        CheckNonExecutionBoundary(report, outputIndexPath, outputIndexExistedBefore);

        FinalizeReport(report);
        await SaveReportsAsync(root, report, cancellationToken);
        return report;
    }

    public async Task<ExecutionPreconditionReport> LoadLatestAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var executionRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            ExecutionPreconditionSchema.ExecutionRootRelativePath,
            "execution preconditions root");
        if (!Directory.Exists(executionRoot))
        {
            throw new FileNotFoundException("No execution precondition reports were found.", executionRoot);
        }

        var latest = Directory
            .EnumerateFiles(executionRoot, ExecutionPreconditionSchema.PreconditionsFileName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(latest))
        {
            throw new FileNotFoundException("No execution precondition JSON report was found.", executionRoot);
        }

        try
        {
            await using var stream = File.OpenRead(latest);
            var report = await JsonSerializer.DeserializeAsync<ExecutionPreconditionReport>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);
            if (report is null)
            {
                throw new InvalidDataException("Execution precondition report is empty.");
            }

            if (!string.Equals(
                    report.SchemaVersion,
                    ExecutionPreconditionSchema.CurrentSchemaVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsupported execution precondition schemaVersion '{report.SchemaVersion}'.");
            }

            return report;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Execution precondition report JSON is invalid: {latest}. {ex.Message}", ex);
        }
    }

    public static string GetExecutionDirectoryRelativePath(string executionId)
    {
        var safeExecutionId = ProjectPackagePathHelpers.NormalizeRelativeToken(executionId, "execution id");
        return $"{ExecutionPreconditionSchema.ExecutionRootRelativePath}/{safeExecutionId}";
    }

    public static string GetExecutionDirectoryPath(string projectRoot, string executionId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetExecutionDirectoryRelativePath(executionId),
            "execution preconditions directory");
    }

    public static string GetPreconditionsRelativePath(string executionId)
    {
        return $"{GetExecutionDirectoryRelativePath(executionId)}/{ExecutionPreconditionSchema.PreconditionsFileName}";
    }

    public static string GetPreconditionsMarkdownRelativePath(string executionId)
    {
        return $"{GetExecutionDirectoryRelativePath(executionId)}/{ExecutionPreconditionSchema.PreconditionsMarkdownFileName}";
    }

    public static string GetPreconditionsPath(string projectRoot, string executionId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetPreconditionsRelativePath(executionId),
            "execution preconditions json");
    }

    public static string GetPreconditionsMarkdownPath(string projectRoot, string executionId)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            GetPreconditionsMarkdownRelativePath(executionId),
            "execution preconditions markdown");
    }

    private async Task<ConstructionReadinessResult?> CheckReadinessAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await readinessGateService.CheckAsync(
                projectRoot,
                ConstructionReadinessMode.PreCodexDryRun,
                cancellationToken);
            AddItem(
                report,
                "readiness.preCodexDryRun.passed",
                readiness.IsReady ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
                readiness.IsReady ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
                readiness.IsReady
                    ? "P1.5 PreCodexDryRun readiness passed."
                    : $"P1.5 PreCodexDryRun readiness reported blockers: {string.Join("; ", readiness.BlockingReasons.Take(3))}",
                ConstructionReadinessSchema.ReportJsonRelativePath,
                !readiness.IsReady,
                readiness.Items.FirstOrDefault(item => item.BlocksExecution)?.FailureCategory?.ToString()
                ?? TaskFailureCategory.ValidationError.ToString());
            return readiness;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddItem(
                report,
                "readiness.preCodexDryRun.passed",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                $"P1.5 PreCodexDryRun readiness could not run: {ex.Message}",
                ConstructionReadinessSchema.ReportJsonRelativePath,
                true,
                ClassifyFailure(ex.Message));
            return null;
        }
    }

    private async Task CheckDryRunAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        CancellationToken cancellationToken)
    {
        var latest = await TryLoadLatestDryRunResultAsync(projectRoot, cancellationToken);
        if (latest.Result is null)
        {
            AddItem(
                report,
                "dryRun.completed",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                latest.ErrorMessage ?? "No P1.6 dry-run result was found.",
                latest.RelativePath,
                true,
                TaskFailureCategory.MissingInput.ToString());
            return;
        }

        var result = latest.Result;
        var flagsSafe = !result.ExecutedCodexCli && !result.CalledOpenAiApi && !result.GeneratedWebsite;
        var passed = result.IsOk && result.IsReadyForFutureExecution && flagsSafe;
        AddItem(
            report,
            "dryRun.completed",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "P1.6 dry-run completed with non-execution flags intact."
                : $"P1.6 dry-run is not ready or has unsafe flags. IsOk={result.IsOk}; ready={result.IsReadyForFutureExecution}; executedCodexCli={result.ExecutedCodexCli}; calledOpenAiApi={result.CalledOpenAiApi}; generatedWebsite={result.GeneratedWebsite}.",
            latest.RelativePath,
            !passed,
            TaskFailureCategory.ValidationError.ToString());
    }

    private async Task CheckProofPackageAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var validation = await proofCheckPackageService.ValidateAsync(projectRoot, cancellationToken);
            AddItem(
                report,
                "proof.package.valid",
                validation.IsOk ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
                validation.IsOk ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
                validation.IsOk
                    ? "P1.7.1 proof-check package is valid and package-only."
                    : $"P1.7.1 proof-check package is not valid: {DescribeProofErrors(validation)}",
                ProofCheckPackageSchema.ManifestRelativePath,
                !validation.IsOk,
                TaskFailureCategory.ValidationError.ToString());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            AddItem(
                report,
                "proof.package.valid",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                $"P1.7.1 proof-check package validation failed: {ex.Message}",
                ProofCheckPackageSchema.ManifestRelativePath,
                true,
                ClassifyFailure(ex.Message));
        }
    }

    private static void CheckProofExecution(
        ExecutionPreconditionReport report,
        ExecutionPreconditionOptions options)
    {
        AddItem(
            report,
            "proof.execution.passed",
            options.RequireProofExecutionPassed ? ExecutionPreconditionSeverity.Error : ExecutionPreconditionSeverity.Info,
            options.RequireProofExecutionPassed ? ExecutionPreconditionStatus.NotImplemented : ExecutionPreconditionStatus.NotApplicable,
            "Real proof execution is not implemented in P1.7.3.",
            ProofCheckPackageSchema.PlannedResultRelativePath,
            options.RequireProofExecutionPassed,
            "ProofCheckFailed");
    }

    private async Task<(ApprovalGateResult? Result, ApprovalGateValidationResult? Validation)> CheckApprovalAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        ExecutionPreconditionOptions options,
        CancellationToken cancellationToken)
    {
        var approval = await TryLoadLatestExecutionApprovalAsync(projectRoot, cancellationToken);
        if (approval.Result is null)
        {
            AddItem(
                report,
                "approval.execution.approved",
                options.RequireApprovalApproved ? ExecutionPreconditionSeverity.Error : ExecutionPreconditionSeverity.Info,
                options.RequireApprovalApproved ? ExecutionPreconditionStatus.Blocked : ExecutionPreconditionStatus.NotApplicable,
                approval.ErrorMessage ?? "No approval gate for future real Codex execution was found.",
                approval.RelativePath,
                options.RequireApprovalApproved,
                TaskFailureCategory.MissingInput.ToString());
            return (null, null);
        }

        try
        {
            var validation = await approvalGateService.ValidateAsync(projectRoot, approval.Result.ApprovalId, cancellationToken);
            var isApproved = validation.Decision == ApprovalDecision.Approved && validation.IsExecutable;
            var failureCategory = validation.Decision switch
            {
                ApprovalDecision.Pending => TaskFailureCategory.MissingInput.ToString(),
                ApprovalDecision.Rejected or ApprovalDecision.Cancelled => TaskFailureCategory.CancelledByUser.ToString(),
                ApprovalDecision.Expired or ApprovalDecision.Superseded => TaskFailureCategory.MissingInput.ToString(),
                _ when !validation.IsBindingCurrent => "StaleApproval",
                _ => TaskFailureCategory.ValidationError.ToString()
            };

            AddItem(
                report,
                "approval.execution.approved",
                isApproved ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
                isApproved ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
                isApproved
                    ? "Future real execution approval is approved, current, and cannot be bypassed by AI."
                    : $"Future real execution approval is not executable. Decision={validation.Decision}; bindingCurrent={validation.IsBindingCurrent}; executable={validation.IsExecutable}.",
                ApprovalGateService.GetResultRelativePath(approval.Result.ApprovalId),
                !isApproved,
                failureCategory);
            return (approval.Result, validation);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            AddItem(
                report,
                "approval.execution.approved",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                $"Approval validation failed: {ex.Message}",
                approval.RelativePath,
                true,
                ClassifyFailure(ex.Message));
            return (approval.Result, null);
        }
    }

    private async Task CheckRollbackAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        ExecutionPreconditionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshots = await snapshotRestoreService.ListSnapshotsAsync(projectRoot, cancellationToken);
            var snapshot = snapshots.FirstOrDefault();
            if (snapshot is null)
            {
                AddItem(
                    report,
                    "rollback.safetySnapshot.available",
                    options.RequireSafetySnapshot ? ExecutionPreconditionSeverity.Error : ExecutionPreconditionSeverity.Warning,
                    options.RequireSafetySnapshot ? ExecutionPreconditionStatus.Blocked : ExecutionPreconditionStatus.Warning,
                    "No readable safety snapshot is available.",
                    ProjectSnapshotSchema.SnapshotRootRelativePath,
                    options.RequireSafetySnapshot,
                    TaskFailureCategory.MissingInput.ToString());
                return;
            }

            var validation = await snapshotRestoreService.ValidateSnapshotAsync(projectRoot, snapshot.SnapshotId, cancellationToken);
            AddItem(
                report,
                "rollback.safetySnapshot.available",
                validation.IsOk ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
                validation.IsOk ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
                validation.IsOk
                    ? "Readable snapshot and restore validation foundation are available."
                    : "Latest snapshot failed validation and cannot be used as rollback safety evidence.",
                $"{ProjectSnapshotSchema.SnapshotRootRelativePath}/{snapshot.SnapshotId}/{ProjectSnapshotSchema.ManifestFileName}",
                !validation.IsOk,
                TaskFailureCategory.ValidationError.ToString());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            AddItem(
                report,
                "rollback.safetySnapshot.available",
                options.RequireSafetySnapshot ? ExecutionPreconditionSeverity.Error : ExecutionPreconditionSeverity.Warning,
                options.RequireSafetySnapshot ? ExecutionPreconditionStatus.Blocked : ExecutionPreconditionStatus.Warning,
                $"Rollback snapshot availability could not be checked: {ex.Message}",
                ProjectSnapshotSchema.SnapshotRootRelativePath,
                options.RequireSafetySnapshot,
                ClassifyFailure(ex.Message));
        }
    }

    private static void CheckSandboxAllowedRoots(
        string projectRoot,
        ExecutionPreconditionReport report)
    {
        var required = new[]
        {
            ProjectDirectoryV2.CodexWorkspace,
            ProjectDirectoryV2.OutputCurrent,
            ProjectDirectoryV2.Logs
        };

        var failures = new List<string>();
        foreach (var relativeRoot in required)
        {
            var probe = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar), "precondition-check.tmp");
            var validation = SandboxPathPolicy.ValidateCodexWritePath(projectRoot, probe);
            if (!validation.IsAllowed)
            {
                failures.Add($"{relativeRoot}: {validation.Message}");
            }
        }

        AddItem(
            report,
            "sandbox.allowedWriteRoots.verified",
            failures.Count == 0 ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            failures.Count == 0 ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            failures.Count == 0
                ? "Allowed future write roots are inside project sandbox."
                : $"Allowed root validation failed: {string.Join("; ", failures)}",
            ProjectDirectoryV2.CodexWorkspace,
            failures.Count != 0,
            TaskFailureCategory.SandboxViolation.ToString());
    }

    private static void CheckSandboxForbiddenRoots(
        string projectRoot,
        ExecutionPreconditionReport report)
    {
        var sourceRoot = FindSourceRoot();
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var candidates = new List<string>
        {
            Path.Combine(projectRoot, ".git", "config"),
            Path.Combine(projectRoot, ".ssh", "id_rsa"),
            Path.Combine(projectRoot, "WebRebuildRecorder.App", "Program.cs"),
            Path.GetFullPath(Path.Combine(projectRoot, "..", "outside-project", "file.txt"))
        };

        if (!string.IsNullOrWhiteSpace(sourceRoot))
        {
            candidates.Add(Path.Combine(sourceRoot, "WebRebuildRecorder.App", "Core", "ProjectSystem", "ExecutionPrecondition.cs"));
        }

        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            candidates.Add(Path.Combine(systemRoot, "system.ini"));
        }

        var allowedDangerous = candidates
            .Where(candidate => SandboxPathPolicy.ValidateCodexWritePath(projectRoot, candidate).IsAllowed)
            .Select(SafeRelativeDisplay)
            .ToList();

        AddItem(
            report,
            "sandbox.forbiddenRoots.verified",
            allowedDangerous.Count == 0 ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            allowedDangerous.Count == 0 ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            allowedDangerous.Count == 0
                ? "Forbidden roots, source directories, credential directories, system paths, and outside-project paths are blocked."
                : $"Dangerous paths were allowed: {string.Join("; ", allowedDangerous)}",
            ".git",
            allowedDangerous.Count != 0,
            TaskFailureCategory.SandboxViolation.ToString());
    }

    private async Task CheckSecretAndLocalPathsAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        CancellationToken cancellationToken)
    {
        var findings = new List<ExecutionScanFinding>();
        try
        {
            var scan = await secretScanService.ScanProjectAsync(projectRoot, cancellationToken);
            foreach (var finding in scan.Findings.Where(finding =>
                         string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(new ExecutionScanFinding(
                    finding.RelativePath,
                    finding.Key,
                    finding.Message,
                    IsSecretFinding(finding.Key),
                    IsLocalPathFinding(finding.Key)));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            findings.Add(new ExecutionScanFinding(
                string.Empty,
                "scannerError",
                $"Secret/local path scan failed: {ex.Message}",
                true,
                true));
        }

        findings.AddRange(await ScanAdditionalExecutionInputsAsync(projectRoot, cancellationToken));
        var secretFindings = findings.Where(finding => finding.IsSecret).ToList();
        var localPathFindings = findings.Where(finding => finding.IsLocalPath).ToList();

        AddItem(
            report,
            "security.secretScan.clean",
            secretFindings.Count == 0 ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            secretFindings.Count == 0 ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            secretFindings.Count == 0
                ? "No secret markers were found in checked execution inputs."
                : $"Secret markers were found: {DescribeFindings(secretFindings)}",
            CodexTaskPackageSchema.RelativePath,
            secretFindings.Count != 0,
            TaskFailureCategory.SecretDetected.ToString());

        AddItem(
            report,
            "security.localPathScan.clean",
            localPathFindings.Count == 0 ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            localPathFindings.Count == 0 ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            localPathFindings.Count == 0
                ? "No local absolute paths or credential directory markers were found in checked execution inputs."
                : $"Local path markers were found: {DescribeFindings(localPathFindings)}",
            CodexTaskPackageSchema.RelativePath,
            localPathFindings.Count != 0,
            TaskFailureCategory.SecretDetected.ToString());
    }

    private static void CheckOutputSite(
        string projectRoot,
        ExecutionPreconditionReport report,
        bool outputIndexExistedBefore)
    {
        var directory = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProjectDirectoryV2.OutputCurrent,
            "output-site current");
        var writeValidation = SandboxPathPolicy.ValidateCodexWritePath(
            projectRoot,
            Path.Combine(directory, "precondition-check.tmp"));
        var indexPath = Path.Combine(directory, "index.html");
        var indexCreated = File.Exists(indexPath) && !outputIndexExistedBefore;
        var passed = Directory.Exists(directory) && writeValidation.IsAllowed && !indexCreated;

        AddItem(
            report,
            "outputSite.current.safe",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "output-site/current is project-local and this service did not create index.html."
                : "output-site/current is missing, unsafe, or index.html was created during this check.",
            ProjectDirectoryV2.OutputCurrent,
            !passed,
            TaskFailureCategory.SandboxViolation.ToString());
    }

    private static void CheckCodexWorkspace(
        string projectRoot,
        ExecutionPreconditionReport report)
    {
        var directory = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProjectDirectoryV2.CodexWorkspace,
            "codex workspace");
        var writeValidation = SandboxPathPolicy.ValidateCodexWritePath(
            projectRoot,
            Path.Combine(directory, "precondition-check.tmp"));
        var passed = Directory.Exists(directory) && writeValidation.IsAllowed;

        AddItem(
            report,
            "codexWorkspace.safe",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "codex-workspace is project-local and allowed by sandbox policy."
                : $"codex-workspace is missing or unsafe: {writeValidation.Message}",
            ProjectDirectoryV2.CodexWorkspace,
            !passed,
            TaskFailureCategory.SandboxViolation.ToString());
    }

    private static void CheckLogsWritable(
        string projectRoot,
        ExecutionPreconditionReport report)
    {
        var logsDirectory = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProjectDirectoryV2.Logs,
            "logs directory");
        var probePath = Path.Combine(logsDirectory, ".execution-precondition-write-check.tmp");
        var validation = SandboxPathPolicy.ValidateCodexWritePath(projectRoot, probePath);
        var passed = false;
        var message = "logs directory is not writable.";
        if (validation.IsAllowed && Directory.Exists(logsDirectory))
        {
            try
            {
                File.WriteAllText(probePath, "ok", Encoding.UTF8);
                File.Delete(probePath);
                passed = true;
                message = "logs directory is project-local and writable.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                message = $"logs write probe failed: {ex.Message}";
            }
        }
        else
        {
            message = validation.Message;
        }

        AddItem(
            report,
            "logs.writable",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            message,
            ProjectDirectoryV2.Logs,
            !passed,
            TaskFailureCategory.EnvironmentMissing.ToString());
    }

    private static void CheckTaskPackageHashStability(
        string projectRoot,
        ExecutionPreconditionReport report,
        ApprovalGateResult? latestApproval,
        ApprovalGateValidationResult? latestApprovalValidation)
    {
        var taskPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexTaskPackageSchema.RelativePath,
            "task package hash");
        var instructionsPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexTaskPackageSchema.InstructionsRelativePath,
            "instructions hash");
        if (!File.Exists(taskPath) || !File.Exists(instructionsPath))
        {
            AddItem(
                report,
                "taskPackage.hashStable",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                "task-package.json or instructions.md is missing.",
                CodexTaskPackageSchema.RelativePath,
                true,
                TaskFailureCategory.MissingInput.ToString());
            return;
        }

        if (latestApproval is null || latestApprovalValidation is null)
        {
            AddItem(
                report,
                "taskPackage.hashStable",
                ExecutionPreconditionSeverity.Error,
                ExecutionPreconditionStatus.Blocked,
                "No execution approval binding is available for task package hash comparison.",
                CodexTaskPackageSchema.RelativePath,
                true,
                TaskFailureCategory.MissingInput.ToString());
            return;
        }

        var passed = latestApprovalValidation.IsBindingCurrent;
        AddItem(
            report,
            "taskPackage.hashStable",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "Task package and instructions hashes match the latest execution approval binding."
                : "Task package or instructions hash changed after approval binding.",
            ApprovalGateService.GetResultRelativePath(latestApproval.ApprovalId),
            !passed,
            "StaleApproval");
    }

    private static void CheckContextFreshness(
        string projectRoot,
        ExecutionPreconditionReport report,
        ConstructionReadinessResult? readiness)
    {
        var missing = ContextFreshnessRequiredFiles
            .Where(relativePath => !File.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "context freshness")))
            .ToList();
        var contextRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageContextSchema.ContextRootRelativePath,
            "context root");
        if (!Directory.Exists(contextRoot))
        {
            missing.Add(ConstructionPackageContextSchema.ContextRootRelativePath);
        }

        var readinessBlocksContext = readiness?.Items.Any(item =>
            item.BlocksExecution
            && item.Key.Contains("context", StringComparison.OrdinalIgnoreCase)) == true;
        var passed = missing.Count == 0 && !readinessBlocksContext;
        AddItem(
            report,
            "context.freshness.valid",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "Construction context files are present and readiness did not report a context blocker."
                : $"Construction context is missing or stale: {string.Join(", ", missing)}",
            ConstructionPackageContextSchema.ContextRootRelativePath,
            !passed,
            TaskFailureCategory.ValidationError.ToString());
    }

    private static void CheckManualFallback(
        string projectRoot,
        ExecutionPreconditionReport report,
        ExecutionPreconditionOptions options)
    {
        var missingRequired = ManualFallbackRequiredFiles
            .Where(relativePath => !File.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "manual fallback file")))
            .ToList();
        var contextRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageContextSchema.ContextRootRelativePath,
            "manual fallback context");
        if (!Directory.Exists(contextRoot))
        {
            missingRequired.Add(ConstructionPackageContextSchema.ContextRootRelativePath);
        }

        var hasReadiness = File.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionReadinessSchema.ReportMarkdownRelativePath,
            "manual fallback readiness report"));
        var hasDryRun = Directory.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexDryRunSchema.DryRunsRootRelativePath,
            "manual fallback dry-runs"))
                        && Directory.EnumerateFiles(
                            ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, CodexDryRunSchema.DryRunsRootRelativePath, "manual fallback dry-runs"),
                            CodexDryRunSchema.ReportFileName,
                            SearchOption.AllDirectories).Any();

        var passed = missingRequired.Count == 0;
        var warnings = new List<string>();
        if (!hasReadiness)
        {
            warnings.Add("readiness report unavailable");
        }

        if (!hasDryRun)
        {
            warnings.Add("dry-run report unavailable");
        }

        AddItem(
            report,
            "manualFallback.available",
            passed ? (warnings.Count == 0 ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Warning) : ExecutionPreconditionSeverity.Error,
            passed ? (warnings.Count == 0 ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Warning) : ExecutionPreconditionStatus.Blocked,
            passed
                ? warnings.Count == 0
                    ? "Manual construction package fallback inputs are available; no package was exported."
                    : $"Manual fallback core inputs are available, with optional warnings: {string.Join("; ", warnings)}"
                : $"Manual fallback required files are missing: {string.Join(", ", missingRequired)}",
            ProjectDirectoryV2.CodexTask,
            !passed && options.RequireManualFallbackAvailable,
            passed ? null : TaskFailureCategory.MissingInput.ToString());
    }

    private static void CheckNonExecutionBoundary(
        ExecutionPreconditionReport report,
        string outputIndexPath,
        bool outputIndexExistedBefore)
    {
        var indexCreated = File.Exists(outputIndexPath) && !outputIndexExistedBefore;
        var passed = !report.ExecutesCodexCli
                     && !report.CallsOpenAiApi
                     && !report.CallsLocalModel
                     && !report.GeneratesWebsite
                     && !indexCreated;
        AddItem(
            report,
            "nonExecutionBoundary.enforced",
            passed ? ExecutionPreconditionSeverity.Info : ExecutionPreconditionSeverity.Error,
            passed ? ExecutionPreconditionStatus.Passed : ExecutionPreconditionStatus.Blocked,
            passed
                ? "P1.7.3 did not execute Codex CLI, call AI APIs or local models, generate a website, or create output-site/current/index.html."
                : "P1.7.3 non-execution boundary was violated.",
            null,
            !passed,
            TaskFailureCategory.ValidationError.ToString());
    }

    private static async Task<(CodexDryRunResult? Result, string? RelativePath, string? ErrorMessage)> TryLoadLatestDryRunResultAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var dryRunsRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexDryRunSchema.DryRunsRootRelativePath,
            "dry-runs root");
        if (!Directory.Exists(dryRunsRoot))
        {
            return (null, CodexDryRunSchema.DryRunsRootRelativePath, "No P1.6 dry-runs directory was found.");
        }

        var candidates = new List<(CodexDryRunResult Result, string Path)>();
        foreach (var path in Directory.EnumerateFiles(dryRunsRoot, CodexDryRunSchema.ResultFileName, SearchOption.AllDirectories))
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
                    candidates.Add((result, path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        if (candidates.Count == 0)
        {
            return (null, CodexDryRunSchema.DryRunsRootRelativePath, "No readable P1.6 dry-run result was found.");
        }

        var selected = candidates
            .Where(candidate => candidate.Result.IsOk
                                && candidate.Result.IsReadyForFutureExecution
                                && !candidate.Result.ExecutedCodexCli
                                && !candidate.Result.CalledOpenAiApi
                                && !candidate.Result.GeneratedWebsite)
            .OrderByDescending(candidate => candidate.Result.CompletedAt)
            .FirstOrDefault();
        if (selected.Result is null)
        {
            selected = candidates.OrderByDescending(candidate => candidate.Result.CompletedAt).First();
        }

        return (selected.Result, ToProjectRelativePath(projectRoot, selected.Path), null);
    }

    private async Task<(ApprovalGateResult? Result, string? RelativePath, string? ErrorMessage)> TryLoadLatestExecutionApprovalAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        string approvalsRoot;
        try
        {
            approvalsRoot = ApprovalGateService.GetApprovalsRootPath(projectRoot);
        }
        catch (InvalidOperationException ex)
        {
            return (null, ApprovalGateSchema.ApprovalsRootRelativePath, ex.Message);
        }

        if (!Directory.Exists(approvalsRoot))
        {
            return (null, ApprovalGateSchema.ApprovalsRootRelativePath, "No approval root directory was found.");
        }

        var approvals = new List<ApprovalGateResult>();
        foreach (var path in Directory.EnumerateFiles(approvalsRoot, ApprovalGateSchema.ApprovalResultFileName, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(path);
                var result = await JsonSerializer.DeserializeAsync<ApprovalGateResult>(
                    stream,
                    WrbJsonOptions.Default,
                    cancellationToken);
                if (result is not null
                    && result.GateType == ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution)
                {
                    approvals.Add(result);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        var latest = approvals
            .OrderByDescending(approval => approval.UpdatedAt)
            .ThenByDescending(approval => approval.CreatedAt)
            .FirstOrDefault();
        return latest is null
            ? (null, ApprovalGateSchema.ApprovalsRootRelativePath, "No future real execution approval result was found.")
            : (latest, ApprovalGateService.GetResultRelativePath(latest.ApprovalId), null);
    }

    private static async Task<IReadOnlyList<ExecutionScanFinding>> ScanAdditionalExecutionInputsAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var findings = new List<ExecutionScanFinding>();
        foreach (var relativePath in EnumerateAdditionalScanRelativePaths(projectRoot).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, "execution precondition scan");
            if (!validation.IsAllowed || !File.Exists(validation.NormalizedTargetPath))
            {
                continue;
            }

            var fileInfo = new FileInfo(validation.NormalizedTargetPath);
            if (fileInfo.Length > MaxScannedFileBytes)
            {
                continue;
            }

            var content = NormalizeForScanner(await File.ReadAllTextAsync(validation.NormalizedTargetPath, cancellationToken));
            if (SecretPattern.IsMatch(content))
            {
                findings.Add(new ExecutionScanFinding(relativePath, "secretMarker", "Secret-like marker detected.", true, false));
            }

            if (LocalPathPattern.IsMatch(content))
            {
                findings.Add(new ExecutionScanFinding(relativePath, "localPathMarker", "Local absolute path or credential directory marker detected.", false, true));
            }
        }

        return findings;
    }

    private static IEnumerable<string> EnumerateAdditionalScanRelativePaths(string projectRoot)
    {
        foreach (var relativePath in new[]
                 {
                     WrbProjectSchema.FileName,
                     AssetsManifestSchema.RelativePath,
                     ThemeManifestSchema.RelativePath,
                     ContentMapSchema.RelativePath,
                     ObservationPackageSchema.RelativePath,
                     ConstructionPackageSchema.RelativePath,
                     CodexTaskPackageSchema.RelativePath,
                     CodexTaskPackageSchema.InstructionsRelativePath,
                     ConstructionPackageContextSchema.PackageIndexRelativePath,
                     ProofCheckPackageSchema.InstructionsRelativePath,
                 })
        {
            if (File.Exists(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                yield return relativePath;
            }
        }

        foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
        {
            if (File.Exists(Path.Combine(projectRoot, definition.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                yield return definition.RelativePath;
            }
        }

        foreach (var rootRelativePath in new[]
                 {
                     ApprovalGateSchema.ApprovalsRootRelativePath,
                     ProjectDirectoryV2.OutputCurrent
                 })
        {
            var root = Path.Combine(projectRoot, rootRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".js", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return ToProjectRelativePath(projectRoot, path);
            }
        }
    }

    private static ExecutionPreconditionReport CreateReport(DateTimeOffset createdAt)
    {
        var executionId = CreateExecutionId(createdAt);
        return new ExecutionPreconditionReport
        {
            SchemaVersion = ExecutionPreconditionSchema.CurrentSchemaVersion,
            ExecutionId = executionId,
            CreatedAt = createdAt,
            Decision = ExecutionPreconditionDecision.Blocked,
            AllowsRealCodexExecution = false,
            ExecutesCodexCli = false,
            CallsOpenAiApi = false,
            CallsLocalModel = false,
            GeneratesWebsite = false
        };
    }

    private static void FinalizeReport(ExecutionPreconditionReport report)
    {
        report.ExecutesCodexCli = false;
        report.CallsOpenAiApi = false;
        report.CallsLocalModel = false;
        report.GeneratesWebsite = false;

        foreach (var item in report.Items)
        {
            item.Key = SanitizeForArtifact(item.Key);
            item.Message = SanitizeForArtifact(item.Message);
            item.RelativePath = item.RelativePath is null ? null : SanitizeRelativePath(item.RelativePath);
            item.FailureCategory = item.FailureCategory is null ? null : SanitizeForArtifact(item.FailureCategory);
            if (item.Status is ExecutionPreconditionStatus.Blocked or ExecutionPreconditionStatus.NotImplemented)
            {
                item.BlocksExecution = true;
                item.Severity = ExecutionPreconditionSeverity.Error;
                item.FailureCategory ??= TaskFailureCategory.ValidationError.ToString();
            }
        }

        report.BlockingReasons = report.Items
            .Where(item => item.BlocksExecution)
            .Select(item => $"{item.Key}: {item.Message}")
            .Select(SanitizeForArtifact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.Warnings = report.Items
            .Where(item => item.Severity == ExecutionPreconditionSeverity.Warning)
            .Select(item => $"{item.Key}: {item.Message}")
            .Select(SanitizeForArtifact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.Decision = report.BlockingReasons.Count == 0
            ? ExecutionPreconditionDecision.ReadyForFutureProofCheckOnly
            : ExecutionPreconditionDecision.Blocked;
        report.AllowsRealCodexExecution = false;
    }

    private static void AddItem(
        ExecutionPreconditionReport report,
        string key,
        ExecutionPreconditionSeverity severity,
        ExecutionPreconditionStatus status,
        string message,
        string? relativePath,
        bool blocksExecution,
        string? failureCategory)
    {
        report.Items.Add(new ExecutionPreconditionItem
        {
            Key = SanitizeForArtifact(key),
            Severity = severity,
            Status = status,
            Message = SanitizeForArtifact(message),
            RelativePath = relativePath is null ? null : SanitizeRelativePath(relativePath),
            BlocksExecution = blocksExecution,
            FailureCategory = failureCategory is null ? null : SanitizeForArtifact(failureCategory)
        });
    }

    private static async Task SaveReportsAsync(
        string projectRoot,
        ExecutionPreconditionReport report,
        CancellationToken cancellationToken)
    {
        var jsonPath = GetPreconditionsPath(projectRoot, report.ExecutionId);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, report, WrbJsonOptions.Default, cancellationToken);
        }

        var markdownPath = GetPreconditionsMarkdownPath(projectRoot, report.ExecutionId);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdownReport(report), Encoding.UTF8, cancellationToken);
    }

    private static string BuildMarkdownReport(ExecutionPreconditionReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Execution Preconditions Report");
        builder.AppendLine();
        builder.AppendLine($"Decision: `{report.Decision}`");
        builder.AppendLine($"AllowsRealCodexExecution: `{report.AllowsRealCodexExecution.ToString().ToLowerInvariant()}`");
        builder.AppendLine($"CreatedAt: `{report.CreatedAt:O}`");
        builder.AppendLine($"ProjectId: `{EscapeInline(report.ProjectId)}`");
        builder.AppendLine($"ExecutionId: `{EscapeInline(report.ExecutionId)}`");
        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        AppendList(builder, report.BlockingReasons);
        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        AppendList(builder, report.Warnings);
        builder.AppendLine();
        builder.AppendLine("## Precondition Items");
        builder.AppendLine();
        builder.AppendLine("| Key | Status | Severity | BlocksExecution | FailureCategory | RelativePath | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in report.Items)
        {
            builder.AppendLine($"| {EscapeTable(item.Key)} | {item.Status} | {item.Severity} | {item.BlocksExecution.ToString().ToLowerInvariant()} | {EscapeTable(item.FailureCategory ?? string.Empty)} | {EscapeTable(item.RelativePath ?? string.Empty)} | {EscapeTable(item.Message)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## P1.7.3 Boundary");
        builder.AppendLine();
        builder.AppendLine("This report only aggregates execution preconditions.");
        builder.AppendLine("It does not execute Codex CLI, run any codex command, call OpenAI API, call local model engines, generate websites, or write output-site/current/index.html.");
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

    private static string DescribeProofErrors(ProofCheckValidationResult validation)
    {
        return string.Join("; ", validation.Items
            .Where(item => item.BlocksExecution || string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
    }

    private static string DescribeFindings(IReadOnlyList<ExecutionScanFinding> findings)
    {
        return string.Join(
            "; ",
            findings
                .Take(5)
                .Select(finding => $"{SanitizeRelativePath(finding.RelativePath)}:{SanitizeForArtifact(finding.Key)}"));
    }

    private static bool IsSecretFinding(string key)
    {
        return key.Contains("secret", StringComparison.OrdinalIgnoreCase)
               || key.Contains("api", StringComparison.OrdinalIgnoreCase)
               || key.Contains("token", StringComparison.OrdinalIgnoreCase)
               || key.Contains("password", StringComparison.OrdinalIgnoreCase)
               || key.Contains("key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalPathFinding(string key)
    {
        return key.Contains("path", StringComparison.OrdinalIgnoreCase)
               || key.Contains("directory", StringComparison.OrdinalIgnoreCase)
               || key.Contains("ssh", StringComparison.OrdinalIgnoreCase)
               || key.Contains("codex", StringComparison.OrdinalIgnoreCase)
               || key.Contains("openai", StringComparison.OrdinalIgnoreCase);
    }

    private static string ClassifyFailure(string message)
    {
        var text = message ?? string.Empty;
        if (text.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.MissingInput.ToString();
        }

        if (text.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || ProjectPackagePathHelpers.ContainsSecretOrLocalPath(text))
        {
            return TaskFailureCategory.SecretDetected.ToString();
        }

        if (text.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            || text.Contains("absolute", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outside", StringComparison.OrdinalIgnoreCase)
            || text.Contains("..", StringComparison.Ordinal)
            || text.Contains(".git", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.SandboxViolation.ToString();
        }

        if (text.Contains("writable", StringComparison.OrdinalIgnoreCase)
            || text.Contains("environment", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.EnvironmentMissing.ToString();
        }

        return TaskFailureCategory.ValidationError.ToString();
    }

    private static string CreateExecutionId(DateTimeOffset now)
    {
        var raw = $"execution-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(raw.Length, 50)];
    }

    private static string ToProjectRelativePath(string projectRoot, string fullPath)
    {
        return Path.GetRelativePath(projectRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string SafeRelativeDisplay(string path)
    {
        return Path.IsPathRooted(path) ? "<redacted-local-path>" : SanitizeRelativePath(path);
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

    private static string NormalizeForScanner(string content)
    {
        var normalized = content ?? string.Empty;
        normalized = Regex.Replace(normalized, @"(?i)<credential-dir>/\.(ssh|codex|openai)", "<safe-credential-placeholder>");
        normalized = Regex.Replace(normalized, @"(?i)<redacted-credential-dir>", "<safe-credential-placeholder>");
        normalized = Regex.Replace(normalized, @"(?i)<redacted-local-path>", "<safe-local-path-placeholder>");
        normalized = Regex.Replace(normalized, @"(?i)<redacted-sensitive(-marker)?>", "<safe-sensitive-placeholder>");
        normalized = Regex.Replace(normalized, @"(?i)\[redacted\]", "<safe-sensitive-placeholder>");
        return normalized;
    }

    private sealed record ExecutionScanFinding(
        string RelativePath,
        string Key,
        string Message,
        bool IsSecret,
        bool IsLocalPath);
}
