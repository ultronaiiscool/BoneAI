using Newtonsoft.Json.Linq;

namespace BoneAI.AI;

public sealed record CodexModelInfo(string Id, string DisplayName, bool Hidden, bool IsDefault,
    string DefaultEffort, IReadOnlyList<string> SupportedEfforts)
{
    public static CodexModelInfo? Parse(JToken? token)
    {
        if (token is not JObject data) return null;
        var id = data["model"]?.Value<string>() ?? data["id"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(id)) return null;
        var efforts = (data["supportedReasoningEfforts"] as JArray ?? new JArray())
            .Select(x => x["reasoningEffort"]?.Value<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        var defaultEffort = data["defaultReasoningEffort"]?.Value<string>() ?? string.Empty;
        if (efforts.Length > 0 && !efforts.Contains(defaultEffort, StringComparer.Ordinal)) defaultEffort = efforts[0];
        return new CodexModelInfo(id, data["displayName"]?.Value<string>() ?? id,
            data["hidden"]?.Value<bool>() ?? false, data["isDefault"]?.Value<bool>() ?? false,
            defaultEffort, efforts);
    }
}
