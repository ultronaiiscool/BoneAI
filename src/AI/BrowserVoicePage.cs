using System.Resources;
using Newtonsoft.Json;

namespace BoneAI.AI;

internal static class BrowserVoicePage
{
    private const string ResourceName = "BoneAI.assets.browser-voice.html";
    private static readonly Lazy<string> Template = new(() =>
    {
        using var stream = typeof(BrowserVoicePage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new MissingManifestResourceException(ResourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string Build(string token, string wakeWord)
    {
        var options = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml };
        var safeToken = JsonConvert.SerializeObject(token, Formatting.None, options);
        var safeWakeWord = JsonConvert.SerializeObject(string.IsNullOrWhiteSpace(wakeWord) ? "Hey BoneAI" : wakeWord.Trim(), Formatting.None, options);
        return Template.Value.Replace("__TOKEN_JSON__", safeToken, StringComparison.Ordinal)
            .Replace("__WAKE_JSON__", safeWakeWord, StringComparison.Ordinal);
    }
}
