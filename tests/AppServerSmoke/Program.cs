using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var codex = args[0];
var port = 4599;
using var process = Process.Start(new ProcessStartInfo(codex, $"app-server --listen ws://127.0.0.1:{port}") { UseShellExecute = false, CreateNoWindow = true });
if (process == null) throw new Exception("Could not start Codex.");
try
{
    await Task.Delay(1200);
    using var socket = new ClientWebSocket();
    await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), CancellationToken.None);
    await Send(1, "initialize", new { clientInfo = new { name = "boneai-smoke", title = "BoneAI Smoke Test", version = "2.1.1" }, capabilities = new { experimentalApi = true } });
    Console.WriteLine(await Response(1));
    await Raw(new { method = "initialized", @params = new { } });
    await Send(2, "thread/start", new { baseInstructions = "Game tools only.", developerInstructions = "Do not use system tools.", sandbox = "read-only", approvalPolicy = "never", dynamicTools = new object[] { new { type = "namespace", name = "avatar", description = "Avatar tools", tools = new object[] { new { type = "function", name = "find", description = "Find an avatar", inputSchema = new { type = "object", additionalProperties = true } } } } } });
    var response = await Response(2);
    Console.WriteLine(response);
    if (response.Contains("\"error\"")) Environment.ExitCode = 2;
    using (var started = JsonDocument.Parse(response))
    {
        var threadId = started.RootElement.GetProperty("result").GetProperty("thread").GetProperty("id").GetString();
        await Send(3, "turn/start", new { threadId, input = new[] { new { type = "text", text = "Call avatar.find with query Morty, then briefly report the tool result." } }, effort = "low" });
        Console.WriteLine(await Response(3));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (!timeout.IsCancellationRequested)
        {
            var text = await Read(timeout.Token);
            Console.WriteLine(text);
            using var message = JsonDocument.Parse(text);
            var root = message.RootElement;
            if (root.TryGetProperty("method", out var method) && method.GetString() == "item/tool/call")
            {
                var requestId = root.GetProperty("id").Clone();
                await Raw(new { id = requestId, result = new { contentItems = new[] { new { type = "inputText", text = "{\"result\":\"success\",\"data\":[{\"title\":\"Morty\",\"barcode\":\"OskSir.RickandMortyPack.Avatar.Morty\"}]}" } }, success = true } });
            }
            if (root.TryGetProperty("method", out method) && method.GetString() == "turn/completed") break;
        }
    }

    async Task Send(int id, string method, object parameters) => await Raw(new { id, method, @params = parameters });
    async Task Raw(object value)
    {
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        await socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    async Task<string> Response(int id)
    {
        var buffer = new byte[65536];
        while (true)
        {
            var text = await Read(CancellationToken.None);
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("id", out var value) && value.GetInt32() == id) return text;
        }
    }
    async Task<string> Read(CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        using var stream = new MemoryStream();
        WebSocketReceiveResult part;
        do { part = await socket.ReceiveAsync(buffer, cancellationToken); stream.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
finally
{
    if (!process.HasExited) process.Kill(true);
}
