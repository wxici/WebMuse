using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface ICodexTaskPackageService
{
    Task<CodexTaskPackage> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default);

    Task<CodexTaskPackage> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        CodexTaskPackage package,
        CancellationToken cancellationToken = default);

    Task<string> WriteInstructionsAsync(
        string projectRoot,
        CodexTaskPackage package,
        CancellationToken cancellationToken = default);
}

public sealed class CodexTaskPackageService : ICodexTaskPackageService
{
    public async Task<CodexTaskPackage> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default)
    {
        ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(projectRoot, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var package = new CodexTaskPackage
        {
            SchemaVersion = CodexTaskPackageSchema.CurrentSchemaVersion,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? identity.ProjectId : projectId.Trim(),
            TaskPackageId = CreatePackageId("task", now),
            CreatedAt = now,
            Instruction = new CodexTaskInstruction
            {
                Title = "Static branded website reconstruction scaffold",
                Goal = "Use the construction package and project manifests to prepare a future static website generation task.",
                PromptMarkdown = BuildPromptMarkdown()
            },
            Sandbox = new CodexTaskSandbox
            {
                WorkspaceRelativePath = ProjectDirectoryV2.CodexWorkspace,
                AllowedWriteRoots =
                [
                    ProjectDirectoryV2.CodexWorkspace,
                    ProjectDirectoryV2.OutputCurrent
                ],
                ForbiddenRoots =
                [
                    ".git",
                    "sourceRepositoryRoot",
                    "userCredentialDirectories",
                    "systemDirectories",
                    "softwareInstallDirectories"
                ]
            },
            InputFiles = CreateDefaultInputs(),
            ExpectedOutputs =
            [
                new()
                {
                    RelativePath = $"{ProjectDirectoryV2.OutputCurrent}/index.html",
                    Description = "Future generated static site entry point."
                }
            ],
            ProhibitedActions =
            [
                "Do not execute Codex CLI.",
                "Do not run Codex CLI in this scaffold round.",
                "Do not call OpenAI API.",
                "Do not call OpenAI APIs.",
                "Do not modify project source files.",
                "Only write codex-workspace/output-site/current.",
                "Do not write .git, system directories, user credential directories, or software installation directories.",
                "Do not write outside codex-workspace/ or output-site/current/.",
                "Do not write .git.",
                "Do not copy reference-site copyrighted assets.",
                "Do not copy reference-site proprietary assets by default.",
                "Do not describe or implement the output as a clone."
            ]
        };

        AddMissingInputWarnings(projectRoot, package);
        return package;
    }

    public async Task<CodexTaskPackage> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetPackagePath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Codex task package was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var package = await JsonSerializer.DeserializeAsync<CodexTaskPackage>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (package is null)
            {
                throw new InvalidDataException($"Codex task package is empty or invalid: {path}");
            }

            ValidateSchema(package, path);
            NormalizeAndValidate(projectRoot, package);
            return package;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Codex task package JSON is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        CodexTaskPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var path = GetPackagePath(projectRoot);
        NormalizeAndValidate(projectRoot, package);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, package, WrbJsonOptions.Default, cancellationToken);
    }

    public async Task<string> WriteInstructionsAsync(
        string projectRoot,
        CodexTaskPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        NormalizeAndValidate(projectRoot, package);
        var path = GetInstructionsPath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, BuildInstructionsMarkdown(package), Encoding.UTF8, cancellationToken);
        return path;
    }

    public static string GetPackagePath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexTaskPackageSchema.RelativePath,
            "codex task package");
    }

    public static string GetInstructionsPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexTaskPackageSchema.InstructionsRelativePath,
            "codex task instructions");
    }

    private static List<CodexTaskInputFile> CreateDefaultInputs()
    {
        return
        [
            new() { RelativePath = ConstructionPackageSchema.RelativePath, Role = "constructionPackage", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.ProjectBriefRelativePath, Role = "projectBrief", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.ObservationSummaryRelativePath, Role = "observationSummary", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.AssetIndexRelativePath, Role = "assetIndex", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.ThemeSummaryRelativePath, Role = "themeSummary", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.ContentMapSummaryRelativePath, Role = "contentMapSummary", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.ConstraintsRelativePath, Role = "constraints", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.AcceptanceChecklistRelativePath, Role = "acceptanceChecklist", Required = true },
            new() { RelativePath = ConstructionPackageContextSchema.PackageIndexRelativePath, Role = "contextPackageIndex", Required = true },
            new() { RelativePath = ObservationPackageSchema.RelativePath, Role = "observationPackage", Required = true },
            new() { RelativePath = AssetsManifestSchema.RelativePath, Role = "assetsManifest", Required = true },
            new() { RelativePath = ThemeManifestSchema.RelativePath, Role = "themeManifest", Required = true },
            new() { RelativePath = ContentMapSchema.RelativePath, Role = "contentMap", Required = true }
        ];
    }

    private static void AddMissingInputWarnings(string projectRoot, CodexTaskPackage package)
    {
        foreach (var input in package.InputFiles.Where(input => input.Required))
        {
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, input.RelativePath, "task input file");
            if (!File.Exists(fullPath))
            {
                package.Warnings.Add($"Required task input is missing at scaffold time: {input.RelativePath}");
            }
        }
    }

    private static string BuildPromptMarkdown()
    {
        return """
        # Future Codex Construction Task

        Use the project manifests to prepare a static branded website output. This scaffold does not authorize real Codex CLI execution yet.

        Required boundaries:
        - Do not execute Codex CLI.
        - This round does not execute Codex CLI.
        - Do not call OpenAI API.
        - Only write codex-workspace/output-site/current.
        - Write workspace files only under `codex-workspace/`.
        - Write final generated site files only under `output-site/current/`.
        - Do not modify project source files.
        - Do not write `.git`, system directories, user credential directories, software installation directories, or any absolute path.
        - Do not call OpenAI APIs from this task package.
        - Do not copy reference-site copyrighted assets.
        - Do not copy protected reference-site assets, logos, copy, or distinctive graphics by default.
        - Produce branded reconstruction context and output, not a clone.
        """;
    }

    private static string BuildInstructionsMarkdown(CodexTaskPackage package)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {package.Instruction.Title}");
        builder.AppendLine();
        builder.AppendLine(package.Instruction.Goal);
        builder.AppendLine();
        builder.AppendLine(package.Instruction.PromptMarkdown.Trim());
        builder.AppendLine();
        builder.AppendLine("## Reading Order");
        builder.AppendLine();
        foreach (var item in GetReadingOrder())
        {
            builder.AppendLine($"- `{item}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Sandbox");
        builder.AppendLine();
        builder.AppendLine($"Workspace: `{package.Sandbox.WorkspaceRelativePath}`");
        builder.AppendLine();
        builder.AppendLine("Allowed write roots:");
        foreach (var root in package.Sandbox.AllowedWriteRoots)
        {
            builder.AppendLine($"- `{root}`");
        }

        builder.AppendLine();
        builder.AppendLine("Forbidden roots and areas:");
        foreach (var root in package.Sandbox.ForbiddenRoots)
        {
            builder.AppendLine($"- `{root}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Inputs");
        foreach (var input in package.InputFiles)
        {
            builder.AppendLine($"- `{input.RelativePath}` ({input.Role}, required: {input.Required})");
        }

        builder.AppendLine();
        builder.AppendLine("## Expected Outputs");
        foreach (var output in package.ExpectedOutputs)
        {
            builder.AppendLine($"- `{output.RelativePath}` - {output.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("## Prohibited Actions");
        foreach (var action in package.ProhibitedActions)
        {
            builder.AppendLine($"- {action}");
        }

        builder.AppendLine();
        builder.AppendLine("## P1.6 Safety Boundary Phrases");
        builder.AppendLine();
        foreach (var phrase in new[]
                 {
                     "Reading order",
                     "Do not execute Codex CLI",
                     "Do not call OpenAI API",
                     "Only write codex-workspace/output-site/current",
                     "Do not modify source code",
                     "Do not write .git",
                     "Do not copy reference-site copyrighted assets"
                 })
        {
            builder.AppendLine($"- {phrase}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> GetReadingOrder()
    {
        return
        [
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
        ];
    }

    private static void ValidateSchema(CodexTaskPackage package, string path)
    {
        if (!string.Equals(
                package.SchemaVersion,
                CodexTaskPackageSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Codex task package schemaVersion '{package.SchemaVersion}' is not supported. Expected '{CodexTaskPackageSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(string projectRoot, CodexTaskPackage package)
    {
        package.SchemaVersion = CodexTaskPackageSchema.CurrentSchemaVersion;
        package.ProjectId = package.ProjectId?.Trim() ?? string.Empty;
        package.TaskPackageId = string.IsNullOrWhiteSpace(package.TaskPackageId)
            ? CreatePackageId("task", DateTimeOffset.UtcNow)
            : package.TaskPackageId.Trim();
        package.CreatedAt = package.CreatedAt == default ? DateTimeOffset.UtcNow : package.CreatedAt;
        package.Instruction ??= new CodexTaskInstruction();
        package.Instruction.Title = NormalizeText(package.Instruction.Title, "Codex task scaffold");
        package.Instruction.Goal = package.Instruction.Goal?.Trim() ?? string.Empty;
        package.Instruction.PromptMarkdown = package.Instruction.PromptMarkdown?.Trim() ?? string.Empty;
        package.Sandbox ??= new CodexTaskSandbox();
        package.Sandbox.WorkspaceRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(
            projectRoot,
            string.IsNullOrWhiteSpace(package.Sandbox.WorkspaceRelativePath)
                ? ProjectDirectoryV2.CodexWorkspace
                : package.Sandbox.WorkspaceRelativePath,
            "task sandbox workspace");
        package.Sandbox.AllowedWriteRoots = package.Sandbox.AllowedWriteRoots?
            .Select(root => ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, root, "task allowed write root"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        package.Sandbox.ForbiddenRoots = package.Sandbox.ForbiddenRoots?
            .Select(root => ProjectPackagePathHelpers.NormalizeRelativeToken(root, "task forbidden root"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        package.InputFiles ??= [];
        package.ExpectedOutputs ??= [];
        package.ProhibitedActions = NormalizeStringList(package.ProhibitedActions);
        package.Warnings = NormalizeStringList(package.Warnings);

        foreach (var input in package.InputFiles)
        {
            input.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, input.RelativePath, "task input file");
            input.Role = NormalizeText(input.Role, "input");
        }

        foreach (var output in package.ExpectedOutputs)
        {
            output.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, output.RelativePath, "task expected output");
            output.Description = output.Description?.Trim() ?? string.Empty;
        }
    }

    private static string NormalizeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static List<string> NormalizeStringList(List<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string CreatePackageId(string prefix, DateTimeOffset now)
    {
        var raw = $"{prefix}-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(44, raw.Length)];
    }
}
