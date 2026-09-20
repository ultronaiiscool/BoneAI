using System.Diagnostics;
using System.Reflection;
using System.Net;
using System.Net.Http;

namespace BonelabAIAgent.Infrastructure;

public sealed class PythonBridgeManager : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _ownedProcess;

    public string Status { get; private set; } = "Not started";
    public bool OwnsProcess => _ownedProcess is { HasExited: false };

    public async Task<bool> EnsureStartedAsync(string endpoint, bool enabled)
    {
        if (!enabled)
        {
            Status = "Automatic bridge launch disabled";
            AgentLog.Info(Status);
            return false;
        }

        if (!TryGetLocalPort(endpoint, out var port))
        {
            Status = "Auto-launch requires a ws://127.0.0.1 or ws://localhost endpoint";
            AgentLog.Warn(Status);
            return false;
        }

        if (await IsReadyAsync(port, _lifetime.Token).ConfigureAwait(false))
        {
            Status = $"Existing bridge ready on port {port}";
            AgentLog.Info(Status);
            return true;
        }

        var modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                           ?? AppDomain.CurrentDomain.BaseDirectory;
        var script = Path.Combine(modDirectory, "start_codex_bridge.py");
        if (!File.Exists(script))
        {
            Status = $"Python bridge script is missing beside the DLL: {script}";
            AgentLog.Error(Status);
            return false;
        }

        Exception? lastError = null;
        foreach (var candidate in new[] { (File: "python.exe", Prefix: (string?)null), (File: "python", Prefix: (string?)null), (File: "py.exe", Prefix: "-3") })
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = candidate.File,
                    WorkingDirectory = modDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                if (candidate.Prefix != null) start.ArgumentList.Add(candidate.Prefix);
                start.ArgumentList.Add(script);
                start.ArgumentList.Add("--port");
                start.ArgumentList.Add(port.ToString());
                start.ArgumentList.Add("--parent-pid");
                start.ArgumentList.Add(Environment.ProcessId.ToString());

                var process = new Process { StartInfo = start, EnableRaisingEvents = true };
                process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AgentLog.Info("Python bridge: " + e.Data); };
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AgentLog.Warn("Python bridge: " + e.Data); };
                if (!process.Start()) throw new InvalidOperationException($"Could not start {candidate.File}.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ownedProcess = process;
                AgentLog.Info($"Started {Path.GetFileName(script)} with {candidate.File} (PID {process.Id}).");
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                AgentLog.Debug($"Python candidate {candidate.File} failed: {ex.GetBaseException().Message}");
            }
        }

        if (_ownedProcess == null)
        {
            Status = "Python was not found or could not start: " + lastError?.GetBaseException().Message;
            AgentLog.Error(Status);
            return false;
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (_ownedProcess.HasExited)
            {
                Status = $"Python bridge exited with code {_ownedProcess.ExitCode}";
                AgentLog.Error(Status);
                return false;
            }
            if (await IsReadyAsync(port, _lifetime.Token).ConfigureAwait(false))
            {
                Status = $"Python bridge ready on port {port}";
                AgentLog.Info(Status);
                return true;
            }
            await Task.Delay(250, _lifetime.Token).ConfigureAwait(false);
        }

        Status = "Python bridge did not become ready within 15 seconds";
        AgentLog.Error(Status);
        return false;
    }

    private static bool TryGetLocalPort(string endpoint, out int port)
    {
        port = 0;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "ws") return false;
        if (!(IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)) &&
            !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return false;
        port = uri.Port;
        return port is > 0 and <= 65535;
    }

    private static async Task<bool> IsReadyAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/readyz", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        if (_ownedProcess is { HasExited: false })
        {
            try
            {
                AgentLog.Info($"Stopping owned Python bridge process {_ownedProcess.Id}.");
                _ownedProcess.Kill(true);
                _ownedProcess.WaitForExit(3000);
            }
            catch (Exception ex) { AgentLog.Warn("Could not stop Python bridge: " + ex.GetBaseException().Message); }
        }
        _ownedProcess?.Dispose();
        _lifetime.Dispose();
    }
}
