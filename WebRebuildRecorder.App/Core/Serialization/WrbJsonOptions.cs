using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebRebuildRecorder.App.Core.Serialization;

public static class WrbJsonOptions
{
    public static JsonSerializerOptions Default { get; } = CreateDefault();
    public static JsonSerializerOptions Compact { get; } = CreateCompact();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static JsonSerializerOptions CreateCompact()
    {
        return new JsonSerializerOptions(Default)
        {
            WriteIndented = false
        };
    }
}
