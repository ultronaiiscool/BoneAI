using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace BoneAI.Infrastructure;

/// <summary>Starts the optional official Codex App Server directly on PCVR.</summary>
public sealed class CodexHostManager : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _ownedProcess;

    public string Status { get; private set; } = "Not started";
    public string Endpoint { get; private set; } = string.Empty;
    public bool OwnsProcess => _ownedProcess is { HasExited: false };

    public async Task<bool> EnsureStartedAsync(string endpoint, bool enabled)
    {
        if (PlatformInfo.IsAndroid)
        {
            Status = "Codex account mode is PCVR-only; use a direct API provider on Quest";
            AgentLog.Info(Status);
            return false;
        }
        if (!enabled)
        {
            Status = "Automatic Codex host launch disabled";
            AgentLog.Info(Status);
            return false;
        }
        if (OwnsProcess && !string.IsNullOrWhiteSpace(Endpoint)) return true;
        // Always create an owned server on an unpredictable loopback port. Never trust a process
        // that happened to bind the configured port first.
        var port = GetUnusedLoopbackPort();
        Endpoint = $"ws://127.0.0.1:{port}";

        var codex = FindCodexExecutable();
        if (codex == null)
        {
            Status = "Codex was not found. Install the Codex desktop app or CLI.";
            AgentLog.Error(Status);
            return false;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = codex,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("app-server");
            start.ArgumentList.Add("--listen");
            start.ArgumentList.Add($"ws://127.0.0.1:{port}");
            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AgentLog.Debug("Codex host: " + e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AgentLog.Warn("Codex host: " + e.Data); };
            if (!process.Start()) throw new InvalidOperationException("Codex process did not start.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ownedProcess = process;
            AgentLog.Info($"Started official Codex App Server directly (PID {process.Id}).");
        }
        catch (Exception ex)
        {
            Status = "Could not start Codex App Server: " + ex.GetBaseException().Message;
            AgentLog.Error(Status);
            return false;
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (_ownedProcess.HasExited)
            {
                Status = $"Codex App Server exited with code {_ownedProcess.ExitCode}";
                AgentLog.Error(Status);
                return false;
            }
            if (await IsReadyAsync(port, _lifetime.Token).ConfigureAwait(false))
            {
                Status = $"Codex App Server ready on port {port}";
                AgentLog.Info(Status);
                return true;
            }
            await Task.Delay(250, _lifetime.Token).ConfigureAwait(false);
        }
        Status = "Codex App Server did not become ready within 15 seconds";
        AgentLog.Error(Status);
        return false;
    }

    private static string? FindCodexExecutable()
    {
        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(local))
        {
            var roots = new[]
            {
                Path.Combine(local, "OpenAI", "Codex", "bin"),
                Path.Combine(local, "Programs", "OpenAI Codex"),
                Path.Combine(local, "Programs", "Codex")
            };
            var installed = roots.Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (installed != null) return installed;
        }
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), "codex.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static bool TryGetLocalPort(string endpoint, out int port)
    {
        port = 0;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "ws") return false;
        if (!(IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)) &&
            !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;
        port = uri.Port;
        return port is > 0 and <= 65535;
    }

    private static int GetUnusedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static async Task<bool> IsReadyAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/readyz", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException) { return false; }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        if (_ownedProcess is { HasExited: false })
        {
            try
            {
                AgentLog.Info($"Stopping owned Codex App Server process {_ownedProcess.Id}.");
                _ownedProcess.Kill(true);
                _ownedProcess.WaitForExit(3000);
            }
            catch (Exception ex) { AgentLog.Warn("Could not stop Codex App Server: " + ex.GetBaseException().Message); }
        }
        _ownedProcess?.Dispose();
        _lifetime.Dispose();
    }
}
