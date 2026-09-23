using System.Net.Http.Headers;
using System.Text;
using BoneAI.Infrastructure;
using Newtonsoft.Json.Linq;

namespace BoneAI.AI;

/// <summary>Opt-in, free-tier web transcription. Credentials never leave the game process.</summary>
public sealed class WebTranscriptionClient : IDisposable
{
    private readonly AgentConfig _config;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };

    public WebTranscriptionClient(AgentConfig config) => _config = config;

    public bool HasGroq => !string.IsNullOrWhiteSpace(RuntimeSecrets.GetProviderApiKey("Groq"));
    public bool HasCloudflare => !string.IsNullOrWhiteSpace(RuntimeSecrets.GetProviderApiKey("Cloudflare"))
        && IsValidAccountId(_config.CloudflareAccountId.Value);

    public async Task<string> TranscribeAsync(string provider, byte[] wav, CancellationToken cancellationToken)
    {
        if (wav.Length is < 44 or > 2 * 1024 * 1024 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            throw new InvalidDataException("Expected a WAV recording under 2 MiB.");
        return provider switch
        {
            "groq" when HasGroq => await GroqAsync(wav, cancellationToken).ConfigureAwait(false),
            "cloudflare" when HasCloudflare => await CloudflareAsync(wav, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Transcription provider is not configured.")
        };
    }

    private async Task<string> GroqAsync(byte[] wav, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "boneai-voice.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RuntimeSecrets.GetProviderApiKey("Groq"));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await ReadLimitedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Groq transcription HTTP " + (int)response.StatusCode);
        return JObject.Parse(body)["text"]?.Value<string>()?.Trim() ?? string.Empty;
    }

    private async Task<string> CloudflareAsync(byte[] wav, CancellationToken cancellationToken)
    {
        var account = _config.CloudflareAccountId.Value.Trim();
        if (!IsValidAccountId(account)) throw new InvalidOperationException("Cloudflare Account ID is invalid.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/accounts/{account}/ai/run/@cf/openai/whisper");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RuntimeSecrets.GetProviderApiKey("Cloudflare"));
        request.Content = new ByteArrayContent(wav);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await ReadLimitedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Cloudflare transcription HTTP " + (int)response.StatusCode);
        var json = JObject.Parse(body);
        if (json["success"]?.Value<bool>() == false) throw new InvalidOperationException("Cloudflare transcription failed.");
        return json.SelectToken("result.text")?.Value<string>()?.Trim() ?? string.Empty;
    }

    private static bool IsValidAccountId(string? value) => value is { Length: 32 }
        && value.All(Uri.IsHexDigit);

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int max = 64 * 1024;
        if (content.Headers.ContentLength > max) throw new InvalidDataException("Transcription response too large.");
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var count = await input.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (output.Length + count > max) throw new InvalidDataException("Transcription response too large.");
            output.Write(chunk, 0, count);
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public void Dispose() => _http.Dispose();
}
