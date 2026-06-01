namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class TaskFailureClassifier
{
    public TaskFailureClassification Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new TaskFailureClassification
            {
                Category = TaskFailureCategory.Unknown,
                Message = "No failure message was provided."
            };
        }

        var normalized = message.Trim();
        var category = normalized.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("outside project", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("path traversal", StringComparison.OrdinalIgnoreCase)
                ? TaskFailureCategory.SandboxViolation
                : normalized.Contains("missing input", StringComparison.OrdinalIgnoreCase)
                  || normalized.Contains("required file", StringComparison.OrdinalIgnoreCase)
                    ? TaskFailureCategory.MissingInput
                    : normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
                      || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
                      || normalized.Contains("api key", StringComparison.OrdinalIgnoreCase)
                        ? TaskFailureCategory.SecretDetected
                        : normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                            ? TaskFailureCategory.Timeout
                            : normalized.Contains("build failed", StringComparison.OrdinalIgnoreCase)
                                ? TaskFailureCategory.BuildFailed
                                : TaskFailureCategory.Unknown;

        return new TaskFailureClassification
        {
            Category = category,
            Message = normalized
        };
    }
}
