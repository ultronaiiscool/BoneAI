using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace BoneAI.Infrastructure;

/// <summary>Owns the authenticated, game-tool-only Android App Server.</summary>
public sealed class QuestCodexHostManager : IDisposable
{
    private const string LibraryFile = "libcodex_app_server.so";
    private readonly MainThreadDispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MelonLoader.NativeLibrary? _library;
    private StartNative? _start;
    private StopNative? _stop;
    private IsRunningNative? _isRunning;
    private LastErrorNative? _lastError;
    private bool _ownsServer;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int StartNative(IntPtr bindAddress, IntPtr codexHome, IntPtr tokenSha256);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int StopNative();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IsRunningNative();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SecurityVersionNative();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate UIntPtr LastErrorNative(IntPtr buffer, UIntPtr bufferLength);

    public string Status { get; private set; } = "Not started";
    public string Endpoint { get; private set; } = string.Empty;
    public string BearerToken { get; private set; } = string.Empty;

    public QuestCodexHostManager(MainThreadDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<bool> EnsureStartedAsync()
    {
        if (!PlatformInfo.IsAndroid) return false;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ownsServer && _isRunning?.Invoke() == 1) return true;
            var source = Path.Combine(MelonEnvironment.UserLibsDirectory, LibraryFile);
            if (!File.Exists(source))
            {
                Status = "Quest Codex library missing from UserLibs; install the complete v3 Quest package";
                return false;
            }

            var privateHome = await _dispatcher.InvokeAsync(GetPrivateCodexHome).ConfigureAwait(false);
            var expectedHash = await VerifyLibraryAsync(source).ConfigureAwait(false);
            var privateNativeDirectory = Path.Combine(privateHome, "native");
            Directory.CreateDirectory(privateNativeDirectory);
            var privateLibrary = Path.Combine(privateNativeDirectory, LibraryFile);
            await CopyIfChangedAsync(source, privateLibrary, expectedHash).ConfigureAwait(false);
            LoadSecureLibrary(privateLibrary);

            var port = GetUnusedLoopbackPort();
            Endpoint = $"ws://127.0.0.1:{port}";
            BearerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var tokenDigest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(BearerToken)));
            var stateHome = Path.Combine(privateHome, "state");
            Directory.CreateDirectory(stateHome);
            var result = Start(Endpoint, stateHome, tokenDigest);
            if (result != 0) throw new InvalidOperationException("Quest Codex start failed: " + NativeError());
            _ownsServer = true;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            while (!timeout.IsCancellationRequested)
            {
                if (_isRunning?.Invoke() != 1) throw new InvalidOperationException("Quest Codex server exited: " + NativeError());
                try
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}/readyz", timeout.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        Status = "Quest Codex App Server ready";
                        AgentLog.Info(Status);
                        return true;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException) { }
                await Task.Delay(250, timeout.Token).ConfigureAwait(false);
            }
            throw new TimeoutException("Quest Codex server did not become ready within 20 seconds");
        }
        catch (Exception ex)
        {
            Status = "Quest Codex unavailable: " + ex.GetBaseException().Message;
            AgentLog.Warn(Status);
            StopOwnedServer();
            BearerToken = string.Empty;
            Endpoint = string.Empty;
            return false;
        }
        finally { _gate.Release(); }
    }

    private static string GetPrivateCodexHome()
    {
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var files = activity.Call<AndroidJavaObject>("getFilesDir");
        var path = files.Call<string>("getAbsolutePath");
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Android did not provide a private files directory");
        return Path.Combine(path, "BoneAI", "Codex");
    }

    private static async Task<string> VerifyLibraryAsync(string path)
    {
        var expectedPath = path + ".sha256";
        if (!File.Exists(expectedPath)) throw new InvalidDataException("Quest Codex checksum is missing");
        var expected = (await File.ReadAllTextAsync(expectedPath).ConfigureAwait(false)).Split(' ', '\t', '\r', '\n')[0];
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException("Quest Codex checksum is malformed");
        var actual = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }).ConfigureAwait(false);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Quest Codex library checksum does not match");
        return expected;
    }

    private static async Task CopyIfChangedAsync(string source, string destination, string expectedHash)
    {
        if (File.Exists(destination) && new FileInfo(source).Length == new FileInfo(destination).Length)
        {
            var actual = await Task.Run(() =>
            {
                using var existing = File.OpenRead(destination);
                using var hash = SHA256.Create();
                return Convert.ToHexString(hash.ComputeHash(existing));
            }).ConfigureAwait(false);
            if (actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) return;
        }
        await Task.Run(() => File.Copy(source, destination, true)).ConfigureAwait(false);
    }

    private void LoadSecureLibrary(string path)
    {
        if (_library != null) return;
        var library = MelonLoader.NativeLibrary.Load(path);
        var security = Marshal.GetDelegateForFunctionPointer<SecurityVersionNative>(
            library.GetExport("codex_app_server_security_version"));
        if (security() != 1) throw new InvalidDataException("Unsupported Quest Codex security ABI");
        _start = Marshal.GetDelegateForFunctionPointer<StartNative>(
            library.GetExport("codex_app_server_start"));
        _stop = Marshal.GetDelegateForFunctionPointer<StopNative>(
            library.GetExport("codex_app_server_stop"));
        _isRunning = Marshal.GetDelegateForFunctionPointer<IsRunningNative>(
            library.GetExport("codex_app_server_is_running"));
        _lastError = Marshal.GetDelegateForFunctionPointer<LastErrorNative>(
            library.GetExport("codex_app_server_last_error"));
        _library = library;
    }

    private int Start(string endpoint, string home, string digest)
    {
        var bind = Marshal.StringToCoTaskMemUTF8(endpoint);
        var homePointer = Marshal.StringToCoTaskMemUTF8(home);
        var digestPointer = Marshal.StringToCoTaskMemUTF8(digest);
        try { return _start!(bind, homePointer, digestPointer); }
        finally
        {
            Marshal.FreeCoTaskMem(bind);
            Marshal.FreeCoTaskMem(homePointer);
            Marshal.FreeCoTaskMem(digestPointer);
        }
    }

    private string NativeError()
    {
        var required = _lastError?.Invoke(IntPtr.Zero, UIntPtr.Zero).ToUInt64() ?? 0;
        if (required <= 1 || required > 8192) return "No native error details";
        var buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            _lastError!(buffer, (UIntPtr)required);
            return Marshal.PtrToStringUTF8(buffer) ?? "No native error details";
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static int GetUnusedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private void StopOwnedServer()
    {
        if (!_ownsServer) return;
        try { _stop?.Invoke(); }
        catch (Exception ex) { AgentLog.Warn("Quest Codex shutdown: " + ex.GetBaseException().Message); }
        _ownsServer = false;
        BearerToken = string.Empty;
        Endpoint = string.Empty;
    }

    public void Dispose()
    {
        StopOwnedServer();
        _library = null;
        _gate.Dispose();
    }
}
