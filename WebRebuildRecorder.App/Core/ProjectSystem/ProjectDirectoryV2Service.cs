using WebRebuildRecorder.App.Core.Security;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IProjectDirectoryV2Service
{
    Task<ProjectDirectoryV2CreateResult> CreateAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectDirectoryV2CreateResult
{
    public string ProjectRoot { get; init; } = string.Empty;
    public List<string> EnsuredDirectories { get; init; } = [];
}

public sealed class ProjectDirectoryV2Service : IProjectDirectoryV2Service
{
    public Task<ProjectDirectoryV2CreateResult> CreateAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootValidation = SandboxPathPolicy.ValidateProjectRoot(projectRoot);
        if (!rootValidation.IsAllowed)
        {
            throw new InvalidOperationException(rootValidation.Message);
        }

        var normalizedRoot = rootValidation.NormalizedTargetPath;
        Directory.CreateDirectory(normalizedRoot);

        var ensured = new List<string>();
        foreach (var relativeDirectory in ProjectDirectoryV2.RequiredDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryPath = Path.Combine(normalizedRoot, relativeDirectory);
            var validation = SandboxPathPolicy.ValidateProjectPath(normalizedRoot, directoryPath);
            if (!validation.IsAllowed)
            {
                throw new InvalidOperationException(validation.Message);
            }

            Directory.CreateDirectory(validation.NormalizedTargetPath);
            ensured.Add(validation.NormalizedTargetPath);
        }

        return Task.FromResult(new ProjectDirectoryV2CreateResult
        {
            ProjectRoot = normalizedRoot,
            EnsuredDirectories = ensured
        });
    }
}
