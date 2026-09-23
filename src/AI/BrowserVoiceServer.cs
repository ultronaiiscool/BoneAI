using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using BoneAI.Infrastructure;
using Newtonsoft.Json.Linq;

namespace BoneAI.AI;

/// <summary>Loopback-only, keyless browser speech bridge with a per-session capability token.</summary>
public sealed class BrowserVoiceServer : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 2 * 1024 * 1024;
    private readonly ConversationManager _conversation;
    private readonly AgentConfig _config;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _clients = new(8, 8);
    private readonly WebTranscriptionClient _transcription;
    private TcpListener? _listener;
    private string _token = string.Empty;
    private Task? _loop;
    private int _commandBusy;

    public string Status { get; private set; } = "Browser voice stopped";
    public string Url { get; private set; } = string.Empty;
    public bool Running => _listener != null;

    public BrowserVoiceServer(ConversationManager conversation, AgentConfig config) { _conversation = conversation; _config = config; _transcription = new WebTranscriptionClient(config); }

    public string Start()
    {
        if (_listener != null) return Url;
        _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start(8);
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}/?token={_token}";
        Status = "Browser voice ready — keep its page open";
        _loop = Task.Run(() => AcceptLoopAsync(_lifetime.Token));
        AgentLog.Info("Browser voice bridge started on loopback.");
        return Url;
    }

    public void Stop()
    {
        try { _listener?.Stop(); } catch { }
        _listener = null; Url = string.Empty; _token = string.Empty; Status = "Browser voice stopped";
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            TcpClient? client = null;
            try { client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false); _ = HandleAsync(client, cancellationToken); }
            catch (OperationCanceledException) { client?.Dispose(); break; }
            catch (ObjectDisposedException) { client?.Dispose(); break; }
            catch (Exception ex) { client?.Dispose(); AgentLog.Warn("Browser voice accept failed: " + ex.GetBaseException().Message); }
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        if (!await _clients.WaitAsync(0, cancellationToken).ConfigureAwait(false)) { client.Dispose(); return; }
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = requestTimeout.Token;
        using (client)
        {
            client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
            try
            {
                await using var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                if (!HasValidToken(request.Target)) { await ReplyAsync(stream, 403, "text/plain; charset=utf-8", "Forbidden", cancellationToken).ConfigureAwait(false); return; }
                if (request.Method == "GET" && request.Target.StartsWith("/?", StringComparison.Ordinal))
                {
                    await ReplyAsync(stream, 200, "text/html; charset=utf-8", BrowserVoicePage.Build(_token, _config.VoiceWakeWord.Value), cancellationToken).ConfigureAwait(false);
                    return;
                }
                if (request.Method == "GET" && request.Target.StartsWith("/capabilities?", StringComparison.Ordinal))
                {
                    var available = new JObject { ["groq"] = _transcription.HasGroq, ["cloudflare"] = _transcription.HasCloudflare };
                    await ReplyAsync(stream, 200, "application/json; charset=utf-8", available.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
                    return;
                }
                if (request.Method == "POST" && request.Target.StartsWith("/transcribe?", StringComparison.Ordinal))
                {
                    var provider = request.Target.Contains("provider=groq&", StringComparison.Ordinal) ? "groq"
                        : request.Target.Contains("provider=cloudflare&", StringComparison.Ordinal) ? "cloudflare" : string.Empty;
                    if (provider.Length == 0 || request.Body.Length is < 44 or > MaxBodyBytes)
                    {
                        await ReplyAsync(stream, 400, "application/json; charset=utf-8", "{\"error\":\"Invalid transcription request\"}", cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    try
                    {
                        var transcript = await _transcription.TranscribeAsync(provider, request.Body, cancellationToken).ConfigureAwait(false);
                        var result = new JObject { ["text"] = transcript };
                        await ReplyAsync(stream, 200, "application/json; charset=utf-8", result.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AgentLog.Warn("Web transcription (" + provider + ") failed: " + RuntimeSecrets.Redact(ex.GetBaseException().Message));
                        await ReplyAsync(stream, 503, "application/json; charset=utf-8", "{\"error\":\"Transcription unavailable\"}", cancellationToken).ConfigureAwait(false);
                    }
                    return;
                }
                if (request.Method != "POST" || !request.Target.StartsWith("/prompt?", StringComparison.Ordinal))
                {
                    await ReplyAsync(stream, 404, "text/plain; charset=utf-8", "Not found", cancellationToken).ConfigureAwait(false); return;
                }
                if (request.Body.Length > 8 * 1024) { await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Command too large", cancellationToken).ConfigureAwait(false); return; }
                var text = JObject.Parse(Encoding.UTF8.GetString(request.Body))["text"]?.Value<string>()?.Trim() ?? string.Empty;
                if (text.Length is < 1 or > 2000) { await ReplyAsync(stream, 400, "text/plain; charset=utf-8", "Invalid command", cancellationToken).ConfigureAwait(false); return; }
                if (Interlocked.CompareExchange(ref _commandBusy, 1, 0) != 0) { await ReplyAsync(stream, 409, "application/json; charset=utf-8", "{\"accepted\":false,\"reason\":\"busy\"}", cancellationToken).ConfigureAwait(false); return; }
                Status = "Heard: " + Trim(text, 80);
                _ = Task.Run(async () =>
                {
                    try { await _conversation.SendAsync(text).ConfigureAwait(false); Status = "Command completed"; }
                    catch (Exception ex) { Status = "Command failed: " + Trim(ex.GetBaseException().Message, 90); AgentLog.Exception("Browser voice command", ex); }
                    finally { Interlocked.Exchange(ref _commandBusy, 0); }
                });
                await ReplyAsync(stream, 202, "application/json; charset=utf-8", "{\"accepted\":true}", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) { AgentLog.Debug("Rejected browser voice request: " + ex.GetBaseException().Message); }
            finally { _clients.Release(); }
        }
    }

    private bool HasValidToken(string target)
    {
        var marker = "token="; var at = target.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return false;
        var value = target[(at + marker.Length)..].Split('&', '#')[0];
        var left = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(value)); var right = Encoding.UTF8.GetBytes(_token);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private async Task<HttpRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var received = new MemoryStream(); var chunk = new byte[1024]; var headerEnd = -1;
        while (received.Length < MaxHeaderBytes && headerEnd < 0)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false); if (read == 0) throw new EndOfStreamException();
            received.Write(chunk, 0, read); headerEnd = FindHeaderEnd(received.GetBuffer(), (int)received.Length);
        }
        if (headerEnd < 0) throw new InvalidDataException("HTTP headers too large.");
        var bytes = received.ToArray(); var headerText = Encoding.ASCII.GetString(bytes, 0, headerEnd);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None); var first = lines[0].Split(' ');
        if (first.Length < 2 || (first[0] != "GET" && first[0] != "POST")) throw new InvalidDataException("Unsupported HTTP request.");
        var contentLength = 0;
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':'); if (colon <= 0) continue;
            if (line[..colon].Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && !int.TryParse(line[(colon + 1)..].Trim(), out contentLength)) throw new InvalidDataException("Invalid content length.");
        }
        if (contentLength is < 0 or > MaxBodyBytes) throw new InvalidDataException("Request body too large.");
        var body = new byte[contentLength]; var buffered = Math.Min(contentLength, bytes.Length - headerEnd - 4);
        if (buffered > 0) Buffer.BlockCopy(bytes, headerEnd + 4, body, 0, buffered);
        var offset = buffered;
        while (offset < body.Length) { var read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false); if (read == 0) throw new EndOfStreamException(); offset += read; }
        return new HttpRequest(first[0], first[1], body);
    }

    private static int FindHeaderEnd(byte[] bytes, int length)
    {
        for (var i = 3; i < length; i++) if (bytes[i - 3] == 13 && bytes[i - 2] == 10 && bytes[i - 1] == 13 && bytes[i] == 10) return i - 3;
        return -1;
    }

    private static async Task ReplyAsync(NetworkStream stream, int status, string contentType, string body, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(body); var reason = status switch { 200 => "OK", 202 => "Accepted", 400 => "Bad Request", 403 => "Forbidden", 409 => "Conflict", 503 => "Service Unavailable", _ => "Not Found" };
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'\r\nPermissions-Policy: on-device-speech-recognition=(self), microphone=(self)\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false); await stream.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { _lifetime.Cancel(); Stop(); try { _loop?.Wait(500); } catch { } _transcription.Dispose(); _lifetime.Dispose(); }
    private sealed record HttpRequest(string Method, string Target, byte[] Body);
}
