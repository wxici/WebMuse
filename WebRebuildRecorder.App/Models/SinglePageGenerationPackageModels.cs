namespace WebRebuildRecorder.App.Models;

public static class SinglePageGenerationPackageSchema
{
    public const string SchemaVersion = "0.1.0-p2a-2-a-single-page-generation-package";
    public const string RootRelativePath = "codex-task/single-page-generation";
    public const string ManifestFileName = "generation-manifest.json";
    public const string ConstructionBriefFileName = "CONSTRUCTION_BRIEF.md";
    public const string PromptForCodexFileName = "PROMPT_FOR_CODEX.md";
    public const string OutputContractFileName = "OUTPUT_CONTRACT.md";
    public const string ForbiddenContentFileName = "FORBIDDEN_CONTENT.md";
    public const string AssetSlotPlanFileName = "ASSET_SLOT_PLAN.md";
    public const string EvidenceIndexFileName = "EVIDENCE_INDEX.md";
    public const string ReviewChecklistFileName = "REVIEW_CHECKLIST.md";
}

public sealed class SinglePageGenerationPackage
{
    public string SchemaVersion { get; set; } = SinglePageGenerationPackageSchema.SchemaVersion;
    public string PackageId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string SourceSnapshotRoot { get; set; } = string.Empty;
    public string PackageRoot { get; set; } = string.Empty;
    public List<string> InputEvidenceFiles { get; set; } = [];
    public List<string> OutputFiles { get; set; } = [];
    public List<string> FilteredOutItems { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool IsReadyForCodexCli { get; set; }
}
