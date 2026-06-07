using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Logging;
using WebRebuildRecorder.App.Core.ProjectSystem;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Core.State;
using WebRebuildRecorder.App.Services;

var failures = new List<string>();
var tempRoot = Path.Combine(
    Path.GetTempPath(),
    "WebRebuildRecorderFoundationSelfTest",
    Guid.NewGuid().ToString("N"));

try
{
    var logger = new AppLogger();
    var projectService = new ProjectService(logger);
    var project = await projectService.CreateNewProjectAsync(new()
    {
        ProjectName = "Foundation Self Check",
        ReferenceUrl = "https://example.com/",
        RootDirectory = tempRoot
    });

    CheckV2Directories(project.ProjectDirectory, failures);
    await CheckManifestAsync(project.ProjectDirectory, failures);
    await CheckStatePreservedAsync(projectService, project.ProjectDirectory, failures);
    await CheckInvalidManifestPathsAsync(project.ProjectDirectory, failures);
    CheckLegacyDoubleWrite(project.ProjectDirectory, failures);
    CheckProjectLock(project.ProjectDirectory, project.ProjectId, failures);
    CheckSandboxPolicy(project.ProjectDirectory, failures);
    CheckReparsePointPolicy(project.ProjectDirectory, failures);
    await CheckEnvironmentStubAsync(project.ProjectDirectory, failures);
    await CheckP1DataFoundationsAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP12PackageScaffoldsAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP13ContentGenerationAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP14SnapshotRestoreAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP15ConstructionReadinessAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP16DryRunOrchestratorAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP171ProofCheckPackageAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP172ApprovalGateAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP173ExecutionPreconditionsAsync(project.ProjectDirectory, project.ProjectId, failures);
    await CheckP180AlphaValidationProbeAsync(project.ProjectDirectory, project.ProjectId, failures);

    if (failures.Count > 0)
    {
        Console.Error.WriteLine("Foundation self-check failed:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    Console.WriteLine("Foundation self-check passed.");
    Console.WriteLine("Verified P0/P1 project manifest, V2 directories, lock, sandbox, reparse, and environment stubs.");
    Console.WriteLine("Verified P1.1 assets/theme/content-map/snapshot/log/export/secret scan foundations.");
    Console.WriteLine("Verified P1.2 observation package, construction package, Codex task package, run records, failure classification, package validation, and no real Codex execution.");
    Console.WriteLine("[P1.3] Legacy observation bridge verified.");
    Console.WriteLine("[P1.3] Construction context builder verified.");
    Console.WriteLine("[P1.3] Task run queued transition verified.");
    Console.WriteLine("[P1.3] Strict package validation verified.");
    Console.WriteLine("[P1.4] Snapshot listing verified.");
    Console.WriteLine("[P1.4] Snapshot validation verified.");
    Console.WriteLine("[P1.4] Hash mismatch detection verified.");
    Console.WriteLine("[P1.4] Safety snapshot before restore verified.");
    Console.WriteLine("[P1.4] Restore result report verified.");
    Console.WriteLine("[P1.4] Forbidden restore paths blocked.");
    Console.WriteLine("[P1.5] Draft readiness verified.");
    Console.WriteLine("[P1.5] Strict readiness verified.");
    Console.WriteLine("[P1.5] PreCodexDryRun readiness verified.");
    Console.WriteLine("[P1.5] Readiness report files verified.");
    Console.WriteLine("[P1.5] Package-index hash mismatch blocked.");
    Console.WriteLine("[P1.5] Secret/local path blocked.");
    Console.WriteLine("[P1.5] Rollback availability verified.");
    Console.WriteLine("[P1.5] Context freshness warning/error verified.");
    Console.WriteLine("[P1.5] Readiness-probe snapshot semantics verified.");
    Console.WriteLine("[P1.5] Output surface writable check verified without requiring generated index.html.");
    Console.WriteLine("[P1.5] AI engine missing is warning, not blocker.");
    Console.WriteLine("[P1.5] Reference-site asset approval is blocked.");
    Console.WriteLine("[P1.5] Optional design-context awareness verified.");
    Console.WriteLine("[P1.5] Readiness report sanitizer verified.");
    Console.WriteLine("[P1.5] Blocking reasons map to failure categories.");
    Console.WriteLine("[P1.6] Dry-run orchestrator verified.");
    Console.WriteLine("[P1.6] PreCodexDryRun readiness integration verified.");
    Console.WriteLine("[P1.6] Task package missing blocks dry-run.");
    Console.WriteLine("[P1.6] Instructions safety boundary check verified.");
    Console.WriteLine("[P1.6] Allowed write roots check verified.");
    Console.WriteLine("[P1.6] Dry-run reports verified.");
    Console.WriteLine("[P1.6] Non-execution flags verified.");
    Console.WriteLine("[P1.6] Output-site untouched verified.");
    Console.WriteLine("[P1.6] Report sanitizer verified.");
    Console.WriteLine("[P1.6] Dry-run logs verified.");
    Console.WriteLine("[P1.7.1] Proof-check package models verified.");
    Console.WriteLine("[P1.7.1] Proof-check package persistence verified.");
    Console.WriteLine("[P1.7.1] Proof-check package validation verified.");
    Console.WriteLine("[P1.7.1] Proof-check path safety verified.");
    Console.WriteLine("[P1.7.1] Proof-check non-execution boundary verified.");
    Console.WriteLine("[P1.7.2] Approval gate models verified.");
    Console.WriteLine("[P1.7.2] Approval gate persistence verified.");
    Console.WriteLine("[P1.7.2] Approval binding hash validation verified.");
    Console.WriteLine("[P1.7.2] Approval state transitions verified.");
    Console.WriteLine("[P1.7.2] Approval non-execution boundary verified.");
    Console.WriteLine("[P1.7.3] Execution precondition models verified.");
    Console.WriteLine("[P1.7.3] Execution precondition persistence verified.");
    Console.WriteLine("[P1.7.3] Readiness/dry-run/proof/approval aggregation verified.");
    Console.WriteLine("[P1.7.3] Blocking preconditions verified.");
    Console.WriteLine("[P1.7.3] Execution precondition non-execution boundary verified.");
    Console.WriteLine("[P1.8-0] Alpha validation probe models verified.");
    Console.WriteLine("[P1.8-0] Alpha validation probe persistence verified.");
    Console.WriteLine("[P1.8-0] Local pipeline probe steps verified.");
    Console.WriteLine("[P1.8-0] Blocked-but-explainable alpha evidence verified.");
    Console.WriteLine("[P1.8-0] Alpha validation non-execution boundary verified.");
    Console.WriteLine($"Temporary project: {project.ProjectDirectory}");
    return 0;
}
finally
{
    TryDeleteTempRoot(tempRoot);
}

static void CheckV2Directories(string projectRoot, List<string> failures)
{
    var required = new[]
    {
        ProjectDirectoryV2.Input,
        ProjectDirectoryV2.Assets,
        ProjectDirectoryV2.Theme,
        ProjectDirectoryV2.Observation,
        ProjectDirectoryV2.CodexTask,
        ProjectDirectoryV2.OutputCurrent,
        ProjectDirectoryV2.Tune,
        ProjectDirectoryV2.Maps,
        ProjectDirectoryV2.Exports,
        ProjectDirectoryV2.Logs,
        ProjectDirectoryV2.Versions
    };

    foreach (var relativePath in required)
    {
        var fullPath = Path.Combine(projectRoot, relativePath);
        if (!Directory.Exists(fullPath))
        {
            failures.Add($"Missing V2 directory: {relativePath}");
        }
    }
}

static async Task CheckManifestAsync(string projectRoot, List<string> failures)
{
    var manifestPath = ProjectManifestService.GetManifestPath(projectRoot);
    if (!File.Exists(manifestPath))
    {
        failures.Add("project.wrbproj was not created.");
        return;
    }

    await using var stream = File.OpenRead(manifestPath);
    using var document = await JsonDocument.ParseAsync(stream);
    if (!document.RootElement.TryGetProperty("state", out var stateElement)
        || stateElement.ValueKind != JsonValueKind.String)
    {
        failures.Add("project.wrbproj state is not serialized as a string.");
    }

    var manifest = await new ProjectManifestService().LoadAsync(projectRoot);
    if (string.IsNullOrWhiteSpace(manifest.ProjectId))
    {
        failures.Add("project.wrbproj has no projectId.");
    }

    if (Path.IsPathRooted(manifest.Paths.OutputCurrent))
    {
        failures.Add("project.wrbproj contains an absolute outputCurrent path.");
    }
}

static async Task CheckStatePreservedAsync(
    ProjectService projectService,
    string projectRoot,
    List<string> failures)
{
    var manifestService = new ProjectManifestService();
    await manifestService.SetProjectStateAsync(projectRoot, ProjectState.ObservationReady);

    await projectService.OpenProjectAsync(projectRoot);
    await projectService.SaveCurrentProjectAsync();

    var reloaded = await manifestService.LoadAsync(projectRoot);
    if (reloaded.State != ProjectState.ObservationReady)
    {
        failures.Add($"ProjectState was reset during save. Expected ObservationReady, got {reloaded.State}.");
    }
}

static async Task CheckInvalidManifestPathsAsync(string projectRoot, List<string> failures)
{
    var manifestService = new ProjectManifestService();
    var originalManifest = await manifestService.LoadAsync(projectRoot);
    var manifestPath = ProjectManifestService.GetManifestPath(projectRoot);

    var traversalManifest = CloneManifest(originalManifest);
    traversalManifest.Paths.OutputCurrent = "../other-project";
    await WriteRawManifestAsync(manifestPath, traversalManifest);
    await ExpectInvalidManifestAsync(
        manifestService,
        projectRoot,
        "ProjectManifestService allowed outputCurrent='../other-project'.",
        failures);

    var absoluteManifest = CloneManifest(originalManifest);
    absoluteManifest.Paths.Logs = @"C:/Users/test/.ssh";
    await WriteRawManifestAsync(manifestPath, absoluteManifest);
    await ExpectInvalidManifestAsync(
        manifestService,
        projectRoot,
        "ProjectManifestService allowed logs='C:/Users/test/.ssh'.",
        failures);

    await manifestService.SaveAsync(projectRoot, originalManifest);
}

static WrbProjectManifest CloneManifest(WrbProjectManifest manifest)
{
    var json = JsonSerializer.Serialize(manifest, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<WrbProjectManifest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone manifest for self-check.");
}

static async Task WriteRawManifestAsync(string manifestPath, WrbProjectManifest manifest)
{
    await using var stream = File.Create(manifestPath);
    await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default);
}

static async Task ExpectInvalidManifestAsync(
    ProjectManifestService manifestService,
    string projectRoot,
    string failureMessage,
    List<string> failures)
{
    try
    {
        await manifestService.LoadAsync(projectRoot);
        failures.Add(failureMessage);
    }
    catch (InvalidDataException)
    {
    }
    catch (InvalidOperationException)
    {
    }
}

static void CheckLegacyDoubleWrite(string projectRoot, List<string> failures)
{
    if (!File.Exists(Path.Combine(projectRoot, "project.json")))
    {
        failures.Add("Legacy project.json was not written.");
    }

    if (!File.Exists(Path.Combine(projectRoot, "project-info.json")))
    {
        failures.Add("Legacy project-info.json was not written.");
    }
}

static void CheckProjectLock(string projectRoot, string projectId, List<string> failures)
{
    var lockService = new ProjectLockService();
    var firstLock = lockService.CreateLock(projectRoot, "self-check", projectId, "Foundation harness check");
    if (!lockService.IsLocked(projectRoot))
    {
        failures.Add("ProjectLockService did not report the first lock.");
    }

    try
    {
        lockService.CreateLock(projectRoot, "duplicate", projectId, "Duplicate check");
        failures.Add("ProjectLockService allowed duplicate locking.");
    }
    catch (InvalidOperationException)
    {
    }

    var lockPath = ProjectLockService.GetLockFilePath(projectRoot);
    using (var document = JsonDocument.Parse(File.ReadAllText(lockPath)))
    {
        if (!document.RootElement.TryGetProperty("processId", out _)
            || !document.RootElement.TryGetProperty("machineName", out _)
            || !document.RootElement.TryGetProperty("projectId", out _))
        {
            failures.Add("project.lock JSON is missing expected fields.");
        }
    }

    if (string.IsNullOrWhiteSpace(firstLock.TaskId))
    {
        failures.Add("ProjectLockService returned an empty taskId.");
    }

    lockService.ReleaseLock(projectRoot);
    if (lockService.IsLocked(projectRoot))
    {
        failures.Add("ProjectLockService still reports locked after release.");
    }

    lockService.CreateLock(projectRoot, "self-check-reacquire", projectId, "Reacquire check");
    lockService.ReleaseLock(projectRoot);
}

static void CheckSandboxPolicy(string projectRoot, List<string> failures)
{
    var allowed = new[]
    {
        Path.Combine(projectRoot, "output-site", "current", "index.html"),
        Path.Combine(projectRoot, "logs", "run.log"),
        Path.Combine(projectRoot, "theme", "theme.generated.css")
    };

    foreach (var path in allowed)
    {
        if (!SandboxPathPolicy.ValidateCodexWritePath(projectRoot, path).IsAllowed)
        {
            failures.Add($"SandboxPathPolicy rejected an allowed path: {path}");
        }
    }

    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    var forbidden = new List<string>
    {
        Path.Combine(projectRoot, "..", "other-project", "file.txt"),
        Path.Combine(projectRoot, ".git", "config")
    };

    if (!string.IsNullOrWhiteSpace(windows))
    {
        forbidden.Add(Path.Combine(windows, "notepad.exe"));
    }

    if (!string.IsNullOrWhiteSpace(userProfile))
    {
        forbidden.Add(Path.Combine(userProfile, ".ssh", "id_rsa"));
        forbidden.Add(Path.Combine(userProfile, ".codex", "config.json"));
    }

    var sourceRoot = FindSourceRoot();
    if (!string.IsNullOrWhiteSpace(sourceRoot))
    {
        forbidden.Add(Path.Combine(sourceRoot, "PROJECT_STATUS.md"));
    }

    forbidden.Add(Path.Combine(AppContext.BaseDirectory, "codex-workspace", "should-not-write.txt"));

    foreach (var path in forbidden)
    {
        if (SandboxPathPolicy.ValidateCodexWritePath(projectRoot, path).IsAllowed)
        {
            failures.Add($"SandboxPathPolicy allowed a forbidden path: {path}");
        }
    }

    var appBaseProjectRoot = Path.Combine(AppContext.BaseDirectory, "project-root-under-app-base");
    if (SandboxPathPolicy.ValidateProjectRoot(appBaseProjectRoot).IsAllowed)
    {
        failures.Add($"SandboxPathPolicy allowed a project root under AppContext.BaseDirectory: {appBaseProjectRoot}");
    }
}

static void CheckReparsePointPolicy(string projectRoot, List<string> failures)
{
    if (!OperatingSystem.IsWindows())
    {
        Console.WriteLine("ReparsePoint self-check skipped: non-Windows runtime.");
        return;
    }

    var externalTarget = Path.Combine(projectRoot, "runtime", "reparse-target");
    var linkPath = Path.Combine(projectRoot, ProjectDirectoryV2.CodexWorkspace, "reparse-link");
    Directory.CreateDirectory(externalTarget);

    try
    {
        Directory.CreateSymbolicLink(linkPath, externalTarget);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
    {
        Console.WriteLine($"ReparsePoint self-check skipped: could not create a local symbolic link ({ex.GetType().Name}). Policy code rejects existing ReparsePoint directories.");
        return;
    }

    var attributes = File.GetAttributes(linkPath);
    if ((attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
    {
        failures.Add("ReparsePoint self-check could not create a detectable reparse point.");
        return;
    }

    var linkedWritePath = Path.Combine(linkPath, "escape.txt");
    if (SandboxPathPolicy.ValidateCodexWritePath(projectRoot, linkedWritePath).IsAllowed)
    {
        failures.Add("SandboxPathPolicy allowed a Codex write path through a reparse point.");
    }
}

static async Task CheckEnvironmentStubAsync(string projectRoot, List<string> failures)
{
    var result = await new EnvironmentCheckService().CheckAsync(projectRoot);
    if (result.Items.Count == 0)
    {
        failures.Add("EnvironmentCheckService returned no items.");
        return;
    }

    var json = JsonSerializer.Serialize(result, WrbJsonOptions.Default);
    if (!json.Contains("\"status\": \"skipped\"", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("EnvironmentCheckService status enum did not serialize as a string.");
    }
}

static async Task CheckP1DataFoundationsAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    await CheckExportIntegrityMissingAsync(projectRoot, failures);
    await CheckAssetsManifestAsync(projectRoot, projectId, failures);
    await CheckThemeManifestAsync(projectRoot, projectId, failures);
    await CheckContentMapAsync(projectRoot, projectId, failures);
    await CheckLayeredLogsAsync(projectRoot, failures);
    await CheckExportIntegrityCleanAsync(projectRoot, failures);
    await CheckSecretAndLocalPathScanAsync(projectRoot, failures);
    await CheckSnapshotScaffoldAsync(projectRoot, failures);
}

static async Task CheckAssetsManifestAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new AssetsManifestService();
    var manifest = await service.CreateNewAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, manifest);

    var assetRelativePath = "input/assets/test-logo.txt";
    var assetPath = Path.Combine(projectRoot, assetRelativePath);
    await File.WriteAllTextAsync(assetPath, "test asset", Encoding.UTF8);
    await service.AddOrUpdateItemAsync(
        projectRoot,
        new AssetManifestItem
        {
            AssetId = "asset-test-logo",
            Kind = "image",
            Role = "brandLogo",
            RelativePath = assetRelativePath,
            OriginalFileName = @"C:\Users\test\logo.txt",
            SourceType = "userUpload",
            IsUserProvided = true,
            IsApprovedForExport = true,
            Tags = ["brand", "test"]
        });

    var reloaded = await service.LoadAsync(projectRoot);
    var item = reloaded.Assets.SingleOrDefault(asset => asset.AssetId == "asset-test-logo");
    if (item is null)
    {
        failures.Add("AssetsManifestService did not persist the test asset.");
        return;
    }

    if (Path.IsPathRooted(item.RelativePath) || item.RelativePath.Contains("..", StringComparison.Ordinal))
    {
        failures.Add($"Assets manifest stored an unsafe path: {item.RelativePath}");
    }

    if (item.OriginalFileName != "logo.txt")
    {
        failures.Add("Assets manifest did not reduce originalFileName to a safe file name.");
    }

    if (string.IsNullOrWhiteSpace(item.Sha256) || item.SizeBytes is null or <= 0)
    {
        failures.Add("Assets manifest did not populate hash/size for the test asset.");
    }

    var json = await File.ReadAllTextAsync(AssetsManifestService.GetManifestPath(projectRoot));
    if (!json.Contains("\"sourceType\": \"userUpload\"", StringComparison.Ordinal))
    {
        failures.Add("Assets manifest did not write stable string sourceType text.");
    }
}

static async Task CheckThemeManifestAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new ThemeManifestService();
    var theme = await service.CreateDefaultAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, theme);

    var reloaded = await service.LoadAsync(projectRoot);
    if (reloaded.CurrentPalette.Colors.Count == 0)
    {
        failures.Add("ThemeManifestService did not persist default colors.");
    }

    if (!string.Equals(
            reloaded.CurrentPalette.Colors.First().Hex,
            "#FFFFFF",
            StringComparison.Ordinal))
    {
        failures.Add("ThemeManifestService did not preserve normalized #RRGGBB colors.");
    }

    var invalid = CloneThemeManifest(reloaded);
    invalid.CurrentPalette.Colors[0].Hex = "not-a-color";
    try
    {
        await service.SaveAsync(projectRoot, invalid);
        failures.Add("ThemeManifestService allowed an invalid hex color.");
    }
    catch (InvalidOperationException)
    {
    }

    await service.SaveAsync(projectRoot, reloaded);
}

static ThemeManifest CloneThemeManifest(ThemeManifest manifest)
{
    var json = JsonSerializer.Serialize(manifest, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<ThemeManifest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone theme manifest.");
}

static async Task CheckContentMapAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new ContentMapService();
    var contentMap = await service.CreateDefaultAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, contentMap);

    var reloaded = await service.LoadAsync(projectRoot);
    if (reloaded.Pages.Count == 0
        || reloaded.Pages[0].Sections.Count == 0
        || reloaded.Pages[0].Sections[0].Elements.Count == 0)
    {
        failures.Add("ContentMapService default map does not include a page, section, and element.");
        return;
    }

    var tuneIds = reloaded.Pages
        .SelectMany(page => page.Sections)
        .Select(section => section.DataTuneId)
        .Concat(reloaded.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Elements).Select(element => element.DataTuneId))
        .ToList();
    if (tuneIds.Any(string.IsNullOrWhiteSpace))
    {
        failures.Add("Content map contains an empty DataTuneId.");
    }

    if (tuneIds.Count != tuneIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
    {
        failures.Add("Content map contains duplicate DataTuneId values.");
    }

    var duplicate = CloneContentMap(reloaded);
    duplicate.Pages[0].Sections[0].Elements[0].DataTuneId = duplicate.Pages[0].Sections[0].DataTuneId;
    try
    {
        await service.SaveAsync(projectRoot, duplicate);
        failures.Add("ContentMapService allowed duplicate DataTuneId values.");
    }
    catch (InvalidOperationException)
    {
    }

    await service.SaveAsync(projectRoot, reloaded);
}

static ContentMap CloneContentMap(ContentMap contentMap)
{
    var json = JsonSerializer.Serialize(contentMap, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<ContentMap>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone content map.");
}

static async Task CheckLayeredLogsAsync(string projectRoot, List<string> failures)
{
    var service = new ProjectLogService();
    await service.WriteAsync(projectRoot, "app", "app message", ProjectLogLevel.Info);
    await service.WriteAsync(projectRoot, "project", "project message", ProjectLogLevel.Warning);
    await service.WriteAsync(projectRoot, "security", "token=abc123", ProjectLogLevel.Error);

    foreach (var fileName in new[] { "app.log", "project.log", "security.log" })
    {
        var path = Path.Combine(projectRoot, ProjectDirectoryV2.Logs, fileName);
        if (!File.Exists(path))
        {
            failures.Add($"ProjectLogService did not create {fileName}.");
            continue;
        }

        var content = await File.ReadAllTextAsync(path);
        if (!content.Contains("\"level\":", StringComparison.Ordinal)
            || !content.Contains("\"channel\":", StringComparison.Ordinal))
        {
            failures.Add($"ProjectLogService wrote malformed log content in {fileName}.");
        }

        if (fileName == "security.log" && content.Contains("abc123", StringComparison.Ordinal))
        {
            failures.Add("ProjectLogService did not redact a token-like log message.");
        }

        if (!SandboxPathPolicy.ValidateProjectPath(projectRoot, path).IsAllowed)
        {
            failures.Add($"ProjectLogService wrote outside sandbox: {path}");
        }
    }
}

static async Task CheckExportIntegrityMissingAsync(string projectRoot, List<string> failures)
{
    var result = await new ExportIntegrityCheckService().CheckAsync(projectRoot);
    if (result.IsOk)
    {
        failures.Add("ExportIntegrityCheckService reported OK before P1 manifest files existed.");
    }

    if (!result.Items.Any(item =>
            string.Equals(item.Key, "themeManifest", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("ExportIntegrityCheckService did not report missing theme/theme.json.");
    }
}

static async Task CheckExportIntegrityCleanAsync(string projectRoot, List<string> failures)
{
    var outputIndexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    await File.WriteAllTextAsync(outputIndexPath, "<!doctype html><title>Self check</title>", Encoding.UTF8);

    var result = await new ExportIntegrityCheckService().CheckAsync(projectRoot);
    if (!result.IsOk)
    {
        var errors = string.Join(", ", result.Items
            .Where(item => string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Key}:{item.RelativePath}"));
        failures.Add($"ExportIntegrityCheckService reported errors after required P1 files existed: {errors}");
    }
}

static async Task CheckSecretAndLocalPathScanAsync(string projectRoot, List<string> failures)
{
    var testPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "secret-test.js");
    await File.WriteAllTextAsync(
        testPath,
        "const key = \"sk-test\"; const localPath = \"C:\\\\Users\\\\test\";",
        Encoding.UTF8);

    var result = await new SecretAndLocalPathScanService().ScanProjectAsync(projectRoot);
    if (!result.Findings.Any(finding => finding.Key == "openaiSecretKey"))
    {
        failures.Add("SecretAndLocalPathScanService did not detect sk-test.");
    }

    if (!result.Findings.Any(finding => finding.Key == "windowsUsersPath"))
    {
        failures.Add("SecretAndLocalPathScanService did not detect C:\\Users\\test.");
    }

    File.Delete(testPath);
}

static async Task CheckSnapshotScaffoldAsync(string projectRoot, List<string> failures)
{
    var blockedDirectory = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "bin");
    Directory.CreateDirectory(blockedDirectory);
    await File.WriteAllTextAsync(Path.Combine(blockedDirectory, "skip.dll"), "do not copy", Encoding.UTF8);

    var manifest = await new ProjectSnapshotService().CreateSnapshotAsync(projectRoot, "self-test snapshot");
    if (string.IsNullOrWhiteSpace(manifest.SnapshotId))
    {
        failures.Add("ProjectSnapshotService returned an empty snapshotId.");
        return;
    }

    var snapshotRoot = Path.Combine(projectRoot, ProjectSnapshotSchema.SnapshotRootRelativePath, manifest.SnapshotId);
    var manifestPath = Path.Combine(snapshotRoot, ProjectSnapshotSchema.ManifestFileName);
    if (!File.Exists(manifestPath))
    {
        failures.Add("ProjectSnapshotService did not write snapshot-manifest.json.");
    }

    if (manifest.Files.Count == 0 || manifest.Files.Any(file => string.IsNullOrWhiteSpace(file.Sha256)))
    {
        failures.Add("ProjectSnapshotService did not record copied files with hashes.");
    }

    if (File.Exists(Path.Combine(snapshotRoot, "output-site", "bin", "skip.dll")))
    {
        failures.Add("ProjectSnapshotService copied a blocked bin/ file into the snapshot.");
    }
}

static async Task CheckP12PackageScaffoldsAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var observation = await CheckObservationPackageAsync(projectRoot, projectId, failures);
    var construction = await CheckConstructionPackageAsync(projectRoot, projectId, failures);
    var taskPackage = await CheckCodexTaskPackageAsync(projectRoot, projectId, failures);
    await CheckCodexTaskRunRecordAsync(projectRoot, projectId, taskPackage.TaskPackageId, failures);
    CheckFailureClassification(failures);
    await CheckPackageValidationAsync(projectRoot, observation, taskPackage, failures);

    if (!string.Equals(construction.ProjectId, projectId, StringComparison.Ordinal))
    {
        failures.Add("Construction package did not preserve the project id.");
    }
}

static async Task<ObservationPackageManifest> CheckObservationPackageAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new ObservationPackageService();
    var manifest = await service.CreateNewAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, manifest);

    var emptyReloaded = await service.LoadAsync(projectRoot);
    if (emptyReloaded.Warnings.Count == 0)
    {
        failures.Add("ObservationPackageService did not warn when no legacy observation artifacts were present.");
    }

    var artifactRelativePath = "observation/screenshots/p1-2-artifact.txt";
    var artifactFullPath = Path.Combine(projectRoot, artifactRelativePath);
    await File.WriteAllTextAsync(artifactFullPath, "observation artifact", Encoding.UTF8);

    await service.AddArtifactAsync(
        projectRoot,
        new ObservationArtifactItem
        {
            ArtifactId = "artifact-p1-2",
            Kind = "text",
            RelativePath = artifactRelativePath,
            Note = "Self-test observation artifact."
        });
    await service.AddSectionAsync(
        projectRoot,
        new ObservationSectionItem
        {
            SectionId = "hero",
            DisplayName = "Hero",
            SectionType = "hero",
            VisualIntent = "Large first viewport statement.",
            RelatedArtifactIds = ["artifact-p1-2"]
        });
    await service.AddInteractionAsync(
        projectRoot,
        new ObservationInteractionItem
        {
            InteractionId = "hover-cta",
            TargetHint = "primary CTA",
            Trigger = "hover",
            ObservedEffect = "Button contrast increases.",
            Confidence = "medium"
        });
    await service.AddFindingAsync(
        projectRoot,
        new ObservationFindingItem
        {
            FindingId = "finding-rhythm",
            Category = "layout",
            Summary = "Hero section carries the page rhythm.",
            Detail = "Scaffold finding for package validation.",
            Confidence = "medium"
        });

    var reloaded = await service.LoadAsync(projectRoot);
    if (reloaded.Artifacts.Count == 0
        || reloaded.Sections.Count == 0
        || reloaded.Interactions.Count == 0
        || reloaded.Findings.Count == 0)
    {
        failures.Add("ObservationPackageService did not persist artifact/section/interaction/finding items.");
    }

    var artifact = reloaded.Artifacts.SingleOrDefault(item => item.ArtifactId == "artifact-p1-2");
    if (artifact is null || string.IsNullOrWhiteSpace(artifact.Sha256) || artifact.SizeBytes is null or <= 0)
    {
        failures.Add("Observation package artifact did not record hash/size.");
    }

    try
    {
        await service.AddArtifactAsync(
            projectRoot,
            new ObservationArtifactItem
            {
                ArtifactId = "bad-absolute-path",
                Kind = "text",
                RelativePath = @"C:\Users\test\artifact.txt"
            });
        failures.Add("ObservationPackageService allowed an absolute artifact path.");
    }
    catch (InvalidOperationException)
    {
    }

    return reloaded;
}

static async Task<ConstructionPackageManifest> CheckConstructionPackageAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new ConstructionPackageService();
    var manifest = await service.CreateNewAsync(projectRoot, projectId, "Foundation Brand", "Prepare a static scaffold.");
    await service.SaveAsync(projectRoot, manifest);

    var reloaded = await service.LoadAsync(projectRoot);
    var required = new[]
    {
        WrbProjectSchema.FileName,
        ObservationPackageSchema.RelativePath,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath
    };

    foreach (var relativePath in required)
    {
        if (!reloaded.RequiredProjectFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"Construction package missing required file reference: {relativePath}");
        }
    }

    if (reloaded.RequiredProjectFiles.Any(Path.IsPathRooted)
        || reloaded.Inputs.Any(input => Path.IsPathRooted(input.RelativePath))
        || reloaded.Deliverables.Any(output => Path.IsPathRooted(output.TargetRelativePath)))
    {
        failures.Add("Construction package stored an absolute path.");
    }

    var missingRoot = Path.Combine(projectRoot, ProjectDirectoryV2.Runtime, "missing-construction-package-check");
    Directory.CreateDirectory(missingRoot);
    var missingManifest = await service.CreateNewAsync(missingRoot, "missing-project");
    if (missingManifest.Warnings.Count == 0)
    {
        failures.Add("ConstructionPackageService did not warn when required files were missing.");
    }

    return reloaded;
}

static async Task<CodexTaskPackage> CheckCodexTaskPackageAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new CodexTaskPackageService();
    var package = await service.CreateNewAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, package);
    var instructionsPath = await service.WriteInstructionsAsync(projectRoot, package);

    if (!File.Exists(instructionsPath))
    {
        failures.Add("CodexTaskPackageService did not write instructions.md.");
    }

    var reloaded = await service.LoadAsync(projectRoot);
    if (reloaded.Sandbox.AllowedWriteRoots.Count == 0)
    {
        failures.Add("Codex task package has no allowed write roots.");
    }

    if (!reloaded.Sandbox.ForbiddenRoots.Any(root => root.Contains(".git", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("Codex task package does not record .git as forbidden.");
    }

    if (reloaded.ExpectedOutputs.All(output =>
            !output.RelativePath.StartsWith(ProjectDirectoryV2.OutputCurrent, StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("Codex task package does not target output-site/current/.");
    }

    try
    {
        var invalid = CloneTaskPackage(reloaded);
        invalid.Sandbox.AllowedWriteRoots = [@"C:\Users\test"];
        await service.SaveAsync(projectRoot, invalid);
        failures.Add("CodexTaskPackageService allowed an absolute allowed-write root.");
    }
    catch (InvalidOperationException)
    {
    }

    await service.SaveAsync(projectRoot, reloaded);
    await service.WriteInstructionsAsync(projectRoot, reloaded);
    return reloaded;
}

static async Task CheckCodexTaskRunRecordAsync(
    string projectRoot,
    string projectId,
    string taskPackageId,
    List<string> failures)
{
    var service = new CodexTaskRunService();
    var created = await service.CreateRunAsync(projectRoot, projectId, taskPackageId);
    if (created.Status != CodexTaskRunStatus.Created)
    {
        failures.Add("CodexTaskRunService did not create a Created run.");
    }

    var running = await service.MarkRunningAsync(projectRoot, created.RunId);
    if (running.Status != CodexTaskRunStatus.Running || running.StartedAt is null)
    {
        failures.Add("CodexTaskRunService did not mark the run as Running.");
    }

    var succeeded = await service.MarkSucceededAsync(
        projectRoot,
        created.RunId,
        [ProjectDirectoryV2.OutputCurrent + "/index.html"]);
    if (succeeded.Status != CodexTaskRunStatus.Succeeded || succeeded.CompletedAt is null)
    {
        failures.Add("CodexTaskRunService did not mark the run as Succeeded.");
    }

    var runRecordJson = await File.ReadAllTextAsync(CodexTaskRunService.GetRunRecordPath(projectRoot, created.RunId));
    using (var document = JsonDocument.Parse(runRecordJson))
    {
        if (!document.RootElement.TryGetProperty("status", out var statusElement)
            || statusElement.ValueKind != JsonValueKind.String
            || !string.Equals(statusElement.GetString(), "succeeded", StringComparison.Ordinal))
        {
            failures.Add("Codex task run status was not saved as a JSON string enum.");
        }
    }

    try
    {
        await service.MarkRunningAsync(projectRoot, created.RunId);
        failures.Add("CodexTaskRunService allowed a terminal status to transition back to Running.");
    }
    catch (InvalidOperationException)
    {
    }
}

static void CheckFailureClassification(List<string> failures)
{
    var classifier = new TaskFailureClassifier();
    if (classifier.Classify("sandbox violation outside project").Category != TaskFailureCategory.SandboxViolation)
    {
        failures.Add("TaskFailureClassifier did not classify sandbox violations.");
    }

    if (classifier.Classify("missing input required file").Category != TaskFailureCategory.MissingInput)
    {
        failures.Add("TaskFailureClassifier did not classify missing input.");
    }

    if (classifier.Classify("secret token detected").Category != TaskFailureCategory.SecretDetected)
    {
        failures.Add("TaskFailureClassifier did not classify secret detection.");
    }

    var json = JsonSerializer.Serialize(
        new TaskFailureClassification
        {
            Category = TaskFailureCategory.SandboxViolation,
            Message = "sandbox"
        },
        WrbJsonOptions.Default);
    if (!json.Contains("\"category\": \"sandboxViolation\"", StringComparison.Ordinal))
    {
        failures.Add("TaskFailureCategory did not serialize as a JSON string enum.");
    }
}

static async Task CheckPackageValidationAsync(
    string projectRoot,
    ObservationPackageManifest observation,
    CodexTaskPackage taskPackage,
    List<string> failures)
{
    var service = new PackageValidationService();
    var observationResult = await service.ValidateObservationPackageAsync(projectRoot);
    if (!observationResult.IsOk)
    {
        failures.Add($"PackageValidationService did not accept the normal observation package: {DescribeValidationErrors(observationResult)}");
    }

    var constructionResult = await service.ValidateConstructionPackageAsync(projectRoot);
    if (!constructionResult.IsOk)
    {
        failures.Add($"PackageValidationService did not accept the normal construction package: {DescribeValidationErrors(constructionResult)}");
    }

    var taskResult = await service.ValidateCodexTaskPackageAsync(projectRoot);
    if (!taskResult.IsOk)
    {
        failures.Add($"PackageValidationService did not accept the normal Codex task package: {DescribeValidationErrors(taskResult)}");
    }

    var instructionsPath = CodexTaskPackageService.GetInstructionsPath(projectRoot);
    var instructionsContent = await File.ReadAllTextAsync(instructionsPath);
    File.Delete(instructionsPath);
    var missingInstructions = await service.ValidateCodexTaskPackageAsync(projectRoot);
    if (missingInstructions.IsOk
        || !missingInstructions.Items.Any(item =>
            item.Key == "task.instructions"
            && string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("PackageValidationService did not report missing instructions.md.");
    }

    await File.WriteAllTextAsync(instructionsPath, instructionsContent, Encoding.UTF8);

    var observationPath = ObservationPackageService.GetManifestPath(projectRoot);
    var cleanObservationJson = await File.ReadAllTextAsync(observationPath);
    var invalidObservation = CloneObservationPackage(observation);
    invalidObservation.Artifacts[0].RelativePath = @"C:\Users\test\escape.png";
    await WriteJsonAsync(observationPath, invalidObservation);
    var absolutePathResult = await service.ValidateObservationPackageAsync(projectRoot);
    if (absolutePathResult.IsOk)
    {
        failures.Add("PackageValidationService did not reject an absolute observation artifact path.");
    }

    await File.WriteAllTextAsync(observationPath, cleanObservationJson, Encoding.UTF8);

    var taskPackagePath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var cleanTaskJson = await File.ReadAllTextAsync(taskPackagePath);
    var secretTask = CloneTaskPackage(taskPackage);
    secretTask.ProhibitedActions.Add("OPENAI_API_KEY=sk-test");
    await WriteJsonAsync(taskPackagePath, secretTask);
    var secretResult = await service.ValidateCodexTaskPackageAsync(projectRoot);
    if (secretResult.IsOk
        || !secretResult.Items.Any(item => item.Key.StartsWith("secretScan.", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("PackageValidationService did not report a secret in the task package.");
    }

    await File.WriteAllTextAsync(taskPackagePath, cleanTaskJson, Encoding.UTF8);
}

static async Task CheckP13ContentGenerationAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    await CheckLegacyObservationBridgeAsync(projectRoot, failures);
    await CheckConstructionContextBuilderAsync(projectRoot, failures);
    await CheckCodexTaskPackageContextAsync(projectRoot, projectId, failures);
    await CheckCodexTaskQueuedTransitionsAsync(projectRoot, projectId, failures);
    await CheckPackageValidationModesAsync(projectRoot, failures);
    CheckFoundationWorkflow(failures);
}

static async Task CheckLegacyObservationBridgeAsync(
    string projectRoot,
    List<string> failures)
{
    var service = new LegacyObservationBridgeService();
    var emptyRoot = Path.Combine(projectRoot, ProjectDirectoryV2.Runtime, "p1-3-empty-legacy-bridge");
    Directory.CreateDirectory(emptyRoot);
    var emptyResult = await service.BuildAsync(emptyRoot);
    if (!emptyResult.IsOk || emptyResult.Warnings.Count == 0)
    {
        failures.Add("LegacyObservationBridgeService did not tolerate missing legacy observation files with warnings.");
    }

    try
    {
        await service.BuildAsync(projectRoot, ["../escape.md"]);
        failures.Add("LegacyObservationBridgeService allowed a traversal source path.");
    }
    catch (InvalidOperationException)
    {
    }

    var observationMarkdownPath = Path.Combine(projectRoot, "observation", "observation.md");
    await File.WriteAllTextAsync(
        observationMarkdownPath,
        """
        # Hero
        Clear first viewport statement and visual hierarchy.

        ## Product Detail
        Feature proof appears after the hero section.
        """,
        Encoding.UTF8);

    await File.WriteAllTextAsync(
        Path.Combine(projectRoot, "observation", "action-log.json"),
        """
        [
          { "type": "scroll", "target": "window", "note": "scroll reveal was observed" },
          { "event": "click", "selector": "#cta", "message": "button opens contact panel" }
        ]
        """,
        Encoding.UTF8);

    await File.WriteAllTextAsync(
        Path.Combine(projectRoot, "observation", "frame-index.json"),
        """
        {
          "frames": [
            { "relativePath": "observation/screenshots/frame-0001.png", "timestamp": "00:00:01.000" }
          ]
        }
        """,
        Encoding.UTF8);

    var result = await service.BuildAsync(projectRoot);
    if (!result.IsOk)
    {
        failures.Add("LegacyObservationBridgeService returned a failing result for valid legacy inputs.");
    }

    var reportPath = LegacyObservationBridgeService.GetReportPath(projectRoot);
    if (!File.Exists(reportPath))
    {
        failures.Add("LegacyObservationBridgeService did not write legacy-bridge-report.json.");
    }
    else
    {
        await using var reportStream = File.OpenRead(reportPath);
        var report = await JsonSerializer.DeserializeAsync<LegacyObservationBridgeResult>(reportStream, WrbJsonOptions.Default);
        if (report is null || report.Items.Count == 0)
        {
            failures.Add("legacy-bridge-report.json could not be read with bridge items.");
        }
    }

    var observation = await new ObservationPackageService().LoadAsync(projectRoot);
    if (!observation.Sections.Any(section => section.SectionId.StartsWith("legacy-md-section-", StringComparison.OrdinalIgnoreCase))
        || !observation.Findings.Any(finding => finding.FindingId.StartsWith("legacy-md-finding-", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("LegacyObservationBridgeService did not generate section/finding items from observation.md.");
    }

    if (!observation.Interactions.Any(interaction => interaction.Trigger == "scroll" || interaction.Trigger == "click"))
    {
        failures.Add("LegacyObservationBridgeService did not generate interactions from action-log.json.");
    }

    if (!observation.Artifacts.Any(artifact => artifact.ArtifactId == "legacy-frame-index"))
    {
        failures.Add("LegacyObservationBridgeService did not register frame-index.json as an artifact.");
    }
}

static async Task CheckConstructionContextBuilderAsync(
    string projectRoot,
    List<string> failures)
{
    var builder = new ConstructionPackageContentBuilderService();
    var result = await builder.BuildAsync(projectRoot);
    if (!result.IsOk)
    {
        failures.Add("ConstructionPackageContentBuilderService returned a failing result for a normal project.");
    }

    foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
    {
        var fullPath = Path.Combine(projectRoot, definition.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            failures.Add($"Construction context file was not generated: {definition.RelativePath}");
        }
    }

    var indexPath = Path.Combine(projectRoot, ConstructionPackageContextSchema.PackageIndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    await using (var indexStream = File.OpenRead(indexPath))
    {
        var index = await JsonSerializer.DeserializeAsync<ConstructionPackageContextIndex>(indexStream, WrbJsonOptions.Default);
        if (index is null)
        {
            failures.Add("package-index.json could not be deserialized.");
        }
        else
        {
            foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
            {
                var indexed = index.Files.SingleOrDefault(file =>
                    string.Equals(file.RelativePath, definition.RelativePath, StringComparison.OrdinalIgnoreCase));
                if (indexed is null)
                {
                    failures.Add($"package-index.json does not contain {definition.RelativePath}.");
                    continue;
                }

                var fullPath = Path.Combine(projectRoot, indexed.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (indexed.SizeBytes <= 0 || string.IsNullOrWhiteSpace(indexed.Sha256))
                {
                    failures.Add($"package-index.json did not record hash/size for {indexed.RelativePath}.");
                }
                else if (!string.Equals(indexed.RelativePath, ConstructionPackageContextSchema.PackageIndexRelativePath, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(indexed.Sha256, ComputeSha256ForTest(fullPath), StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"package-index.json hash does not match file content for {indexed.RelativePath}.");
                }

                if (Path.IsPathRooted(indexed.RelativePath))
                {
                    failures.Add($"package-index.json stored an absolute path for {indexed.RelativePath}.");
                }
            }
        }
    }

    foreach (var definition in ConstructionPackageContextSchema.ContextFiles.Where(file => file.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
    {
        var content = await File.ReadAllTextAsync(Path.Combine(projectRoot, definition.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase) || content.Contains(@":\", StringComparison.Ordinal))
        {
            failures.Add($"Construction context markdown contains an absolute local path: {definition.RelativePath}");
        }
    }

    var construction = await new ConstructionPackageService().LoadAsync(projectRoot);
    foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
    {
        if (!construction.Inputs.Any(input => string.Equals(input.RelativePath, definition.RelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"Construction package inputs do not include context file {definition.RelativePath}.");
        }
    }

    var missingRoot = Path.Combine(projectRoot, ProjectDirectoryV2.Runtime, "p1-3-missing-context-source");
    Directory.CreateDirectory(missingRoot);
    var missingResult = await builder.BuildAsync(missingRoot);
    if (missingResult.Warnings.Count == 0)
    {
        failures.Add("ConstructionPackageContentBuilderService did not warn when source manifests were missing.");
    }
}

static async Task CheckCodexTaskPackageContextAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var service = new CodexTaskPackageService();
    var package = await service.CreateNewAsync(projectRoot, projectId);
    await service.SaveAsync(projectRoot, package);
    var instructionsPath = await service.WriteInstructionsAsync(projectRoot, package);
    var reloaded = await service.LoadAsync(projectRoot);

    foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
    {
        if (!reloaded.InputFiles.Any(input => string.Equals(input.RelativePath, definition.RelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"Codex task package inputs do not include context file {definition.RelativePath}.");
        }
    }

    var instructions = await File.ReadAllTextAsync(instructionsPath);
    foreach (var relativePath in new[]
             {
                 WrbProjectSchema.FileName,
                 ConstructionPackageSchema.RelativePath,
                 ConstructionPackageContextSchema.ProjectBriefRelativePath,
                 ConstructionPackageContextSchema.ObservationSummaryRelativePath,
                 ConstructionPackageContextSchema.AssetIndexRelativePath,
                 ConstructionPackageContextSchema.ThemeSummaryRelativePath,
                 ConstructionPackageContextSchema.ContentMapSummaryRelativePath,
                 ConstructionPackageContextSchema.ConstraintsRelativePath,
                 ConstructionPackageContextSchema.AcceptanceChecklistRelativePath,
                 ContentMapSchema.RelativePath,
                 ThemeManifestSchema.RelativePath,
                 AssetsManifestSchema.RelativePath,
                 ObservationPackageSchema.RelativePath
             })
    {
        if (!instructions.Contains(relativePath, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"instructions.md missing reading-order entry: {relativePath}");
        }
    }

    var allowed = reloaded.Sandbox.AllowedWriteRoots.ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!allowed.SetEquals([ProjectDirectoryV2.CodexWorkspace, ProjectDirectoryV2.OutputCurrent]))
    {
        failures.Add("Codex task package allowed write roots are not limited to codex-workspace/ and output-site/current/.");
    }

    if (!reloaded.Sandbox.ForbiddenRoots.Any(root => root.Contains(".git", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("Codex task package lost forbidden roots after P1.3 context update.");
    }
}

static async Task CheckCodexTaskQueuedTransitionsAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var taskPackage = await new CodexTaskPackageService().LoadAsync(projectRoot);
    var service = new CodexTaskRunService();

    var queuedRun = await service.CreateRunAsync(projectRoot, projectId, taskPackage.TaskPackageId);
    var queued = await service.MarkQueuedAsync(projectRoot, queuedRun.RunId);
    if (queued.Status != CodexTaskRunStatus.Queued)
    {
        failures.Add("CodexTaskRunService did not mark a Created run as Queued.");
    }

    var running = await service.MarkRunningAsync(projectRoot, queuedRun.RunId);
    var succeeded = await service.MarkSucceededAsync(projectRoot, queuedRun.RunId, [ProjectDirectoryV2.OutputCurrent + "/index.html"]);
    if (running.Status != CodexTaskRunStatus.Running || succeeded.Status != CodexTaskRunStatus.Succeeded)
    {
        failures.Add("CodexTaskRunService did not support Created -> Queued -> Running -> Succeeded.");
    }

    var directRun = await service.CreateRunAsync(projectRoot, projectId, taskPackage.TaskPackageId);
    await service.MarkRunningAsync(projectRoot, directRun.RunId);
    await service.MarkSucceededAsync(projectRoot, directRun.RunId, [ProjectDirectoryV2.OutputCurrent + "/index.html"]);

    try
    {
        await service.MarkRunningAsync(projectRoot, queuedRun.RunId);
        failures.Add("CodexTaskRunService allowed Succeeded -> Running.");
    }
    catch (InvalidOperationException)
    {
    }

    var failedRun = await service.CreateRunAsync(projectRoot, projectId, taskPackage.TaskPackageId);
    await service.MarkRunningAsync(projectRoot, failedRun.RunId);
    await service.MarkFailedAsync(projectRoot, failedRun.RunId, TaskFailureCategory.ValidationError, "validation failed");
    try
    {
        await service.MarkRunningAsync(projectRoot, failedRun.RunId);
        failures.Add("CodexTaskRunService allowed Failed -> Running.");
    }
    catch (InvalidOperationException)
    {
    }

    var runRecordJson = await File.ReadAllTextAsync(CodexTaskRunService.GetRunRecordPath(projectRoot, queuedRun.RunId));
    if (!runRecordJson.Contains("\"status\": \"succeeded\"", StringComparison.Ordinal))
    {
        failures.Add("Codex task run status stopped serializing as a JSON string enum.");
    }
}

static async Task CheckPackageValidationModesAsync(
    string projectRoot,
    List<string> failures)
{
    var service = new PackageValidationService();
    var strict = await service.ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Strict);
    if (!strict.IsOk)
    {
        failures.Add($"PackageValidationService strict mode did not accept a complete P1.3 task package: {DescribeValidationErrors(strict)}");
    }

    var indexPath = Path.Combine(projectRoot, ConstructionPackageContextSchema.PackageIndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var indexJson = await File.ReadAllTextAsync(indexPath);
    File.Delete(indexPath);
    var draftMissingContext = await service.ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Draft);
    var strictMissingContext = await service.ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Strict);
    if (!draftMissingContext.IsOk
        || !draftMissingContext.Items.Any(item =>
            item.RelativePath == ConstructionPackageContextSchema.PackageIndexRelativePath
            && string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("PackageValidationService draft mode did not downgrade missing context package-index to warning.");
    }

    if (strictMissingContext.IsOk
        || !strictMissingContext.Items.Any(item =>
            item.RelativePath == ConstructionPackageContextSchema.PackageIndexRelativePath
            && string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("PackageValidationService strict mode did not treat missing context package-index as error.");
    }

    await File.WriteAllTextAsync(indexPath, indexJson, Encoding.UTF8);

    var taskPackagePath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var cleanTaskJson = await File.ReadAllTextAsync(taskPackagePath);
    var taskPackage = await new CodexTaskPackageService().LoadAsync(projectRoot);
    taskPackage.ProhibitedActions.Add("OPENAI_API_KEY=sk-test");
    await WriteJsonAsync(taskPackagePath, taskPackage);
    var draftSecret = await service.ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Draft);
    var strictSecret = await service.ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Strict);
    if (draftSecret.IsOk || strictSecret.IsOk)
    {
        failures.Add("PackageValidationService did not treat a detected secret marker as error in both modes.");
    }

    await File.WriteAllTextAsync(taskPackagePath, cleanTaskJson, Encoding.UTF8);

    var observationPath = ObservationPackageService.GetManifestPath(projectRoot);
    var cleanObservationJson = await File.ReadAllTextAsync(observationPath);
    var observation = await new ObservationPackageService().LoadAsync(projectRoot);
    observation.Artifacts[0].RelativePath = @"C:\Users\test\escape.png";
    await WriteJsonAsync(observationPath, observation);
    var draftAbsolute = await service.ValidateObservationPackageAsync(projectRoot, PackageValidationMode.Draft);
    var strictAbsolute = await service.ValidateObservationPackageAsync(projectRoot, PackageValidationMode.Strict);
    if (draftAbsolute.IsOk || strictAbsolute.IsOk)
    {
        failures.Add("PackageValidationService did not reject an absolute path in both modes.");
    }

    await File.WriteAllTextAsync(observationPath, cleanObservationJson, Encoding.UTF8);

    var modeJson = JsonSerializer.Serialize(PackageValidationMode.Strict, WrbJsonOptions.Default);
    if (!modeJson.Contains("\"strict\"", StringComparison.Ordinal))
    {
        failures.Add("PackageValidationMode did not serialize as a JSON string enum.");
    }
}

static async Task CheckP14SnapshotRestoreAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var themeService = new ThemeManifestService();
    var contentMapService = new ContentMapService();
    var manifestService = new ProjectManifestService();
    var snapshotService = new ProjectSnapshotService();
    var restoreService = new SnapshotRestoreService();

    var snapshotTheme = await themeService.LoadAsync(projectRoot);
    snapshotTheme.Notes = "P1.4 snapshot theme notes";
    await themeService.SaveAsync(projectRoot, snapshotTheme);

    var snapshotContentMap = await contentMapService.LoadAsync(projectRoot);
    snapshotContentMap.Pages[0].Title = "P1.4 snapshot page title";
    await contentMapService.SaveAsync(projectRoot, snapshotContentMap);

    var snapshotProject = await manifestService.LoadAsync(projectRoot);
    snapshotProject.CurrentOutputVersion = "p1-4-snapshot-output";
    await manifestService.SaveAsync(projectRoot, snapshotProject);

    var snapshotOutputPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    await File.WriteAllTextAsync(snapshotOutputPath, "<!doctype html><title>P1.4 snapshot</title>", Encoding.UTF8);

    var snapshot = await snapshotService.CreateSnapshotAsync(projectRoot, "p1.4 restorable snapshot");

    var listed = await restoreService.ListSnapshotsAsync(projectRoot);
    if (!listed.Any(item => string.Equals(item.SnapshotId, snapshot.SnapshotId, StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("SnapshotRestoreService.ListSnapshotsAsync did not include the created snapshot.");
    }

    var loaded = await restoreService.LoadSnapshotAsync(projectRoot, snapshot.SnapshotId);
    if (!string.Equals(loaded.SnapshotId, snapshot.SnapshotId, StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("SnapshotRestoreService.LoadSnapshotAsync returned the wrong snapshot.");
    }

    var validation = await restoreService.ValidateSnapshotAsync(projectRoot, snapshot.SnapshotId);
    if (!validation.IsOk)
    {
        failures.Add($"SnapshotRestoreService.ValidateSnapshotAsync did not accept a normal snapshot: {DescribeSnapshotValidationErrors(validation)}");
    }

    var themeSnapshotItem = snapshot.Files.FirstOrDefault(file =>
        string.Equals(file.RelativePath, ThemeManifestSchema.RelativePath, StringComparison.OrdinalIgnoreCase))
        ?? snapshot.Files.FirstOrDefault();
    if (themeSnapshotItem is null)
    {
        failures.Add("ProjectSnapshotService did not produce a restorable file for P1.4 validation.");
        return;
    }

    var snapshotFilePath = Path.Combine(
        projectRoot,
        ProjectSnapshotSchema.SnapshotRootRelativePath,
        snapshot.SnapshotId,
        themeSnapshotItem.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    var originalSnapshotBytes = await File.ReadAllBytesAsync(snapshotFilePath);
    await File.AppendAllTextAsync(snapshotFilePath, "tampered", Encoding.UTF8);
    var tamperedValidation = await restoreService.ValidateSnapshotAsync(projectRoot, snapshot.SnapshotId);
    if (tamperedValidation.IsOk
        || !tamperedValidation.Items.Any(item => string.Equals(item.Key, "snapshot.file.hashMismatch", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("SnapshotRestoreService did not detect a snapshot file hash mismatch.");
    }

    await File.WriteAllBytesAsync(snapshotFilePath, originalSnapshotBytes);

    var changedTheme = await themeService.LoadAsync(projectRoot);
    changedTheme.Notes = "P1.4 changed after snapshot";
    await themeService.SaveAsync(projectRoot, changedTheme);

    var changedContentMap = await contentMapService.LoadAsync(projectRoot);
    changedContentMap.Pages[0].Title = "P1.4 changed after snapshot";
    await contentMapService.SaveAsync(projectRoot, changedContentMap);

    var changedProject = await manifestService.LoadAsync(projectRoot);
    changedProject.CurrentOutputVersion = "p1-4-changed-output";
    await manifestService.SaveAsync(projectRoot, changedProject);
    await File.WriteAllTextAsync(snapshotOutputPath, "<!doctype html><title>P1.4 changed</title>", Encoding.UTF8);

    var restore = await restoreService.RestoreAsync(projectRoot, snapshot.SnapshotId);
    if (!restore.IsOk)
    {
        failures.Add($"SnapshotRestoreService.RestoreAsync failed for a valid snapshot: {string.Join("; ", restore.Errors)}");
    }

    if (string.IsNullOrWhiteSpace(restore.SafetySnapshotId))
    {
        failures.Add("SnapshotRestoreService did not create a before-restore safety snapshot.");
    }
    else
    {
        var afterRestoreSnapshots = await restoreService.ListSnapshotsAsync(projectRoot);
        var safetySnapshot = afterRestoreSnapshots.SingleOrDefault(item =>
            string.Equals(item.SnapshotId, restore.SafetySnapshotId, StringComparison.OrdinalIgnoreCase));
        if (safetySnapshot is null
            || !safetySnapshot.Reason.Contains($"before-restore:{snapshot.SnapshotId}", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Before-restore safety snapshot was not listed with the expected reason.");
        }
    }

    var restoredTheme = await themeService.LoadAsync(projectRoot);
    if (!string.Equals(restoredTheme.Notes, "P1.4 snapshot theme notes", StringComparison.Ordinal))
    {
        failures.Add("SnapshotRestoreService did not restore theme/theme.json from the snapshot.");
    }

    var restoredContentMap = await contentMapService.LoadAsync(projectRoot);
    if (!string.Equals(restoredContentMap.Pages[0].Title, "P1.4 snapshot page title", StringComparison.Ordinal))
    {
        failures.Add("SnapshotRestoreService did not restore maps/content-map.json from the snapshot.");
    }

    var restoredProject = await manifestService.LoadAsync(projectRoot);
    if (!string.Equals(restoredProject.CurrentOutputVersion, "p1-4-snapshot-output", StringComparison.Ordinal))
    {
        failures.Add("SnapshotRestoreService did not restore project.wrbproj from the snapshot.");
    }

    var restoredOutput = await File.ReadAllTextAsync(snapshotOutputPath);
    if (!restoredOutput.Contains("P1.4 snapshot", StringComparison.Ordinal))
    {
        failures.Add("SnapshotRestoreService did not restore output-site/current content from the snapshot.");
    }

    if (!File.Exists(SnapshotRestoreService.GetRestorePlanPath(projectRoot, restore.RestoreId)))
    {
        failures.Add("SnapshotRestoreService did not write restore-plan.json.");
    }

    if (!File.Exists(SnapshotRestoreService.GetRestoreResultPath(projectRoot, restore.RestoreId)))
    {
        failures.Add("SnapshotRestoreService did not write restore-result.json.");
    }

    var projectLogPath = Path.Combine(projectRoot, ProjectDirectoryV2.Logs, "project.log");
    var securityLogPath = Path.Combine(projectRoot, ProjectDirectoryV2.Logs, "security.log");
    if (!File.Exists(projectLogPath) || !File.ReadAllText(projectLogPath).Contains(restore.RestoreId, StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("SnapshotRestoreService did not write the project log.");
    }

    if (!File.Exists(securityLogPath) || !File.ReadAllText(securityLogPath).Contains("Snapshot restore boundary check", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("SnapshotRestoreService did not write the security log.");
    }

    var policySnapshot = await CreateManualSnapshotAsync(
        projectRoot,
        "p1-4-policy-snapshot",
        projectId,
        [
            ("output-site/site.zip", "zip content", null),
            ("output-site/demo.mp4", "video content", null)
        ]);
    var policyValidation = await restoreService.ValidateSnapshotAsync(projectRoot, policySnapshot.SnapshotId);
    if (!policyValidation.IsOk
        || !policyValidation.Items.Any(item => string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase)
                                              && item.Message.Contains("extension", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("SnapshotRestoreService did not treat zip/video restore targets as skipped warnings.");
    }

    var policyPlan = await restoreService.CreateRestorePlanAsync(projectRoot, policySnapshot.SnapshotId);
    if (!policyPlan.Files.All(file => string.Equals(file.Status, "skipped", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("SnapshotRestoreService did not skip zip/video files in the restore plan.");
    }

    var forbiddenGitSnapshot = await CreateManualSnapshotAsync(
        projectRoot,
        "p1-4-forbidden-git-snapshot",
        projectId,
        [(".git/config", "do not restore", null)]);
    var forbiddenGitValidation = await restoreService.ValidateSnapshotAsync(projectRoot, forbiddenGitSnapshot.SnapshotId);
    if (forbiddenGitValidation.IsOk)
    {
        failures.Add("SnapshotRestoreService validation allowed a .git restore path.");
    }

    var forbiddenGitRestore = await restoreService.RestoreAsync(projectRoot, forbiddenGitSnapshot.SnapshotId);
    if (forbiddenGitRestore.IsOk || File.Exists(Path.Combine(projectRoot, ".git", "config")))
    {
        failures.Add("SnapshotRestoreService restored or accepted a .git path.");
    }

    var escapeSnapshot = new ProjectSnapshotManifest
    {
        SchemaVersion = ProjectSnapshotSchema.CurrentSchemaVersion,
        SnapshotId = "p1-4-escape-snapshot",
        ProjectId = projectId,
        CreatedAt = DateTimeOffset.UtcNow,
        Reason = "P1.4 escape self-test",
        Files =
        [
            new SnapshotFileItem
            {
                RelativePath = "../escape.txt",
                Sha256 = "not-used",
                SizeBytes = 1
            }
        ]
    };
    await WriteManualSnapshotManifestAsync(projectRoot, escapeSnapshot);
    var escapeValidation = await restoreService.ValidateSnapshotAsync(projectRoot, escapeSnapshot.SnapshotId);
    if (escapeValidation.IsOk)
    {
        failures.Add("SnapshotRestoreService validation allowed a project-escape snapshot path.");
    }

    var escapeRestore = await restoreService.RestoreAsync(projectRoot, escapeSnapshot.SnapshotId);
    if (escapeRestore.IsOk || File.Exists(Path.Combine(projectRoot, "..", "escape.txt")))
    {
        failures.Add("SnapshotRestoreService restored or accepted a project-escape path.");
    }
}

static async Task CheckP15ConstructionReadinessAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var outputBin = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "bin");
    if (Directory.Exists(outputBin))
    {
        Directory.Delete(outputBin, recursive: true);
    }

    var secretTestPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "secret-test.js");
    if (File.Exists(secretTestPath))
    {
        File.Delete(secretTestPath);
    }

    var taskService = new CodexTaskPackageService();
    var taskPackage = await taskService.CreateNewAsync(projectRoot, projectId);
    await taskService.SaveAsync(projectRoot, taskPackage);
    await taskService.WriteInstructionsAsync(projectRoot, taskPackage);

    var contentBuilder = new ConstructionPackageContentBuilderService();
    await contentBuilder.BuildAsync(projectRoot);

    var gate = new ConstructionReadinessGateService();
    var draft = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.Draft);
    if (!draft.IsReady)
    {
        failures.Add($"ConstructionReadinessGateService Draft mode was not ready: {DescribeReadinessBlockers(draft)}");
    }

    var strict = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.Strict);
    if (!strict.IsReady)
    {
        failures.Add($"ConstructionReadinessGateService Strict mode was not ready: {DescribeReadinessBlockers(strict)}");
    }

    var pre = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.PreCodexDryRun);
    if (!pre.IsReady)
    {
        failures.Add($"ConstructionReadinessGateService PreCodexDryRun mode was not ready: {DescribeReadinessBlockers(pre)}");
    }

    if (!pre.Items.Any(item => item.Category == "rollback" && item.Key == "rollback.snapshotValidation" && item.Status == ConstructionReadinessStatus.Ok))
    {
        failures.Add("ConstructionReadinessGateService did not verify rollback snapshot validation.");
    }

    var snapshots = await new SnapshotRestoreService().ListSnapshotsAsync(projectRoot);
    if (!snapshots.Any(snapshot =>
            snapshot.SnapshotId.StartsWith("readiness-probe-", StringComparison.OrdinalIgnoreCase)
            && string.Equals(snapshot.Reason, "readiness-probe", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add("Readiness gate did not create a readiness-probe snapshot with the required prefix/reason.");
    }

    if (!pre.Items.Any(item => item.Category == "aiEngine"
                               && item.Status == ConstructionReadinessStatus.Warning
                               && !item.BlocksExecution))
    {
        failures.Add("ConstructionReadinessGateService did not record missing AI engines as non-blocking warnings.");
    }

    if (!pre.Items.Any(item => item.Category == "optionalAwareness"
                               && item.Status == ConstructionReadinessStatus.Warning
                               && !item.BlocksExecution))
    {
        failures.Add("ConstructionReadinessGateService did not record optional design/reference awareness as non-blocking warnings.");
    }

    if (!pre.Items.Any(item => item.Key == "outputSurface.indexOptional"
                               && item.Status == ConstructionReadinessStatus.Ok))
    {
        failures.Add("ConstructionReadinessGateService did not mark generated index.html as optional.");
    }

    var reportJsonPath = ConstructionReadinessGateService.GetReportJsonPath(projectRoot);
    var reportMarkdownPath = ConstructionReadinessGateService.GetReportMarkdownPath(projectRoot);
    if (!File.Exists(reportJsonPath) || !File.Exists(reportMarkdownPath))
    {
        failures.Add("ConstructionReadinessGateService did not write readiness-report.json and readiness-report.md.");
    }
    else
    {
        var reportJson = await File.ReadAllTextAsync(reportJsonPath);
        var reportMarkdown = await File.ReadAllTextAsync(reportMarkdownPath);
        if (!reportJson.Contains("\"mode\": \"PreCodexDryRun\"", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("readiness-report.json did not serialize readiness mode as a string.");
        }

        if (ReportLeaksSensitiveData(reportJson, projectRoot) || ReportLeaksSensitiveData(reportMarkdown, projectRoot))
        {
            failures.Add("Readiness reports leaked a local path or secret marker.");
        }
    }

    await CheckP15SecretAndSanitizerAsync(projectRoot, gate, taskService, failures);
    await contentBuilder.BuildAsync(projectRoot);

    await CheckP15PackageIndexMismatchAsync(projectRoot, gate, contentBuilder, failures);
    await CheckP15MissingPackageIndexAsync(projectRoot, gate, contentBuilder, failures);
    await CheckP15OutputIndexIsOptionalAsync(projectRoot, gate, failures);
    await CheckP15MissingOutputSurfaceAsync(projectRoot, gate, failures);
    await CheckP15TaskSandboxFailureAsync(projectRoot, gate, taskService, contentBuilder, failures);
    await CheckP15ReferenceAssetRiskAsync(projectRoot, gate, contentBuilder, failures);
}

static async Task CheckP15SecretAndSanitizerAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    CodexTaskPackageService taskService,
    List<string> failures)
{
    var taskPath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var cleanTask = await taskService.LoadAsync(projectRoot);
    var secretTask = CloneTaskPackage(cleanTask);
    secretTask.ProhibitedActions.Add(@"OPENAI_API_KEY=sk-test C:\Users\test\.ssh");
    await WriteJsonAsync(taskPath, secretTask);

    var secretResult = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.Draft);
    if (secretResult.IsReady
        || !secretResult.Items.Any(item => item.BlocksExecution && item.FailureCategory == TaskFailureCategory.SecretDetected))
    {
        failures.Add("ConstructionReadinessGateService did not block a secret/local path in task-package.json.");
    }

    var reportJson = await File.ReadAllTextAsync(ConstructionReadinessGateService.GetReportJsonPath(projectRoot));
    var reportMarkdown = await File.ReadAllTextAsync(ConstructionReadinessGateService.GetReportMarkdownPath(projectRoot));
    if (ReportLeaksSensitiveData(reportJson, projectRoot) || ReportLeaksSensitiveData(reportMarkdown, projectRoot))
    {
        failures.Add("ConstructionReadinessGateService report sanitizer did not redact a secret/local path.");
    }

    await taskService.SaveAsync(projectRoot, cleanTask);
    await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
}

static async Task CheckP15PackageIndexMismatchAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var projectBriefPath = Path.Combine(
        projectRoot,
        ConstructionPackageContextSchema.ProjectBriefRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var original = await File.ReadAllTextAsync(projectBriefPath);
    await File.WriteAllTextAsync(projectBriefPath, original + Environment.NewLine + "tampered", Encoding.UTF8);

    var mismatch = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.PreCodexDryRun);
    if (mismatch.IsReady
        || !mismatch.Items.Any(item => item.Key.Contains("context.hash.projectBrief", StringComparison.OrdinalIgnoreCase)
                                      && item.BlocksExecution))
    {
        failures.Add("ConstructionReadinessGateService did not block a package-index context hash mismatch.");
    }

    await File.WriteAllTextAsync(projectBriefPath, original, Encoding.UTF8);
    await contentBuilder.BuildAsync(projectRoot);
}

static async Task CheckP15MissingPackageIndexAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var packageIndexPath = Path.Combine(
        projectRoot,
        ConstructionPackageContextSchema.PackageIndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var original = await File.ReadAllTextAsync(packageIndexPath);
    File.Delete(packageIndexPath);

    var missingIndex = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.Strict);
    if (missingIndex.IsReady
        || !missingIndex.Items.Any(item => item.Key == "context.packageIndex" && item.BlocksExecution))
    {
        failures.Add("ConstructionReadinessGateService Strict mode did not block a missing context package-index.json.");
    }

    await File.WriteAllTextAsync(packageIndexPath, original, Encoding.UTF8);
    await contentBuilder.BuildAsync(projectRoot);
}

static async Task CheckP15OutputIndexIsOptionalAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    List<string> failures)
{
    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    var hadIndex = File.Exists(indexPath);
    var original = hadIndex ? await File.ReadAllTextAsync(indexPath) : string.Empty;
    if (hadIndex)
    {
        File.Delete(indexPath);
    }

    var noIndex = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.PreCodexDryRun);
    if (!noIndex.IsReady)
    {
        failures.Add($"ConstructionReadinessGateService required output-site/current/index.html unexpectedly: {DescribeReadinessBlockers(noIndex)}");
    }

    if (!noIndex.Items.Any(item => item.Key == "outputSurface.indexOptional"
                                   && item.Status == ConstructionReadinessStatus.Ok))
    {
        failures.Add("ConstructionReadinessGateService did not report index.html as optional when missing.");
    }

    if (hadIndex)
    {
        await File.WriteAllTextAsync(indexPath, original, Encoding.UTF8);
    }
}

static async Task CheckP15MissingOutputSurfaceAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    List<string> failures)
{
    var outputRoot = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent);
    var backupRoot = Path.Combine(projectRoot, ProjectDirectoryV2.Runtime, "p1-5-output-current-backup");
    if (Directory.Exists(backupRoot))
    {
        Directory.Delete(backupRoot, recursive: true);
    }

    Directory.Move(outputRoot, backupRoot);
    try
    {
        var missingOutput = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.PreCodexDryRun);
        if (missingOutput.IsReady
            || !missingOutput.Items.Any(item => item.Key is "outputSurface.directory" or "directory.writable.output-site/current"
                                              && item.BlocksExecution))
        {
            failures.Add("ConstructionReadinessGateService PreCodexDryRun did not block a missing output-site/current directory.");
        }
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.Move(backupRoot, outputRoot);
    }
}

static async Task CheckP15TaskSandboxFailureAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var cleanTask = await taskService.LoadAsync(projectRoot);
    var badTask = CloneTaskPackage(cleanTask);
    badTask.Sandbox.AllowedWriteRoots.Add(ProjectDirectoryV2.Logs);
    await taskService.SaveAsync(projectRoot, badTask);
    await taskService.WriteInstructionsAsync(projectRoot, badTask);
    await contentBuilder.BuildAsync(projectRoot);

    var badSandbox = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.PreCodexDryRun);
    if (badSandbox.IsReady
        || !badSandbox.Items.Any(item => item.Key == "task.allowedWriteRoots.exact"
                                        && item.BlocksExecution
                                        && item.FailureCategory == TaskFailureCategory.SandboxViolation))
    {
        failures.Add("ConstructionReadinessGateService did not block expanded task allowed write roots.");
    }

    await taskService.SaveAsync(projectRoot, cleanTask);
    await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
    await contentBuilder.BuildAsync(projectRoot);
}

static async Task CheckP15ReferenceAssetRiskAsync(
    string projectRoot,
    ConstructionReadinessGateService gate,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var assetsPath = AssetsManifestService.GetManifestPath(projectRoot);
    var cleanAssets = await new AssetsManifestService().LoadAsync(projectRoot);
    var riskyAssets = CloneAssetsManifest(cleanAssets);
    if (riskyAssets.Assets.Count == 0)
    {
        riskyAssets.Assets.Add(new AssetManifestItem
        {
            AssetId = "asset-reference-risk",
            Kind = "image",
            Role = "reference",
            RelativePath = "input/assets/test-logo.txt"
        });
    }

    riskyAssets.Assets[0].SourceType = "referenceSite";
    riskyAssets.Assets[0].IsExternalReference = true;
    riskyAssets.Assets[0].IsApprovedForExport = true;
    await WriteJsonAsync(assetsPath, riskyAssets);
    await contentBuilder.BuildAsync(projectRoot);

    var risk = await gate.CheckAsync(projectRoot, ConstructionReadinessMode.Strict);
    if (risk.IsReady
        || !risk.Items.Any(item => item.Key.Contains("assets.referenceApproved", StringComparison.OrdinalIgnoreCase)
                                  && item.BlocksExecution))
    {
        failures.Add("ConstructionReadinessGateService did not block approved reference-site asset export risk.");
    }

    await new AssetsManifestService().SaveAsync(projectRoot, cleanAssets);
    await contentBuilder.BuildAsync(projectRoot);
}

static async Task CheckP16DryRunOrchestratorAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    if (File.Exists(indexPath))
    {
        File.Delete(indexPath);
    }

    var taskService = new CodexTaskPackageService();
    var contentBuilder = new ConstructionPackageContentBuilderService();
    var taskPackage = await taskService.CreateNewAsync(projectRoot, projectId);
    await taskService.SaveAsync(projectRoot, taskPackage);
    await taskService.WriteInstructionsAsync(projectRoot, taskPackage);
    await contentBuilder.BuildAsync(projectRoot);

    var service = new CodexDryRunOrchestratorService();
    var success = await service.RunAsync(projectRoot);
    if (!success.IsOk || !success.IsReadyForFutureExecution)
    {
        failures.Add($"CodexDryRunOrchestratorService did not pass a valid dry-run: {string.Join("; ", success.BlockingReasons)}");
    }

    if (success.ExecutedCodexCli || success.CalledOpenAiApi || success.GeneratedWebsite)
    {
        failures.Add("CodexDryRunOrchestratorService reported a forbidden execution flag as true.");
    }

    var planPath = CodexDryRunOrchestratorService.GetPlanPath(projectRoot, success.DryRunId);
    var resultPath = CodexDryRunOrchestratorService.GetResultPath(projectRoot, success.DryRunId);
    var reportPath = CodexDryRunOrchestratorService.GetReportPath(projectRoot, success.DryRunId);
    var recordPath = CodexDryRunOrchestratorService.GetRecordPath(projectRoot, success.DryRunId);
    if (!File.Exists(planPath) || !File.Exists(resultPath) || !File.Exists(reportPath) || !File.Exists(recordPath))
    {
        failures.Add("CodexDryRunOrchestratorService did not write plan/result/report/record files.");
    }
    else
    {
        var plan = await ReadJsonAsync<CodexDryRunPlan>(planPath);
        var result = await ReadJsonAsync<CodexDryRunResult>(resultPath);
        var record = await ReadJsonAsync<CodexDryRunRecord>(recordPath);
        if (plan is null || result is null || record is null)
        {
            failures.Add("Dry-run plan/result/record could not be deserialized.");
        }
        else
        {
            var requiredSteps = new[]
            {
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
            };

            foreach (var stepId in requiredSteps)
            {
                if (!plan.Steps.Any(step => string.Equals(step.StepId, stepId, StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add($"Dry-run plan missing simulated step: {stepId}");
                }
            }

            var simulateStep = plan.Steps.SingleOrDefault(step => step.StepId == "simulate-codex-command");
            if (simulateStep is null || simulateStep.WouldRunExternalProcess)
            {
                failures.Add("Dry-run simulate-codex-command step would run an external process.");
            }

            if (plan.WouldExecuteCodexCli)
            {
                failures.Add("Dry-run plan says it would execute Codex CLI.");
            }

            if (!plan.SafetyChecks.Any(check => check.Key == "readiness-gate" && check.Status == "ok"))
            {
                failures.Add("Dry-run plan did not record successful PreCodexDryRun readiness integration.");
            }

            if (!record.IsDryRun
                || record.ExecutedCodexCli
                || record.CalledOpenAiApi
                || record.GeneratedWebsite
                || string.Equals(record.Status, "running", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("Dry-run record can be misread as a real Codex execution.");
            }
        }

        var report = await File.ReadAllTextAsync(reportPath);
        foreach (var required in new[]
                 {
                     "# Codex CLI Dry-Run Report",
                     "Codex CLI executed: false",
                     "OpenAI API called: false",
                     "Website generated: false",
                     "Codex CLI not executed",
                     "OpenAI API not called",
                     "Website not generated",
                     "Dry-run only"
                 })
        {
            if (!report.Contains(required, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"dry-run-report.md missing required statement: {required}");
            }
        }

        if (DryRunArtifactLeaksSensitiveData(projectRoot, planPath, resultPath, reportPath, recordPath))
        {
            failures.Add("Dry-run artifacts leaked local path or sensitive marker.");
        }
    }

    if (File.Exists(indexPath))
    {
        failures.Add("Dry-run created output-site/current/index.html.");
        File.Delete(indexPath);
    }

    await CheckP16ReadinessFailureStillReportsAsync(projectRoot, service, contentBuilder, failures);
    await CheckP16MissingTaskPackageAsync(projectRoot, service, taskService, contentBuilder, failures);
    await CheckP16InstructionBoundaryFailureAsync(projectRoot, service, taskService, contentBuilder, failures);
    await CheckP16UnsafeAllowedRootsAsync(projectRoot, service, taskService, contentBuilder, failures);
    await CheckP16DryRunSanitizerAsync(projectRoot, service, taskService, contentBuilder, failures);
    await CheckP16LogsAsync(projectRoot, failures);

    taskPackage = await taskService.CreateNewAsync(projectRoot, projectId);
    await taskService.SaveAsync(projectRoot, taskPackage);
    await taskService.WriteInstructionsAsync(projectRoot, taskPackage);
    await contentBuilder.BuildAsync(projectRoot);
}

static async Task CheckP16ReadinessFailureStillReportsAsync(
    string projectRoot,
    CodexDryRunOrchestratorService service,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var packageIndexPath = Path.Combine(
        projectRoot,
        ConstructionPackageContextSchema.PackageIndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var original = await File.ReadAllTextAsync(packageIndexPath);
    File.Delete(packageIndexPath);
    try
    {
        var blocked = await service.RunAsync(projectRoot);
        if (blocked.IsOk || blocked.IsReadyForFutureExecution)
        {
            failures.Add("Dry-run stayed ready when PreCodexDryRun readiness gate failed.");
        }

        if (!File.Exists(CodexDryRunOrchestratorService.GetReportPath(projectRoot, blocked.DryRunId)))
        {
            failures.Add("Dry-run did not write a report when readiness gate failed.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(packageIndexPath, original, Encoding.UTF8);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP16MissingTaskPackageAsync(
    string projectRoot,
    CodexDryRunOrchestratorService service,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var taskPath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var original = await File.ReadAllTextAsync(taskPath);
    File.Delete(taskPath);
    try
    {
        var missing = await service.RunAsync(projectRoot);
        if (missing.IsOk
            || !missing.BlockingReasons.Any(reason => reason.Contains("task-package", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Dry-run did not block a missing task-package.json.");
        }

        if (!File.Exists(CodexDryRunOrchestratorService.GetReportPath(projectRoot, missing.DryRunId)))
        {
            failures.Add("Dry-run did not write a report for a missing task package.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(taskPath, original, Encoding.UTF8);
        var cleanTask = await taskService.LoadAsync(projectRoot);
        await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP16InstructionBoundaryFailureAsync(
    string projectRoot,
    CodexDryRunOrchestratorService service,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var instructionsPath = CodexTaskPackageService.GetInstructionsPath(projectRoot);
    var original = await File.ReadAllTextAsync(instructionsPath);
    await File.WriteAllTextAsync(
        instructionsPath,
        "# Unsafe Instructions" + Environment.NewLine + "Reading order only.",
        Encoding.UTF8);
    try
    {
        var badInstructions = await service.RunAsync(projectRoot);
        if (badInstructions.IsOk
            || !badInstructions.BlockingReasons.Any(reason => reason.Contains("instructions-boundary", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Dry-run did not block instructions.md with missing safety boundaries.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(instructionsPath, original, Encoding.UTF8);
        var cleanTask = await taskService.LoadAsync(projectRoot);
        await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP16UnsafeAllowedRootsAsync(
    string projectRoot,
    CodexDryRunOrchestratorService service,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var taskPath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var original = await File.ReadAllTextAsync(taskPath);
    var badTask = await taskService.LoadAsync(projectRoot);
    badTask.Sandbox.AllowedWriteRoots.Add(ProjectDirectoryV2.Logs);
    await WriteJsonAsync(taskPath, badTask);
    try
    {
        var badRoots = await service.RunAsync(projectRoot);
        if (badRoots.IsOk
            || !badRoots.BlockingReasons.Any(reason => reason.Contains("allowed-write-roots-exact", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Dry-run did not block unsafe allowed write roots.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(taskPath, original, Encoding.UTF8);
        var cleanTask = await taskService.LoadAsync(projectRoot);
        await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP16DryRunSanitizerAsync(
    string projectRoot,
    CodexDryRunOrchestratorService service,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var instructionsPath = CodexTaskPackageService.GetInstructionsPath(projectRoot);
    var original = await File.ReadAllTextAsync(instructionsPath);
    await File.WriteAllTextAsync(
        instructionsPath,
        original + Environment.NewLine + @"OPENAI_API_KEY=sk-test C:\Users\test\.ssh token password secret cookie",
        Encoding.UTF8);
    try
    {
        var sanitized = await service.RunAsync(projectRoot);
        if (sanitized.IsOk)
        {
            failures.Add("Dry-run did not block sensitive markers in instructions.md.");
        }

        var planPath = CodexDryRunOrchestratorService.GetPlanPath(projectRoot, sanitized.DryRunId);
        var resultPath = CodexDryRunOrchestratorService.GetResultPath(projectRoot, sanitized.DryRunId);
        var reportPath = CodexDryRunOrchestratorService.GetReportPath(projectRoot, sanitized.DryRunId);
        var recordPath = CodexDryRunOrchestratorService.GetRecordPath(projectRoot, sanitized.DryRunId);
        if (DryRunArtifactLeaksSensitiveData(projectRoot, planPath, resultPath, reportPath, recordPath))
        {
            failures.Add("Dry-run sanitizer did not remove sensitive markers from artifacts.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(instructionsPath, original, Encoding.UTF8);
        var cleanTask = await taskService.LoadAsync(projectRoot);
        await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP16LogsAsync(
    string projectRoot,
    List<string> failures)
{
    var expectedLogs = new[]
    {
        ("logs/project.log", "P1.6 dry-run completed"),
        ("logs/security.log", "OpenAI API not called"),
        ("logs/codex-task.log", "Website not generated")
    };

    foreach (var (relativePath, requiredText) in expectedLogs)
    {
        var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            failures.Add($"Dry-run log was not written: {relativePath}");
            continue;
        }

        var content = await File.ReadAllTextAsync(fullPath);
        if (!content.Contains(requiredText, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Dry-run log {relativePath} missing text: {requiredText}");
        }
    }
}

static async Task CheckP171ProofCheckPackageAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    if (File.Exists(indexPath))
    {
        File.Delete(indexPath);
    }

    var service = new ProofCheckPackageService();
    var manifest = await service.CreateNewAsync(projectRoot);
    if (!string.Equals(manifest.SchemaVersion, ProofCheckPackageSchema.CurrentSchemaVersion, StringComparison.Ordinal)
        || !string.Equals(manifest.ProjectId, projectId, StringComparison.Ordinal)
        || string.IsNullOrWhiteSpace(manifest.ProofPackageId))
    {
        failures.Add("ProofCheckPackageService did not create a manifest with the expected schema/project/package identity.");
    }

    if (manifest.ExecutesCodexCli || manifest.CallsOpenAiApi || manifest.GeneratesWebsite)
    {
        failures.Add("Proof-check manifest reported a forbidden execution/API/website flag as true.");
    }

    var manifestPath = ProofCheckPackageService.GetManifestPath(projectRoot);
    var requestPath = ProofCheckPackageService.GetRequestPath(projectRoot);
    var instructionsPath = ProofCheckPackageService.GetInstructionsPath(projectRoot);
    var validationJsonPath = ProofCheckPackageService.GetValidationReportJsonPath(projectRoot);
    var validationMarkdownPath = ProofCheckPackageService.GetValidationReportMarkdownPath(projectRoot);
    foreach (var requiredPath in new[] { manifestPath, requestPath, instructionsPath, validationJsonPath, validationMarkdownPath })
    {
        if (!File.Exists(requiredPath))
        {
            failures.Add($"ProofCheckPackageService did not write required package file: {Path.GetFileName(requiredPath)}");
        }
    }

    foreach (var plannedRuntimePath in new[]
             {
                 ProofCheckPackageService.GetPlannedCreatedFilePath(projectRoot),
                 ProofCheckPackageService.GetPlannedResultPath(projectRoot),
                 ProofCheckPackageService.GetPlannedReportPath(projectRoot)
             })
    {
        if (File.Exists(plannedRuntimePath))
        {
            failures.Add($"ProofCheckPackageService created a future-only runtime artifact: {Path.GetFileName(plannedRuntimePath)}");
        }
    }

    if (File.Exists(indexPath))
    {
        failures.Add("ProofCheckPackageService created output-site/current/index.html.");
        File.Delete(indexPath);
    }

    var loaded = await service.LoadAsync(projectRoot);
    if (!string.Equals(loaded.ProofPackageId, manifest.ProofPackageId, StringComparison.Ordinal))
    {
        failures.Add("ProofCheckPackageService.LoadAsync did not load the created proof package.");
    }

    var request = await ReadJsonAsync<ProofCheckRequest>(requestPath);
    var report = await ReadJsonAsync<ProofCheckReport>(validationJsonPath);
    if (request is null || report is null)
    {
        failures.Add("Proof-check request or validation report could not be deserialized.");
        return;
    }

    if (!request.MustNotExecuteInThisRound)
    {
        failures.Add("Proof-check request does not preserve the P1.7.1 non-execution flag.");
    }

    if (report.ExecutedCodexCli || report.CalledOpenAiApi || report.GeneratedWebsite)
    {
        failures.Add("Proof-check validation report reported a forbidden execution/API/website flag as true.");
    }

    var validation = await service.ValidateAsync(projectRoot);
    if (!validation.IsOk)
    {
        failures.Add($"ProofCheckPackageService.ValidateAsync did not accept the generated package: {DescribeProofValidationErrors(validation)}");
    }

    var requiredChecks = new[]
    {
        "create-file-in-codex-workspace",
        "deny-write-dot-git",
        "deny-write-outside-project-root",
        "deny-write-system-directory",
        "deny-access-credential-directory",
        "read-task-package",
        "write-codex-workspace",
        "write-output-site-proof-subdirectory-only",
        "hash-check-created-file",
        "record-proof-logs"
    };
    foreach (var requiredCheck in requiredChecks)
    {
        if (!manifest.RequiredChecks.Any(check => check.Required && check.Key == requiredCheck)
            || !request.RequiredChecks.Any(check => check.Required && check.Key == requiredCheck))
        {
            failures.Add($"Proof-check package missing required check: {requiredCheck}");
        }
    }

    var expectedWorkspace = $"{ProjectDirectoryV2.CodexWorkspace}/proof/{manifest.ProofPackageId}";
    var expectedOutputProof = $"{ProjectDirectoryV2.OutputCurrent}/__proofcheck/{manifest.ProofPackageId}";
    var allowedTargets = manifest.AllowedWriteTargets
        .Select(target => target.RelativePath.TrimEnd('/', '\\'))
        .ToList();
    if (manifest.AllowedWriteTargets.Count != 2
        || !allowedTargets.Contains(expectedWorkspace, StringComparer.OrdinalIgnoreCase)
        || !allowedTargets.Contains(expectedOutputProof, StringComparer.OrdinalIgnoreCase)
        || manifest.AllowedWriteTargets.Any(target => !target.IsAllowed))
    {
        failures.Add("Proof-check allowed write targets are not limited to the two planned proof-only directories.");
    }

    foreach (var deniedTarget in new[]
             {
                 ".git/",
                 "../outside",
                 "<system-dir>",
                 "<credential-dir>/.ssh",
                 "<credential-dir>/.codex",
                 "<credential-dir>/.openai",
                 "<app-base-dir>",
                 "WebRebuildRecorder.App/",
                 "WebRebuildRecorder.FoundationSelfTest/"
             })
    {
        var found = manifest.DeniedWriteTargets.Any(target =>
            !target.IsAllowed
            && target.RelativePath.TrimEnd('/', '\\').Contains(deniedTarget.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase));
        if (!found)
        {
            failures.Add($"Proof-check denied targets missing placeholder: {deniedTarget}");
        }
    }

    var instructions = await File.ReadAllTextAsync(instructionsPath);
    foreach (var requiredPhrase in new[]
             {
                 "This is a future proof-check instruction package.",
                 "P1.7.1 does not execute this instruction.",
                 "Do not execute Codex CLI in P1.7.1.",
                 "Do not call OpenAI API in P1.7.1.",
                 "Do not call local model engines in P1.7.1.",
                 "Do not call Ollama or LM Studio in P1.7.1.",
                 "Do not generate a website.",
                 "Do not write output-site/current/index.html.",
                 "Only future approved proof-check execution may create proof-created-file.txt."
             })
    {
        if (!instructions.Contains(requiredPhrase, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"proof-instructions.md missing required boundary phrase: {requiredPhrase}");
        }
    }

    var validationMarkdown = await File.ReadAllTextAsync(validationMarkdownPath);
    foreach (var requiredReportText in new[]
             {
                 "Package valid: true",
                 "Codex CLI executed: false",
                 "OpenAI API called: false",
                 "Website generated: false",
                 "does not call local model engines"
             })
    {
        if (!validationMarkdown.Contains(requiredReportText, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"proof-package-validation-report.md missing text: {requiredReportText}");
        }
    }

    if (ProofReportLeaksConcreteLocalPath(projectRoot, validationJsonPath, validationMarkdownPath))
    {
        failures.Add("Proof-check validation reports leaked a concrete local path or sensitive marker.");
    }

    var originalManifestJson = await File.ReadAllTextAsync(manifestPath);
    var originalRequestJson = await File.ReadAllTextAsync(requestPath);
    var originalInstructions = await File.ReadAllTextAsync(instructionsPath);
    try
    {
        var absoluteManifest = CloneProofManifest(manifest);
        absoluteManifest.AllowedWriteTargets[0].RelativePath = @"C:/Users/test/proof";
        await WriteJsonAsync(manifestPath, absoluteManifest);
        var absoluteResult = await service.ValidateAsync(projectRoot);
        if (absoluteResult.IsOk || !HasProofValidationError(absoluteResult, "proof.allowedWriteTarget.path"))
        {
            failures.Add($"Proof-check validation did not reject an absolute allowed target: {DescribeProofValidationErrors(absoluteResult)}");
        }

        var traversalManifest = CloneProofManifest(manifest);
        traversalManifest.AllowedWriteTargets[0].RelativePath = "../outside";
        await WriteJsonAsync(manifestPath, traversalManifest);
        var traversalResult = await service.ValidateAsync(projectRoot);
        if (traversalResult.IsOk || !HasProofValidationError(traversalResult, "proof.allowedWriteTarget.path"))
        {
            failures.Add($"Proof-check validation did not reject an allowed target containing '..': {DescribeProofValidationErrors(traversalResult)}");
        }

        var gitManifest = CloneProofManifest(manifest);
        gitManifest.AllowedWriteTargets[0].RelativePath = ".git/proof";
        await WriteJsonAsync(manifestPath, gitManifest);
        var gitResult = await service.ValidateAsync(projectRoot);
        if (gitResult.IsOk || !HasProofValidationError(gitResult, "proof.allowedWriteTarget.path"))
        {
            failures.Add($"Proof-check validation did not reject a .git allowed target: {DescribeProofValidationErrors(gitResult)}");
        }

        var sourceManifest = CloneProofManifest(manifest);
        sourceManifest.AllowedWriteTargets[0].RelativePath = "WebRebuildRecorder.App/proof";
        await WriteJsonAsync(manifestPath, sourceManifest);
        var sourceResult = await service.ValidateAsync(projectRoot);
        if (sourceResult.IsOk || !HasProofValidationError(sourceResult, "proof.allowedWriteTarget.forbidden"))
        {
            failures.Add($"Proof-check validation did not reject a source-directory allowed target: {DescribeProofValidationErrors(sourceResult)}");
        }

        var outputIndexManifest = CloneProofManifest(manifest);
        outputIndexManifest.AllowedWriteTargets[0].RelativePath = $"{ProjectDirectoryV2.OutputCurrent}/index.html";
        await WriteJsonAsync(manifestPath, outputIndexManifest);
        var outputIndexResult = await service.ValidateAsync(projectRoot);
        if (outputIndexResult.IsOk || !HasProofValidationError(outputIndexResult, "proof.allowedWriteTarget.forbidden"))
        {
            failures.Add($"Proof-check validation did not reject output-site/current/index.html as an allowed target: {DescribeProofValidationErrors(outputIndexResult)}");
        }

        var executionManifest = CloneProofManifest(manifest);
        executionManifest.ExecutesCodexCli = true;
        await WriteJsonAsync(manifestPath, executionManifest);
        var executionResult = await service.ValidateAsync(projectRoot);
        if (executionResult.IsOk || !HasProofValidationError(executionResult, "proof.executesCodexCli"))
        {
            failures.Add($"Proof-check validation did not reject ExecutesCodexCli=true: {DescribeProofValidationErrors(executionResult)}");
        }

        await File.WriteAllTextAsync(manifestPath, originalManifestJson, Encoding.UTF8);
        var executionRequest = CloneProofRequest(request);
        executionRequest.MustNotExecuteInThisRound = false;
        await WriteJsonAsync(requestPath, executionRequest);
        var requestResult = await service.ValidateAsync(projectRoot);
        if (requestResult.IsOk || !HasProofValidationError(requestResult, "proof.requestMustNotExecute"))
        {
            failures.Add($"Proof-check validation did not reject MustNotExecuteInThisRound=false: {DescribeProofValidationErrors(requestResult)}");
        }

        await File.WriteAllTextAsync(requestPath, originalRequestJson, Encoding.UTF8);
        await File.WriteAllTextAsync(instructionsPath, "# Unsafe Proof Instructions", Encoding.UTF8);
        var instructionsResult = await service.ValidateAsync(projectRoot);
        if (instructionsResult.IsOk
            || !instructionsResult.Items.Any(item => item.Key.StartsWith("proof.instructions.", StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"Proof-check validation did not reject instructions missing P1.7.1 boundaries: {DescribeProofValidationErrors(instructionsResult)}");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(manifestPath, originalManifestJson, Encoding.UTF8);
        await File.WriteAllTextAsync(requestPath, originalRequestJson, Encoding.UTF8);
        await File.WriteAllTextAsync(instructionsPath, originalInstructions, Encoding.UTF8);
    }

    var restored = await service.ValidateAsync(projectRoot);
    if (!restored.IsOk)
    {
        failures.Add($"Proof-check package was not valid after path-safety restore: {DescribeProofValidationErrors(restored)}");
    }

    if (File.Exists(ProofCheckPackageService.GetPlannedCreatedFilePath(projectRoot))
        || File.Exists(ProofCheckPackageService.GetPlannedResultPath(projectRoot))
        || File.Exists(ProofCheckPackageService.GetPlannedReportPath(projectRoot))
        || File.Exists(indexPath))
    {
        failures.Add("Proof-check validation created a runtime proof/result/report file or output-site index.");
    }
}

static async Task CheckP172ApprovalGateAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    if (File.Exists(indexPath))
    {
        File.Delete(indexPath);
    }

    var service = new ApprovalGateService();
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "Future real execution approval gate",
        "User approval is required before any future real execution can proceed.",
        "This approval only records consent state and does not execute anything.");

    if (!string.Equals(request.SchemaVersion, ApprovalGateSchema.CurrentSchemaVersion, StringComparison.Ordinal)
        || !string.Equals(request.ProjectId, projectId, StringComparison.Ordinal)
        || string.IsNullOrWhiteSpace(request.ApprovalId)
        || !string.Equals(request.GateId, request.ApprovalId, StringComparison.Ordinal)
        || request.GateType != ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution
        || string.IsNullOrWhiteSpace(request.Purpose)
        || string.IsNullOrWhiteSpace(request.RequiredSummary)
        || string.IsNullOrWhiteSpace(request.RiskWarning))
    {
        failures.Add("ApprovalGateService did not create a request with the expected identity, type, purpose, summary, and risk fields.");
    }

    if (!request.CannotBeBypassedByAi)
    {
        failures.Add("ApprovalGateRequest did not force cannotBeBypassedByAi=true.");
    }

    var requestPath = ApprovalGateService.GetRequestPath(projectRoot, request.ApprovalId);
    var resultPath = ApprovalGateService.GetResultPath(projectRoot, request.ApprovalId);
    var validationJsonPath = ApprovalGateService.GetValidationReportJsonPath(projectRoot, request.ApprovalId);
    var validationMarkdownPath = ApprovalGateService.GetValidationReportMarkdownPath(projectRoot, request.ApprovalId);
    foreach (var requiredPath in new[] { requestPath, resultPath, validationJsonPath, validationMarkdownPath })
    {
        if (!File.Exists(requiredPath))
        {
            failures.Add($"ApprovalGateService did not write required approval file: {Path.GetFileName(requiredPath)}");
        }
    }

    var loadedRequest = await service.LoadRequestAsync(projectRoot, request.ApprovalId);
    var loadedResult = await service.LoadResultAsync(projectRoot, request.ApprovalId);
    if (loadedRequest.GateType != ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution
        || loadedResult.Decision != ApprovalDecision.Pending
        || !loadedResult.CannotBeBypassedByAi)
    {
        failures.Add("ApprovalGateService did not load the pending request/result state correctly.");
    }

    if (Path.IsPathRooted(loadedRequest.StoredRelativePath)
        || loadedRequest.StoredRelativePath.Contains("..", StringComparison.Ordinal)
        || !loadedRequest.StoredRelativePath.StartsWith(ApprovalGateSchema.ApprovalsRootRelativePath, StringComparison.Ordinal))
    {
        failures.Add($"Approval request stored an unsafe relative path: {loadedRequest.StoredRelativePath}");
    }

    var requestJson = await File.ReadAllTextAsync(requestPath);
    var resultJson = await File.ReadAllTextAsync(resultPath);
    if (!requestJson.Contains("\"gateType\": \"approvalRequiredBeforeRealCodexExecution\"", StringComparison.Ordinal)
        || !resultJson.Contains("\"decision\": \"pending\"", StringComparison.Ordinal)
        || resultJson.Contains("\"decision\": 0", StringComparison.Ordinal))
    {
        failures.Add("Approval gate request/result did not serialize enums as stable strings.");
    }

    var pendingValidation = await service.ValidateAsync(projectRoot, request.ApprovalId);
    if (pendingValidation.IsOk
        || pendingValidation.IsExecutable
        || !pendingValidation.IsBindingCurrent
        || pendingValidation.Items.Any(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
    {
        failures.Add($"Pending approval validation should be bound/current but not executable: {DescribeApprovalValidationErrors(pendingValidation)}");
    }

    var approvedRequest = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeProofCheck,
        "Future proof-check approval gate",
        "User approval is required before any future proof-check may proceed.",
        "This round only records approval metadata.");
    var approved = await service.ApproveAsync(projectRoot, approvedRequest.ApprovalId, "approved for self-check");
    if (approved.Decision != ApprovalDecision.Approved)
    {
        failures.Add("ApproveAsync did not set decision=Approved.");
    }

    var approvedValidation = await service.ValidateAsync(projectRoot, approvedRequest.ApprovalId);
    if (!approvedValidation.IsOk || !approvedValidation.IsExecutable || !approvedValidation.IsBindingCurrent)
    {
        failures.Add($"Approved approval did not validate as executable/current: {DescribeApprovalValidationErrors(approvedValidation)}");
    }

    await CheckP172TerminalTransitionAsync(
        projectRoot,
        service,
        ApprovalDecision.Rejected,
        request => service.RejectAsync(projectRoot, request.ApprovalId, "rejected in self-check"),
        failures);
    await CheckP172TerminalTransitionAsync(
        projectRoot,
        service,
        ApprovalDecision.Cancelled,
        request => service.CancelAsync(projectRoot, request.ApprovalId, "cancelled in self-check"),
        failures);
    await CheckP172TerminalTransitionAsync(
        projectRoot,
        service,
        ApprovalDecision.Expired,
        request => service.ExpireAsync(projectRoot, request.ApprovalId, "expired in self-check"),
        failures);
    await CheckP172TerminalTransitionAsync(
        projectRoot,
        service,
        ApprovalDecision.Superseded,
        request => service.SupersedeAsync(projectRoot, request.ApprovalId, "superseded in self-check"),
        failures);

    await CheckP172ApprovedTransitionAsync(projectRoot, service, ApprovalDecision.Superseded, failures);
    await CheckP172ApprovedTransitionAsync(projectRoot, service, ApprovalDecision.Expired, failures);
    await CheckP172ApprovedRejectBlockedAsync(projectRoot, service, failures);

    await CheckP172TaskHashInvalidationAsync(projectRoot, service, failures);
    await CheckP172InstructionsHashInvalidationAsync(projectRoot, service, failures);

    if (ApprovalArtifactsLeakSensitiveData(projectRoot))
    {
        failures.Add("Approval artifacts leaked a concrete local path or sensitive marker.");
    }

    CheckP172ApprovalGitIgnore(failures);

    if (File.Exists(indexPath))
    {
        failures.Add("Approval gate validation created output-site/current/index.html.");
        File.Delete(indexPath);
    }
}

static async Task CheckP172TerminalTransitionAsync(
    string projectRoot,
    ApprovalGateService service,
    ApprovalDecision expectedDecision,
    Func<ApprovalGateRequest, Task<ApprovalGateResult>> transition,
    List<string> failures)
{
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeUploadingAnything,
        $"Future {expectedDecision} transition approval gate",
        "User approval state transition must be explicit.",
        "This transition test records metadata only.");
    var result = await transition(request);
    if (result.Decision != expectedDecision)
    {
        failures.Add($"Approval transition did not set decision={expectedDecision}.");
    }

    try
    {
        await service.ApproveAsync(projectRoot, request.ApprovalId, "illegal approval after terminal state");
        failures.Add($"ApprovalGateService allowed {expectedDecision} -> Approved.");
    }
    catch (InvalidOperationException)
    {
    }

    var validation = await service.ValidateAsync(projectRoot, request.ApprovalId);
    if (validation.IsExecutable)
    {
        failures.Add($"Approval decision {expectedDecision} should block execution.");
    }
}

static async Task CheckP172ApprovedTransitionAsync(
    string projectRoot,
    ApprovalGateService service,
    ApprovalDecision expectedDecision,
    List<string> failures)
{
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeExportZip,
        $"Future approved to {expectedDecision} approval gate",
        "Approved approvals may only become superseded or expired.",
        "This transition test records metadata only.");
    await service.ApproveAsync(projectRoot, request.ApprovalId, "approved before terminal transition");
    var result = expectedDecision == ApprovalDecision.Superseded
        ? await service.SupersedeAsync(projectRoot, request.ApprovalId, "superseded after approval")
        : await service.ExpireAsync(projectRoot, request.ApprovalId, "expired after approval");
    if (result.Decision != expectedDecision)
    {
        failures.Add($"ApprovalGateService did not allow Approved -> {expectedDecision}.");
    }
}

static async Task CheckP172ApprovedRejectBlockedAsync(
    string projectRoot,
    ApprovalGateService service,
    List<string> failures)
{
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeWritingOutputSite,
        "Future output-site write approval gate",
        "Approved approval cannot later be rejected.",
        "This transition test records metadata only.");
    await service.ApproveAsync(projectRoot, request.ApprovalId, "approved before illegal reject");
    try
    {
        await service.RejectAsync(projectRoot, request.ApprovalId, "illegal reject after approval");
        failures.Add("ApprovalGateService allowed Approved -> Rejected.");
    }
    catch (InvalidOperationException)
    {
    }
}

static async Task CheckP172TaskHashInvalidationAsync(
    string projectRoot,
    ApprovalGateService service,
    List<string> failures)
{
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "Future task hash approval gate",
        "Approval must bind to the exact task package hash.",
        "Changing the task package must stale the approval.");
    var taskPath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var originalTaskJson = await File.ReadAllTextAsync(taskPath);
    try
    {
        var taskPackage = await ReadJsonAsync<CodexTaskPackage>(taskPath);
        if (taskPackage is null)
        {
            failures.Add("Could not deserialize task package for P1.7.2 stale-hash check.");
            return;
        }

        taskPackage.Warnings.Add("P1.7.2 task hash invalidation check.");
        await WriteJsonAsync(taskPath, taskPackage);
        var validation = await service.ValidateAsync(projectRoot, request.ApprovalId);
        if (validation.IsBindingCurrent
            || !HasApprovalValidationError(validation, "approval.binding.taskPackage.hashChanged"))
        {
            failures.Add($"Approval validation did not detect task package hash change: {DescribeApprovalValidationErrors(validation)}");
        }

        try
        {
            await service.ApproveAsync(projectRoot, request.ApprovalId, "should be blocked by task hash change");
            failures.Add("ApproveAsync allowed a stale task package binding.");
        }
        catch (InvalidOperationException)
        {
        }
    }
    finally
    {
        await File.WriteAllTextAsync(taskPath, originalTaskJson, Encoding.UTF8);
    }
}

static async Task CheckP172InstructionsHashInvalidationAsync(
    string projectRoot,
    ApprovalGateService service,
    List<string> failures)
{
    var request = await service.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "Future instructions hash approval gate",
        "Approval must bind to the exact instructions hash.",
        "Changing instructions must stale the approval.");
    var instructionsPath = CodexTaskPackageService.GetInstructionsPath(projectRoot);
    var originalInstructions = await File.ReadAllTextAsync(instructionsPath);
    try
    {
        await File.WriteAllTextAsync(
            instructionsPath,
            originalInstructions + Environment.NewLine + "P1.7.2 instructions hash invalidation check.",
            Encoding.UTF8);
        var validation = await service.ValidateAsync(projectRoot, request.ApprovalId);
        if (validation.IsBindingCurrent
            || !HasApprovalValidationError(validation, "approval.binding.instructions.hashChanged"))
        {
            failures.Add($"Approval validation did not detect instructions hash change: {DescribeApprovalValidationErrors(validation)}");
        }

        try
        {
            await service.ApproveAsync(projectRoot, request.ApprovalId, "should be blocked by instructions hash change");
            failures.Add("ApproveAsync allowed a stale instructions binding.");
        }
        catch (InvalidOperationException)
        {
        }
    }
    finally
    {
        await File.WriteAllTextAsync(instructionsPath, originalInstructions, Encoding.UTF8);
    }
}

static async Task CheckP173ExecutionPreconditionsAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    if (File.Exists(indexPath))
    {
        File.Delete(indexPath);
    }

    var service = new ExecutionPreconditionService();
    var approvalService = new ApprovalGateService();
    var taskService = new CodexTaskPackageService();
    var contentBuilder = new ConstructionPackageContentBuilderService();

    var enumJson = JsonSerializer.Serialize(ExecutionPreconditionDecision.Blocked, WrbJsonOptions.Default);
    if (!enumJson.Contains("\"blocked\"", StringComparison.Ordinal))
    {
        failures.Add("ExecutionPreconditionDecision did not serialize as a JSON string enum.");
    }

    await CheckP173MissingApprovalAsync(projectRoot, service, failures);
    await contentBuilder.BuildAsync(projectRoot);

    var baseline = await service.EvaluateAsync(projectRoot);
    if (!string.Equals(baseline.SchemaVersion, ExecutionPreconditionSchema.CurrentSchemaVersion, StringComparison.Ordinal)
        || !string.Equals(baseline.ProjectId, projectId, StringComparison.Ordinal)
        || string.IsNullOrWhiteSpace(baseline.ExecutionId))
    {
        failures.Add("ExecutionPreconditionService did not create a report with expected schema/project/execution identity.");
    }

    if (baseline.AllowsRealCodexExecution
        || baseline.ExecutesCodexCli
        || baseline.CallsOpenAiApi
        || baseline.CallsLocalModel
        || baseline.GeneratesWebsite
        || baseline.Decision != ExecutionPreconditionDecision.Blocked)
    {
        failures.Add("ExecutionPreconditionService default report did not keep real execution blocked and non-executing.");
    }

    var jsonPath = ExecutionPreconditionService.GetPreconditionsPath(projectRoot, baseline.ExecutionId);
    var markdownPath = ExecutionPreconditionService.GetPreconditionsMarkdownPath(projectRoot, baseline.ExecutionId);
    if (!File.Exists(jsonPath) || !File.Exists(markdownPath))
    {
        failures.Add("ExecutionPreconditionService did not write JSON and Markdown reports.");
    }
    else
    {
        var reportJson = await File.ReadAllTextAsync(jsonPath);
        var reportMarkdown = await File.ReadAllTextAsync(markdownPath);
        if (!reportJson.Contains("\"decision\": \"blocked\"", StringComparison.Ordinal)
            || reportJson.Contains("\"decision\": 0", StringComparison.Ordinal)
            || !reportJson.Contains("\"status\": \"notImplemented\"", StringComparison.Ordinal))
        {
            failures.Add("execution-preconditions.json did not serialize enums as stable strings.");
        }

        foreach (var requiredText in new[]
                 {
                     "# Execution Preconditions Report",
                     "Decision",
                     "AllowsRealCodexExecution",
                     "## Blocking Reasons",
                     "## Warnings",
                     "## Precondition Items"
                 })
        {
            if (!reportMarkdown.Contains(requiredText, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"execution-preconditions.md missing required section/text: {requiredText}");
            }
        }
    }

    if (Path.IsPathRooted(baseline.StoredRelativePath)
        || baseline.StoredRelativePath.Contains("..", StringComparison.Ordinal)
        || !baseline.StoredRelativePath.StartsWith(ExecutionPreconditionSchema.ExecutionRootRelativePath, StringComparison.Ordinal))
    {
        failures.Add($"Execution precondition stored path is not project-relative: {baseline.StoredRelativePath}");
    }

    var latest = await service.LoadLatestAsync(projectRoot);
    if (string.IsNullOrWhiteSpace(latest.ExecutionId))
    {
        failures.Add("ExecutionPreconditionService.LoadLatestAsync did not load the latest report.");
    }

    foreach (var requiredKey in new[]
             {
                 "readiness.preCodexDryRun.passed",
                 "dryRun.completed",
                 "proof.package.valid",
                 "proof.execution.passed",
                 "approval.execution.approved",
                 "rollback.safetySnapshot.available",
                 "sandbox.allowedWriteRoots.verified",
                 "sandbox.forbiddenRoots.verified",
                 "security.secretScan.clean",
                 "security.localPathScan.clean",
                 "outputSite.current.safe",
                 "codexWorkspace.safe",
                 "logs.writable",
                 "taskPackage.hashStable",
                 "context.freshness.valid",
                 "manualFallback.available",
                 "nonExecutionBoundary.enforced"
             })
    {
        if (!baseline.Items.Any(item => string.Equals(item.Key, requiredKey, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"Execution precondition report missing item: {requiredKey}");
        }
    }

    if (!HasExecutionItem(baseline, "proof.execution.passed", ExecutionPreconditionStatus.NotImplemented, blocks: true))
    {
        failures.Add("Execution precondition report did not block missing real proof execution as NotImplemented.");
    }

    var approvedRequest = await approvalService.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "P1.7.3 future execution approval",
        "Approval is required before any future real execution can proceed.",
        "This self-test approval records metadata only.");
    await approvalService.ApproveAsync(projectRoot, approvedRequest.ApprovalId, "approved for P1.7.3 aggregation self-test");

    var approvedReport = await service.EvaluateAsync(projectRoot);
    foreach (var expectedPassed in new[]
             {
                 "readiness.preCodexDryRun.passed",
                 "dryRun.completed",
                 "proof.package.valid",
                 "approval.execution.approved",
                 "rollback.safetySnapshot.available",
                 "sandbox.allowedWriteRoots.verified",
                 "sandbox.forbiddenRoots.verified",
                 "security.secretScan.clean",
                 "security.localPathScan.clean",
                 "outputSite.current.safe",
                 "codexWorkspace.safe",
                 "logs.writable",
                 "taskPackage.hashStable",
                 "context.freshness.valid",
                 "manualFallback.available",
                 "nonExecutionBoundary.enforced"
             })
    {
        if (!HasExecutionItem(approvedReport, expectedPassed, ExecutionPreconditionStatus.Passed, blocks: false))
        {
            failures.Add($"Execution precondition aggregation did not pass expected item {expectedPassed}: {DescribeExecutionReport(approvedReport)}");
        }
    }

    if (approvedReport.AllowsRealCodexExecution
        || approvedReport.Decision != ExecutionPreconditionDecision.Blocked
        || !HasExecutionItem(approvedReport, "proof.execution.passed", ExecutionPreconditionStatus.NotImplemented, blocks: true))
    {
        failures.Add("Approved aggregation should still block because real proof execution is not implemented.");
    }

    await CheckP173MissingReadinessAsync(projectRoot, service, contentBuilder, failures);
    await CheckP173MissingDryRunAsync(projectRoot, service, failures);
    await CheckP173MissingProofPackageAsync(projectRoot, service, failures);
    await CheckP173ApprovalBlockingAsync(projectRoot, service, approvalService, failures);
    await CheckP173SecretAndLocalPathBlockingAsync(projectRoot, service, taskService, contentBuilder, failures);

    var leak = DescribeExecutionReportLeaks(projectRoot);
    if (!string.IsNullOrWhiteSpace(leak))
    {
        failures.Add($"Execution precondition reports leaked a concrete local path or sensitive marker: {leak}");
    }

    CheckP173ExecutionGitIgnore(failures);

    if (File.Exists(indexPath))
    {
        failures.Add("ExecutionPreconditionService created output-site/current/index.html.");
        File.Delete(indexPath);
    }
}

static async Task CheckP180AlphaValidationProbeAsync(
    string projectRoot,
    string projectId,
    List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    if (!File.Exists(Path.Combine(sourceRoot, "WebRebuildRecorder.App", "Core", "ProjectSystem", "AlphaValidationProbe.cs")))
    {
        failures.Add("AlphaValidationProbe.cs was not found in the project-system source folder.");
    }

    if (!File.Exists(Path.Combine(sourceRoot, "WebRebuildRecorder.App", "Core", "ProjectSystem", "AlphaValidationProbeService.cs")))
    {
        failures.Add("AlphaValidationProbeService.cs was not found in the project-system source folder.");
    }

    var indexPath = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent, "index.html");
    if (File.Exists(indexPath))
    {
        File.Delete(indexPath);
    }

    var service = new AlphaValidationProbeService();
    var enumJson = JsonSerializer.Serialize(AlphaValidationStepStatus.NotImplemented, WrbJsonOptions.Default);
    if (!enumJson.Contains("\"notImplemented\"", StringComparison.Ordinal))
    {
        failures.Add("AlphaValidationStepStatus did not serialize as a JSON string enum.");
    }

    var report = await service.RunAsync(projectRoot);
    if (!string.Equals(report.SchemaVersion, AlphaValidationProbeSchema.CurrentSchemaVersion, StringComparison.Ordinal)
        || !string.Equals(report.ProjectId, projectId, StringComparison.Ordinal)
        || string.IsNullOrWhiteSpace(report.ProbeId))
    {
        failures.Add("AlphaValidationProbeService did not create a report with expected schema/project/probe identity.");
    }

    if (!report.IsUsableAsAlphaEvidence
        || report.ExecutesCodexCli
        || report.CallsOpenAiApi
        || report.CallsLocalModel
        || report.GeneratesWebsite)
    {
        failures.Add("Alpha validation report did not preserve usable alpha evidence with non-execution flags false.");
    }

    var jsonPath = AlphaValidationProbeService.GetReportJsonPath(projectRoot, report.ProbeId);
    var markdownPath = AlphaValidationProbeService.GetReportMarkdownPath(projectRoot, report.ProbeId);
    if (!File.Exists(jsonPath) || !File.Exists(markdownPath))
    {
        failures.Add("AlphaValidationProbeService.RunAsync did not write JSON and Markdown reports.");
    }
    else
    {
        var reportJson = await File.ReadAllTextAsync(jsonPath);
        var reportMarkdown = await File.ReadAllTextAsync(markdownPath);
        if (!reportJson.Contains("\"status\": \"notImplemented\"", StringComparison.Ordinal)
            || reportJson.Contains("\"status\": 4", StringComparison.Ordinal))
        {
            failures.Add("alpha-validation-report.json did not serialize step enums as stable strings.");
        }

        foreach (var requiredText in new[]
                 {
                     "# Alpha Validation Probe Report",
                     "ProjectId",
                     "ProbeId",
                     "CreatedAt",
                     "IsUsableAsAlphaEvidence",
                     "## Summary",
                     "## Steps",
                     "## Blocking Reasons",
                     "## Warnings",
                     "## Next Recommended Action",
                     "This probe does not execute Codex CLI.",
                     "This probe does not run any codex command.",
                     "This probe does not call OpenAI API.",
                     "This probe does not call local model engines.",
                     "This probe does not generate a website.",
                     "This probe does not write output-site/current/index.html."
                 })
        {
            if (!reportMarkdown.Contains(requiredText, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"alpha-validation-report.md missing required text: {requiredText}");
            }
        }
    }

    if (Path.IsPathRooted(report.StoredRelativePath)
        || report.StoredRelativePath.Contains("..", StringComparison.Ordinal)
        || !report.StoredRelativePath.StartsWith(AlphaValidationProbeSchema.AlphaValidationRootRelativePath, StringComparison.Ordinal))
    {
        failures.Add($"Alpha validation stored path is not project-relative: {report.StoredRelativePath}");
    }

    var latest = await service.LoadLatestAsync(projectRoot);
    if (string.IsNullOrWhiteSpace(latest.ProbeId))
    {
        failures.Add("AlphaValidationProbeService.LoadLatestAsync did not load the latest report.");
    }

    foreach (var requiredKey in new[]
             {
                 "project.v2Structure.exists",
                 "project.manifest.exists",
                 "assets.manifest.exists",
                 "theme.exists",
                 "contentMap.exists",
                 "observation.package.exists",
                 "construction.package.exists",
                 "task.package.exists",
                 "instructions.exists",
                 "readiness.preCodexDryRun.runs",
                 "dryRun.runsOrLatestExists",
                 "proof.package.valid",
                 "approval.requestOrResult.exists",
                 "executionPrecondition.runs",
                 "executionPrecondition.blocksRealExecution",
                 "manualFallback.evidenceExists",
                 "runtimeArtifacts.ignored",
                 "nonExecutionBoundary.enforced"
             })
    {
        if (!report.Steps.Any(step => string.Equals(step.Key, requiredKey, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"Alpha validation report missing step: {requiredKey}");
        }
    }

    if (!HasAlphaStep(report, "executionPrecondition.blocksRealExecution", AlphaValidationStepStatus.Passed)
        || !HasAlphaStep(report, "nonExecutionBoundary.enforced", AlphaValidationStepStatus.Passed))
    {
        failures.Add("Alpha validation did not record execution precondition blocking as valid alpha evidence.");
    }

    foreach (var expectedStatus in new[]
             {
                 AlphaValidationStepStatus.Passed,
                 AlphaValidationStepStatus.Warning,
                 AlphaValidationStepStatus.NotImplemented
             })
    {
        if (!report.Steps.Any(step => step.Status == expectedStatus))
        {
            failures.Add($"Alpha validation report missing expected status: {expectedStatus}.");
        }
    }

    await CheckP180BlockedButExplainableAsync(projectRoot, service, failures);

    var leak = DescribeAlphaValidationReportLeaks(projectRoot);
    if (!string.IsNullOrWhiteSpace(leak))
    {
        failures.Add($"Alpha validation reports leaked a concrete local path or sensitive marker: {leak}");
    }

    CheckP180AlphaGitIgnore(failures);
    CheckP180NoForbiddenScopeDiff(failures);

    if (File.Exists(indexPath))
    {
        failures.Add("AlphaValidationProbeService created output-site/current/index.html.");
        File.Delete(indexPath);
    }
}

static async Task CheckP180BlockedButExplainableAsync(
    string projectRoot,
    AlphaValidationProbeService service,
    List<string> failures)
{
    var packagePath = Path.Combine(projectRoot, ConstructionPackageSchema.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    var holdPath = packagePath + ".p180hold";
    if (File.Exists(holdPath))
    {
        File.Delete(holdPath);
    }

    if (!File.Exists(packagePath))
    {
        failures.Add("Cannot run P1.8-0 blocked-but-explainable check because construction-package.json is missing before the check.");
        return;
    }

    File.Move(packagePath, holdPath);
    try
    {
        var blockedReport = await service.RunAsync(projectRoot);
        if (!blockedReport.IsUsableAsAlphaEvidence)
        {
            failures.Add("Alpha validation blocked scenario was not usable as explainable alpha evidence.");
        }

        if (blockedReport.BlockingReasons.Count == 0
            || !HasAlphaStep(blockedReport, "construction.package.exists", AlphaValidationStepStatus.Blocked)
            || !blockedReport.Steps.Any(step => step.Status == AlphaValidationStepStatus.Blocked)
            || !blockedReport.Steps.Any(step => step.Status == AlphaValidationStepStatus.Passed)
            || !blockedReport.Steps.Any(step => step.Status == AlphaValidationStepStatus.Warning)
            || !blockedReport.Steps.Any(step => step.Status == AlphaValidationStepStatus.NotImplemented))
        {
            failures.Add($"Alpha validation did not record missing package as blocked-but-explainable evidence: {DescribeAlphaReport(blockedReport)}");
        }

        if (!HasAlphaStep(blockedReport, "executionPrecondition.blocksRealExecution", AlphaValidationStepStatus.Passed))
        {
            failures.Add("Alpha validation blocked scenario did not preserve execution precondition blocking as evidence.");
        }
    }
    finally
    {
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        File.Move(holdPath, packagePath);
    }
}

static bool HasAlphaStep(
    AlphaValidationProbeReport report,
    string key,
    AlphaValidationStepStatus status)
{
    return report.Steps.Any(step =>
        string.Equals(step.Key, key, StringComparison.OrdinalIgnoreCase)
        && step.Status == status);
}

static string DescribeAlphaReport(AlphaValidationProbeReport report)
{
    return string.Join("; ", report.Steps.Select(step => $"{step.Key}:{step.Status}:{step.Message}"));
}

static async Task CheckP173MissingApprovalAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    List<string> failures)
{
    var approvalsRoot = ApprovalGateService.GetApprovalsRootPath(projectRoot);
    var holdRoot = approvalsRoot + ".p173hold";
    if (Directory.Exists(holdRoot))
    {
        Directory.Delete(holdRoot, recursive: true);
    }

    var moved = Directory.Exists(approvalsRoot);
    if (moved)
    {
        Directory.Move(approvalsRoot, holdRoot);
    }

    try
    {
        var report = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(report, "approval.execution.approved", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block missing execution approval.");
        }
    }
    finally
    {
        if (moved)
        {
            if (Directory.Exists(approvalsRoot))
            {
                Directory.Delete(approvalsRoot, recursive: true);
            }

            Directory.Move(holdRoot, approvalsRoot);
        }
    }
}

static async Task CheckP173MissingReadinessAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var packageIndexPath = Path.Combine(
        projectRoot,
        ConstructionPackageContextSchema.PackageIndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var original = await File.ReadAllTextAsync(packageIndexPath);
    File.Delete(packageIndexPath);
    try
    {
        var report = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(report, "readiness.preCodexDryRun.passed", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block missing/stale readiness inputs.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(packageIndexPath, original, Encoding.UTF8);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static async Task CheckP173MissingDryRunAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    List<string> failures)
{
    var dryRunsRoot = Path.Combine(projectRoot, CodexDryRunSchema.DryRunsRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
    var holdRoot = dryRunsRoot + ".p173hold";
    if (Directory.Exists(holdRoot))
    {
        Directory.Delete(holdRoot, recursive: true);
    }

    var moved = Directory.Exists(dryRunsRoot);
    if (moved)
    {
        Directory.Move(dryRunsRoot, holdRoot);
    }

    try
    {
        var report = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(report, "dryRun.completed", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block missing dry-run result.");
        }
    }
    finally
    {
        if (moved)
        {
            if (Directory.Exists(dryRunsRoot))
            {
                Directory.Delete(dryRunsRoot, recursive: true);
            }

            Directory.Move(holdRoot, dryRunsRoot);
        }
    }
}

static async Task CheckP173MissingProofPackageAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    List<string> failures)
{
    var proofRoot = ProofCheckPackageService.GetProofRootPath(projectRoot);
    var holdRoot = proofRoot + ".p173hold";
    if (Directory.Exists(holdRoot))
    {
        Directory.Delete(holdRoot, recursive: true);
    }

    var moved = Directory.Exists(proofRoot);
    if (moved)
    {
        Directory.Move(proofRoot, holdRoot);
    }

    try
    {
        var report = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(report, "proof.package.valid", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block missing proof-check package.");
        }
    }
    finally
    {
        if (Directory.Exists(proofRoot))
        {
            Directory.Delete(proofRoot, recursive: true);
        }

        if (moved)
        {
            Directory.Move(holdRoot, proofRoot);
        }
    }
}

static async Task CheckP173ApprovalBlockingAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    ApprovalGateService approvalService,
    List<string> failures)
{
    var pending = await approvalService.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "P1.7.3 pending approval",
        "Pending approval must not authorize execution.",
        "This approval remains pending for self-test.");
    var pendingReport = await service.EvaluateAsync(projectRoot);
    if (!HasExecutionItem(pendingReport, "approval.execution.approved", ExecutionPreconditionStatus.Blocked, blocks: true))
    {
        failures.Add($"Execution precondition service did not block pending approval {pending.ApprovalId}.");
    }

    var rejected = await approvalService.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "P1.7.3 rejected approval",
        "Rejected approval must not authorize execution.",
        "This approval is rejected for self-test.");
    await approvalService.RejectAsync(projectRoot, rejected.ApprovalId, "rejected for P1.7.3 self-test");
    var rejectedReport = await service.EvaluateAsync(projectRoot);
    if (!HasExecutionItem(rejectedReport, "approval.execution.approved", ExecutionPreconditionStatus.Blocked, blocks: true))
    {
        failures.Add("Execution precondition service did not block rejected approval.");
    }

    var stale = await approvalService.CreatePendingAsync(
        projectRoot,
        ApprovalGateType.ApprovalRequiredBeforeRealCodexExecution,
        "P1.7.3 stale approval",
        "Stale approval must not authorize execution.",
        "This approval is made stale by changing task package.");
    await approvalService.ApproveAsync(projectRoot, stale.ApprovalId, "approved before stale hash self-test");
    var taskPath = CodexTaskPackageService.GetPackagePath(projectRoot);
    var originalTaskJson = await File.ReadAllTextAsync(taskPath);
    try
    {
        var taskPackage = await ReadJsonAsync<CodexTaskPackage>(taskPath);
        if (taskPackage is null)
        {
            failures.Add("Could not deserialize task package for P1.7.3 stale approval check.");
            return;
        }

        taskPackage.Warnings.Add("P1.7.3 stale approval hash self-test.");
        await WriteJsonAsync(taskPath, taskPackage);
        var staleReport = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(staleReport, "approval.execution.approved", ExecutionPreconditionStatus.Blocked, blocks: true)
            || !HasExecutionItem(staleReport, "taskPackage.hashStable", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block approved but stale approval binding.");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(taskPath, originalTaskJson, Encoding.UTF8);
    }
}

static async Task CheckP173SecretAndLocalPathBlockingAsync(
    string projectRoot,
    ExecutionPreconditionService service,
    CodexTaskPackageService taskService,
    ConstructionPackageContentBuilderService contentBuilder,
    List<string> failures)
{
    var instructionsPath = CodexTaskPackageService.GetInstructionsPath(projectRoot);
    var originalInstructions = await File.ReadAllTextAsync(instructionsPath);
    await File.WriteAllTextAsync(
        instructionsPath,
        originalInstructions + Environment.NewLine + @"OPENAI_API_KEY=sk-test C:\Users\test\.ssh token=password",
        Encoding.UTF8);

    try
    {
        var report = await service.EvaluateAsync(projectRoot);
        if (!HasExecutionItem(report, "security.secretScan.clean", ExecutionPreconditionStatus.Blocked, blocks: true)
            || !HasExecutionItem(report, "security.localPathScan.clean", ExecutionPreconditionStatus.Blocked, blocks: true))
        {
            failures.Add("Execution precondition service did not block secret and local path markers.");
        }

        var leak = DescribeExecutionReportLeaks(projectRoot);
        if (!string.IsNullOrWhiteSpace(leak))
        {
            failures.Add($"Execution precondition sanitizer did not remove sensitive/local markers from reports: {leak}");
        }
    }
    finally
    {
        await File.WriteAllTextAsync(instructionsPath, originalInstructions, Encoding.UTF8);
        var cleanTask = await taskService.LoadAsync(projectRoot);
        await taskService.WriteInstructionsAsync(projectRoot, cleanTask);
        await contentBuilder.BuildAsync(projectRoot);
    }
}

static bool HasExecutionItem(
    ExecutionPreconditionReport report,
    string key,
    ExecutionPreconditionStatus status,
    bool blocks)
{
    return report.Items.Any(item =>
        string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
        && item.Status == status
        && item.BlocksExecution == blocks);
}

static string DescribeExecutionReport(ExecutionPreconditionReport report)
{
    return string.Join("; ", report.Items.Select(item => $"{item.Key}:{item.Status}:{item.BlocksExecution}:{item.Message}"));
}

static bool DryRunArtifactLeaksSensitiveData(
    string projectRoot,
    params string[] paths)
{
    foreach (var path in paths)
    {
        if (!File.Exists(path))
        {
            continue;
        }

        var content = File.ReadAllText(path);
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
            || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase)
            || content.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || content.Contains(".ssh", StringComparison.OrdinalIgnoreCase)
            || content.Contains(".codex", StringComparison.OrdinalIgnoreCase)
            || content.Contains(".openai", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" token", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" password", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" secret", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" cookie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static bool ReportLeaksSensitiveData(string content, string projectRoot)
{
    return content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
           || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
           || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
           || content.Contains(@"\.ssh", StringComparison.OrdinalIgnoreCase)
           || content.Contains("/home/", StringComparison.OrdinalIgnoreCase);
}

static string DescribeReadinessBlockers(ConstructionReadinessResult result)
{
    return string.Join("; ", result.Items
        .Where(item => item.BlocksExecution)
        .Select(item => $"{item.Key}:{item.FailureCategory}:{item.RelativePath}:{item.Message}"));
}

static async Task<ProjectSnapshotManifest> CreateManualSnapshotAsync(
    string projectRoot,
    string snapshotId,
    string projectId,
    IReadOnlyList<(string RelativePath, string Content, string? Sha256Override)> files)
{
    var manifest = new ProjectSnapshotManifest
    {
        SchemaVersion = ProjectSnapshotSchema.CurrentSchemaVersion,
        SnapshotId = snapshotId,
        ProjectId = projectId,
        CreatedAt = DateTimeOffset.UtcNow,
        Reason = $"manual self-test {snapshotId}"
    };

    var snapshotRoot = Path.Combine(projectRoot, ProjectSnapshotSchema.SnapshotRootRelativePath, snapshotId);
    Directory.CreateDirectory(snapshotRoot);
    foreach (var file in files)
    {
        var fullPath = Path.Combine(snapshotRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, file.Content, Encoding.UTF8);
        manifest.Files.Add(new SnapshotFileItem
        {
            RelativePath = file.RelativePath,
            Sha256 = file.Sha256Override ?? ComputeSha256ForTest(fullPath),
            SizeBytes = new FileInfo(fullPath).Length
        });
    }

    await WriteManualSnapshotManifestAsync(projectRoot, manifest);
    return manifest;
}

static async Task WriteManualSnapshotManifestAsync(
    string projectRoot,
    ProjectSnapshotManifest manifest)
{
    var snapshotRoot = Path.Combine(projectRoot, ProjectSnapshotSchema.SnapshotRootRelativePath, manifest.SnapshotId);
    Directory.CreateDirectory(snapshotRoot);
    var manifestPath = Path.Combine(snapshotRoot, ProjectSnapshotSchema.ManifestFileName);
    await WriteJsonAsync(manifestPath, manifest);
}

static string DescribeSnapshotValidationErrors(SnapshotValidationResult result)
{
    return string.Join("; ", result.Items
        .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
        .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
}

static void CheckFoundationWorkflow(List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    var parentRoot = Directory.GetParent(sourceRoot)?.FullName ?? string.Empty;
    var workflowCandidates = new[]
    {
        Path.Combine(sourceRoot, ".github", "workflows", "webrebuildrecorder-foundation.yml"),
        Path.Combine(parentRoot, ".github", "workflows", "webrebuildrecorder-foundation.yml")
    };

    var workflowPath = workflowCandidates.FirstOrDefault(File.Exists);
    if (string.IsNullOrWhiteSpace(workflowPath))
    {
        failures.Add("GitHub Actions foundation workflow file was not found.");
        return;
    }

    var workflow = File.ReadAllText(workflowPath);
    foreach (var required in new[]
             {
                 "workflow_dispatch",
                 "windows-latest",
                 "timeout-minutes: 15",
                 "dotnet build WebRebuildRecorder.slnx",
                 "WebRebuildRecorder.FoundationSelfTest"
             })
    {
        if (!workflow.Contains(required, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"GitHub Actions foundation workflow missing required entry: {required}");
        }
    }
}

static string ComputeSha256ForTest(string fullPath)
{
    using var stream = File.OpenRead(fullPath);
    var hash = System.Security.Cryptography.SHA256.HashData(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static ObservationPackageManifest CloneObservationPackage(ObservationPackageManifest manifest)
{
    var json = JsonSerializer.Serialize(manifest, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<ObservationPackageManifest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone observation package.");
}

static CodexTaskPackage CloneTaskPackage(CodexTaskPackage package)
{
    var json = JsonSerializer.Serialize(package, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<CodexTaskPackage>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone task package.");
}

static AssetsManifest CloneAssetsManifest(AssetsManifest manifest)
{
    var json = JsonSerializer.Serialize(manifest, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<AssetsManifest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone assets manifest.");
}

static ProofCheckPackageManifest CloneProofManifest(ProofCheckPackageManifest manifest)
{
    var json = JsonSerializer.Serialize(manifest, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<ProofCheckPackageManifest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone proof-check manifest.");
}

static ProofCheckRequest CloneProofRequest(ProofCheckRequest request)
{
    var json = JsonSerializer.Serialize(request, WrbJsonOptions.Default);
    return JsonSerializer.Deserialize<ProofCheckRequest>(json, WrbJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to clone proof-check request.");
}

static async Task WriteJsonAsync<T>(string path, T value)
{
    await using var stream = File.Create(path);
    await JsonSerializer.SerializeAsync(stream, value, WrbJsonOptions.Default);
}

static async Task<T?> ReadJsonAsync<T>(string path)
{
    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<T>(stream, WrbJsonOptions.Default);
}

static string DescribeValidationErrors(PackageValidationResult result)
{
    return string.Join("; ", result.Items
        .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
        .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
}

static string DescribeProofValidationErrors(ProofCheckValidationResult result)
{
    return string.Join("; ", result.Items
        .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
        .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
}

static bool HasProofValidationError(ProofCheckValidationResult result, string key)
{
    return result.Items.Any(item =>
        string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

static string DescribeApprovalValidationErrors(ApprovalGateValidationResult result)
{
    return string.Join("; ", result.Items
        .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
        .Select(item => $"{item.Key}:{item.RelativePath}:{item.Message}"));
}

static bool HasApprovalValidationError(ApprovalGateValidationResult result, string key)
{
    return result.Items.Any(item =>
        string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

static bool ProofReportLeaksConcreteLocalPath(
    string projectRoot,
    params string[] paths)
{
    foreach (var path in paths)
    {
        if (!File.Exists(path))
        {
            continue;
        }

        var content = File.ReadAllText(path);
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
            || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase)
            || content.Contains("/home/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static bool ApprovalArtifactsLeakSensitiveData(string projectRoot)
{
    var approvalsRoot = ApprovalGateService.GetApprovalsRootPath(projectRoot);
    if (!Directory.Exists(approvalsRoot))
    {
        return true;
    }

    foreach (var path in Directory.EnumerateFiles(approvalsRoot, "*", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(path);
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
            || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase)
            || content.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || ContainsCredentialDirectoryMarker(content)
            || content.Contains(" token", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" password", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" secret", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" cookie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static string DescribeExecutionReportLeaks(string projectRoot)
{
    var executionRoot = Path.Combine(
        projectRoot,
        ExecutionPreconditionSchema.ExecutionRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
    if (!Directory.Exists(executionRoot))
    {
        return "execution root missing";
    }

    foreach (var path in Directory.EnumerateFiles(executionRoot, "*", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(path);
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
            || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase)
            || content.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || ContainsCredentialDirectoryMarker(content)
            || content.Contains("token=", StringComparison.OrdinalIgnoreCase)
            || content.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || content.Contains("cookie=", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(projectRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var marker = content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase) ? "projectRoot"
                : content.Contains("sk-test", StringComparison.OrdinalIgnoreCase) ? "sk-test"
                : content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase) ? "OPENAI_API_KEY"
                : content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase) ? "C:\\Users"
                : content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase) ? "E:\\"
                : content.Contains("/home/", StringComparison.OrdinalIgnoreCase) ? "/home/"
                : ContainsCredentialDirectoryMarker(content) ? "credential-dir"
                : content.Contains("token=", StringComparison.OrdinalIgnoreCase) ? "token="
                : content.Contains("password=", StringComparison.OrdinalIgnoreCase) ? "password="
                : content.Contains("cookie=", StringComparison.OrdinalIgnoreCase) ? "cookie="
                : "unknown";
            var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var snippet = markerIndex < 0
                ? string.Empty
                : content.Substring(
                        Math.Max(0, markerIndex - 40),
                        Math.Min(120, content.Length - Math.Max(0, markerIndex - 40)))
                    .ReplaceLineEndings(" ");
            return $"{relativePath}:{marker}:{snippet}";
        }
    }

    return string.Empty;
}

static string DescribeAlphaValidationReportLeaks(string projectRoot)
{
    var alphaRoot = Path.Combine(
        projectRoot,
        AlphaValidationProbeSchema.AlphaValidationRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
    if (!Directory.Exists(alphaRoot))
    {
        return "alpha validation root missing";
    }

    foreach (var path in Directory.EnumerateFiles(alphaRoot, "*", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(path);
        if (content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase)
            || content.Contains("sk-test", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase)
            || content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase)
            || content.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || ContainsCredentialDirectoryMarker(content)
            || content.Contains("token=", StringComparison.OrdinalIgnoreCase)
            || content.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || content.Contains("cookie=", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(projectRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var marker = content.Contains(projectRoot, StringComparison.OrdinalIgnoreCase) ? "projectRoot"
                : content.Contains("sk-test", StringComparison.OrdinalIgnoreCase) ? "sk-test"
                : content.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase) ? "OPENAI_API_KEY"
                : content.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase) ? "C:\\Users"
                : content.Contains(@"E:\", StringComparison.OrdinalIgnoreCase) ? "E:\\"
                : content.Contains("/home/", StringComparison.OrdinalIgnoreCase) ? "/home/"
                : ContainsCredentialDirectoryMarker(content) ? "credential-dir"
                : content.Contains("token=", StringComparison.OrdinalIgnoreCase) ? "token="
                : content.Contains("password=", StringComparison.OrdinalIgnoreCase) ? "password="
                : content.Contains("cookie=", StringComparison.OrdinalIgnoreCase) ? "cookie="
                : "unknown";
            var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var snippet = markerIndex < 0
                ? string.Empty
                : content.Substring(
                        Math.Max(0, markerIndex - 40),
                        Math.Min(120, content.Length - Math.Max(0, markerIndex - 40)))
                    .ReplaceLineEndings(" ");
            return $"{relativePath}:{marker}:{snippet}";
        }
    }

    return string.Empty;
}

static void CheckP180AlphaGitIgnore(List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    var projectIgnorePath = Path.Combine(sourceRoot, ".gitignore");
    var repositoryIgnorePath = Path.Combine(Directory.GetParent(sourceRoot)?.FullName ?? string.Empty, ".gitignore");

    if (!File.Exists(projectIgnorePath)
        || !File.ReadAllText(projectIgnorePath).Contains("codex-task/alpha-validation", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Project .gitignore does not ignore codex-task/alpha-validation runtime artifacts.");
    }

    if (File.Exists(repositoryIgnorePath)
        && !File.ReadAllText(repositoryIgnorePath).Contains("codex-task/alpha-validation", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Repository .gitignore does not ignore WebRebuildRecorder codex-task/alpha-validation runtime artifacts.");
    }
}

static void CheckP180NoForbiddenScopeDiff(List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    var repositoryRoot = Directory.GetParent(sourceRoot)?.FullName ?? string.Empty;
    var currentTaskPath = Path.Combine(sourceRoot, "CURRENT_TASK.md");
    var isP2A0PreviewShell = File.Exists(currentTaskPath)
        && File.ReadAllText(currentTaskPath).Contains("P2A-0 WebView2 Preview Shell", StringComparison.Ordinal);
    var isP2A01DetachedPreview = File.Exists(currentTaskPath)
        && File.ReadAllText(currentTaskPath).Contains("P2A-0.1 Detached WebView2 Preview Window", StringComparison.Ordinal);
    var p2A0AllowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WebRebuildRecorder/WebRebuildRecorder.App/WebRebuildRecorder.App.csproj",
        "WebRebuildRecorder/WebRebuildRecorder.App/MainWindow.xaml",
        "WebRebuildRecorder/WebRebuildRecorder.App/MainWindow.xaml.cs"
    };
    var p2A01AllowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WebRebuildRecorder/WebRebuildRecorder.App/MainWindow.xaml",
        "WebRebuildRecorder/WebRebuildRecorder.App/MainWindow.xaml.cs",
        "WebRebuildRecorder/WebRebuildRecorder.App/Views/DetachedPreviewWindow.xaml",
        "WebRebuildRecorder/WebRebuildRecorder.App/Views/DetachedPreviewWindow.xaml.cs"
    };

    if (string.IsNullOrWhiteSpace(repositoryRoot)
        || !Directory.Exists(Path.Combine(repositoryRoot, ".git")))
    {
        return;
    }

    try
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff --name-only HEAD -- WebRebuildRecorder",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000))
        {
            process.Kill(entireProcessTree: true);
            failures.Add("Could not verify P1.8-0 forbidden UI/WebView2 scope diff because git diff timed out.");
            return;
        }

        if (process.ExitCode != 0)
        {
            failures.Add($"Could not verify P1.8-0 forbidden UI/WebView2 scope diff: {error.Trim()}");
            return;
        }

        var forbidden = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .Where(path =>
                !((isP2A0PreviewShell && p2A0AllowedFiles.Contains(path))
                    || (isP2A01DetachedPreview && p2A01AllowedFiles.Contains(path)))
                && (path.Contains("WebRebuildRecorder.App/Views/", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("MainWindow.xaml", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("MainWindow.xaml.cs", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("SourceSnapshot", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("ProposalPreview", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (forbidden.Count != 0)
        {
            failures.Add($"P1.8-0 changed forbidden UI/WebView2/SourceSnapshot/ProposalPreview scope files: {string.Join(", ", forbidden)}");
        }
    }
    catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
    {
        failures.Add($"Could not verify P1.8-0 forbidden UI/WebView2 scope diff: {ex.Message}");
    }
}

static void CheckP172ApprovalGitIgnore(List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    var projectIgnorePath = Path.Combine(sourceRoot, ".gitignore");
    var repositoryIgnorePath = Path.Combine(Directory.GetParent(sourceRoot)?.FullName ?? string.Empty, ".gitignore");

    if (!File.Exists(projectIgnorePath)
        || !File.ReadAllText(projectIgnorePath).Contains("codex-task/approvals", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Project .gitignore does not ignore codex-task/approvals runtime artifacts.");
    }

    if (File.Exists(repositoryIgnorePath)
        && !File.ReadAllText(repositoryIgnorePath).Contains("codex-task/approvals", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Repository .gitignore does not ignore WebRebuildRecorder codex-task/approvals runtime artifacts.");
    }
}

static bool ContainsCredentialDirectoryMarker(string content)
{
    foreach (var marker in new[] { ".ssh", ".codex", ".openai" })
    {
        var index = 0;
        while (index < content.Length)
        {
            index = content.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            var nextIndex = index + marker.Length;
            if (nextIndex >= content.Length || IsCredentialMarkerBoundary(content[nextIndex]))
            {
                return true;
            }

            index = nextIndex;
        }
    }

    return false;
}

static bool IsCredentialMarkerBoundary(char value)
{
    return value is '/' or '\\' or ' ' or '.' or ':' or ';' or '"' or '\'' or '`' or ',' or ')' or ']';
}

static void CheckP173ExecutionGitIgnore(List<string> failures)
{
    var sourceRoot = FindSourceRoot();
    var projectIgnorePath = Path.Combine(sourceRoot, ".gitignore");
    var repositoryIgnorePath = Path.Combine(Directory.GetParent(sourceRoot)?.FullName ?? string.Empty, ".gitignore");

    if (!File.Exists(projectIgnorePath)
        || !File.ReadAllText(projectIgnorePath).Contains("codex-task/execution", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Project .gitignore does not ignore codex-task/execution runtime artifacts.");
    }

    if (File.Exists(repositoryIgnorePath)
        && !File.ReadAllText(repositoryIgnorePath).Contains("codex-task/execution", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Repository .gitignore does not ignore WebRebuildRecorder codex-task/execution runtime artifacts.");
    }
}

static string FindSourceRoot()
{
    var directory = new DirectoryInfo(Environment.CurrentDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "WebRebuildRecorder.slnx")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return string.Empty;
}

static void TryDeleteTempRoot(string tempRoot)
{
    var fullTempRoot = Path.GetFullPath(tempRoot);
    var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WebRebuildRecorderFoundationSelfTest"));
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    if (fullTempRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, comparison)
        && Directory.Exists(fullTempRoot))
    {
        Directory.Delete(fullTempRoot, recursive: true);
    }
}
