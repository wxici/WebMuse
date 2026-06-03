namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ProofCheckPackageSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.7.1-proof-package";
    public const string ProofRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/proof";
    public const string ManifestFileName = "proof-manifest.json";
    public const string RequestFileName = "proof-request.json";
    public const string InstructionsFileName = "proof-instructions.md";
    public const string ValidationReportJsonFileName = "proof-package-validation-report.json";
    public const string ValidationReportMarkdownFileName = "proof-package-validation-report.md";

    public const string PlannedResultFileName = "proof-result.json";
    public const string PlannedReportFileName = "proof-report.md";
    public const string PlannedCreatedFileName = "proof-created-file.txt";

    public const string ManifestRelativePath = $"{ProofRootRelativePath}/{ManifestFileName}";
    public const string RequestRelativePath = $"{ProofRootRelativePath}/{RequestFileName}";
    public const string InstructionsRelativePath = $"{ProofRootRelativePath}/{InstructionsFileName}";
    public const string ValidationReportJsonRelativePath = $"{ProofRootRelativePath}/{ValidationReportJsonFileName}";
    public const string ValidationReportMarkdownRelativePath = $"{ProofRootRelativePath}/{ValidationReportMarkdownFileName}";
    public const string PlannedResultRelativePath = $"{ProofRootRelativePath}/{PlannedResultFileName}";
    public const string PlannedReportRelativePath = $"{ProofRootRelativePath}/{PlannedReportFileName}";
    public const string PlannedCreatedFileRelativePath = $"{ProofRootRelativePath}/{PlannedCreatedFileName}";
}

public sealed class ProofCheckPackageManifest
{
    public string SchemaVersion { get; set; } = ProofCheckPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProofPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Mode { get; set; } = "packageOnly";
    public bool ExecutesCodexCli { get; set; }
    public bool CallsOpenAiApi { get; set; }
    public bool GeneratesWebsite { get; set; }

    public string ProofRequestRelativePath { get; set; } = string.Empty;
    public string ProofInstructionsRelativePath { get; set; } = string.Empty;
    public string PlannedProofResultRelativePath { get; set; } = string.Empty;
    public string PlannedProofReportRelativePath { get; set; } = string.Empty;
    public string PlannedCreatedFileRelativePath { get; set; } = string.Empty;

    public List<ProofCheckRequiredCheck> RequiredChecks { get; set; } = [];
    public List<ProofCheckPathTarget> AllowedWriteTargets { get; set; } = [];
    public List<ProofCheckPathTarget> DeniedWriteTargets { get; set; } = [];
    public List<ProofCheckInputReference> InputReferences { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ProofCheckRequest
{
    public string SchemaVersion { get; set; } = ProofCheckPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProofPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ExecutionMode { get; set; } = "futureProofCheckPackageOnly";
    public bool MustNotExecuteInThisRound { get; set; } = true;

    public List<ProofCheckRequiredCheck> RequiredChecks { get; set; } = [];
    public List<ProofCheckPathTarget> AllowedWriteTargets { get; set; } = [];
    public List<ProofCheckPathTarget> DeniedWriteTargets { get; set; } = [];
    public List<ProofCheckInputReference> InputReferences { get; set; } = [];
}

public sealed class ProofCheckResult
{
    public string SchemaVersion { get; set; } = ProofCheckPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProofPackageId { get; set; } = string.Empty;
    public string ProofRunId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public string EngineId { get; set; } = string.Empty;
    public string ExecutorPath { get; set; } = string.Empty;
    public bool IsOk { get; set; }

    public string CreatedFileRelativePath { get; set; } = string.Empty;
    public string CreatedFileSha256 { get; set; } = string.Empty;

    public List<ProofCheckValidationItem> DeniedWriteChecks { get; set; } = [];
    public List<ProofCheckValidationItem> ReadChecks { get; set; } = [];
    public List<string> LogRelativePaths { get; set; } = [];
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ProofCheckReport
{
    public string SchemaVersion { get; set; } = ProofCheckPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProofPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsPackageValid { get; set; }
    public bool ExecutedCodexCli { get; set; }
    public bool CalledOpenAiApi { get; set; }
    public bool GeneratedWebsite { get; set; }

    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<ProofCheckValidationItem> Items { get; set; } = [];
}

public sealed class ProofCheckRequiredCheck
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
}

public sealed class ProofCheckPathTarget
{
    public string RelativePath { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}

public sealed class ProofCheckInputReference
{
    public string RelativePath { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class ProofCheckValidationResult
{
    public bool IsOk { get; set; }
    public string ProofPackageId { get; set; } = string.Empty;
    public List<ProofCheckValidationItem> Items { get; set; } = [];
}

public sealed class ProofCheckValidationItem
{
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public bool BlocksExecution { get; set; }
    public string? FailureCategory { get; set; }
}
