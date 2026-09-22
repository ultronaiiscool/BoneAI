using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using BoneAI.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoneAI.AI;

/// <summary>Loopback-only, keyless browser speech bridge with a per-session capability token.</summary>
public sealed class BrowserVoiceServer : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 8 * 1024;
    private readonly ConversationManager _conversation;
    private readonly AgentConfig _config;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _clients = new(8, 8);
    private TcpListener? _listener;
    private string _token = string.Empty;
    private Task? _loop;
    private int _commandBusy;

    public string Status { get; private set; } = "Browser voice stopped";
    public string Url { get; private set; } = string.Empty;
    public bool Running => _listener != null;

    public BrowserVoiceServer(ConversationManager conversation, AgentConfig config) { _conversation = conversation; _config = config; }

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
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(7));
        cancellationToken = requestTimeout.Token;
        using (client)
        {
            client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
            try
            {
                await using var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                if (!HasValidToken(request.Target)) { await ReplyAsync(stream, 403, "text/plain; charset=utf-8", "Forbidden", cancellationToken).ConfigureAwait(false); return; }
                if (request.Method == "GET")
                {
                    await ReplyAsync(stream, 200, "text/html; charset=utf-8", BuildPage(), cancellationToken).ConfigureAwait(false);
                    return;
                }
                if (request.Method != "POST" || !request.Target.StartsWith("/prompt?", StringComparison.Ordinal))
                {
                    await ReplyAsync(stream, 404, "text/plain; charset=utf-8", "Not found", cancellationToken).ConfigureAwait(false); return;
                }
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
        var payload = Encoding.UTF8.GetBytes(body); var reason = status switch { 200 => "OK", 202 => "Accepted", 400 => "Bad Request", 403 => "Forbidden", 409 => "Conflict", _ => "Not Found" };
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'self' 'unsafe-inline'; connect-src 'self'\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false); await stream.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private string BuildPage()
    {
        var wake = JsonConvert.SerializeObject(string.IsNullOrWhiteSpace(_config.VoiceWakeWord.Value) ? "Hey BoneAI" : _config.VoiceWakeWord.Value.Trim());
        var token = JsonConvert.SerializeObject(_token);
        return "<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>BoneAI Voice</title><style>body{font:18px system-ui;background:#090913;color:#eee;max-width:720px;margin:40px auto;padding:24px}button{font-size:20px;padding:14px 22px;background:#ff641e;color:#fff;border:0;border-radius:12px}.card{background:#171725;padding:22px;border-radius:16px;margin:16px 0}#heard{color:#ff9b64}</style></head><body><h1>BoneAI Browser Voice (Beta)</h1><div class=\"card\"><p id=\"status\">Press Start and allow microphone access.</p><button id=\"start\">Start listening</button><p>Wake word: <b id=\"wake\"></b></p><p id=\"heard\"></p></div><p>Keep this page open. Speech recognition is provided by your browser and may use its online service. BoneAI does not impose a usage limit or require an STT API key.</p><script>const TOKEN=" + token + ",WAKE=" + wake + ";document.getElementById('wake').textContent=WAKE;const SR=window.SpeechRecognition||window.webkitSpeechRecognition;let active=false,armedUntil=0,rec;if(!SR){document.getElementById('status').textContent='This browser does not support SpeechRecognition.';document.getElementById('start').disabled=true}else{rec=new SR();rec.continuous=true;rec.interimResults=false;rec.lang=navigator.language||'en-US';rec.onresult=e=>{for(let i=e.resultIndex;i<e.results.length;i++){if(!e.results[i].isFinal)continue;let text=e.results[i][0].transcript.trim();document.getElementById('heard').textContent='Heard: '+text;let lower=text.toLowerCase(),w=WAKE.toLowerCase(),at=lower.indexOf(w),cmd='';if(at>=0){cmd=text.slice(at+WAKE.length).replace(/^[ ,:;-]+/,'').trim();armedUntil=Date.now()+8000}else if(Date.now()<armedUntil){cmd=text;armedUntil=0}if(cmd)send(cmd)}};rec.onerror=e=>document.getElementById('status').textContent='Speech error: '+e.error;rec.onend=()=>{if(active)setTimeout(()=>{try{rec.start()}catch{}},350)};document.getElementById('start').onclick=()=>{active=!active;if(active){document.getElementById('start').textContent='Stop listening';document.getElementById('status').textContent='Listening for “'+WAKE+'”…';try{rec.start()}catch{}}else{document.getElementById('start').textContent='Start listening';document.getElementById('status').textContent='Stopped';rec.stop()}}}async function send(text){document.getElementById('status').textContent='Sending: '+text;try{let r=await fetch('/prompt?token='+encodeURIComponent(TOKEN),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({text})});document.getElementById('status').textContent=r.ok?'Sent. Listening for “'+WAKE+'”…':'BoneAI rejected the command.'}catch(e){document.getElementById('status').textContent='BoneAI connection failed.'}}</script></body></html>";
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    public void Dispose() { _lifetime.Cancel(); Stop(); try { _loop?.Wait(500); } catch { } _lifetime.Dispose(); }
    private sealed record HttpRequest(string Method, string Target, byte[] Body);
}
