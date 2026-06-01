using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class LocalModelTaskGenerator : IAiTaskGenerator
{
    public Task<string> GenerateCodexTaskAsync(ProjectContext context) => Task.FromResult(string.Empty);
    public Task<string> GenerateFixTaskAsync(ProjectContext context) => Task.FromResult(string.Empty);
}

public sealed class OpenAiTaskGenerator : IAiTaskGenerator
{
    public Task<string> GenerateCodexTaskAsync(ProjectContext context) => Task.FromResult(string.Empty);
    public Task<string> GenerateFixTaskAsync(ProjectContext context) => Task.FromResult(string.Empty);
}
