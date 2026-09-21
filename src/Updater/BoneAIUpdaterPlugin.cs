using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;

namespace BoneAI.Updater;

public sealed class BoneAIUpdaterPlugin : MelonPlugin
{
    private const string CurrentVersion = "2.3.0";
    private const string LatestRelease = "https://api.github.com/repos/ultronaiiscool/BoneAI/releases/latest";
    private string? _pendingZip;
    private string? _pendingHash;

    public override void OnApplicationStarted()
    {
        if (IsManagedModProfileInstall())
        {
            MelonLogger.Msg("Thunderstore/r2modman profile install detected; updates are managed by the mod manager.");
            return;
        }
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BoneAI-Updater/2.3.0");
            using var release = JsonDocument.Parse(await http.GetStringAsync(LatestRelease));
            var root = release.RootElement;
            if (root.GetProperty("prerelease").GetBoolean() || root.GetProperty("draft").GetBoolean()) return;
            var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
            if (!Version.TryParse(tag, out var latest) || latest <= Version.Parse(CurrentVersion)) return;
            var wanted = $"BoneAI-v{latest}.zip";
            string? zipUrl = null, shaUrl = null, apiDigest = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name == wanted)
                {
                    zipUrl = asset.GetProperty("browser_download_url").GetString();
                    if (asset.TryGetProperty("digest", out var digest)) apiDigest = digest.GetString()?.Replace("sha256:", "", StringComparison.OrdinalIgnoreCase);
                }
                else if (name == wanted + ".sha256") shaUrl = asset.GetProperty("browser_download_url").GetString();
            }
            if (zipUrl == null) { MelonLogger.Warning($"BoneAI {latest} has no {wanted} asset; update skipped."); return; }
            var folder = Path.Combine(MelonEnvironment.UserDataDirectory, "BoneAI", "Updates", latest.ToString());
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, wanted);
            await File.WriteAllBytesAsync(target, await http.GetByteArrayAsync(zipUrl));
            var expected = apiDigest;
            if (string.IsNullOrWhiteSpace(expected) && shaUrl != null) expected = (await http.GetStringAsync(shaUrl)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            if (string.IsNullOrWhiteSpace(expected)) { File.Delete(target); MelonLogger.Warning("Update has no SHA-256 digest; refusing to install it."); return; }
            var actual = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(target)));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) { File.Delete(target); MelonLogger.Error("BoneAI update SHA-256 mismatch; download deleted."); return; }
            _pendingZip = target; _pendingHash = actual;
            MelonLogger.Msg($"BoneAI {latest} downloaded and verified. It will install after BONELAB closes.");
        }
        catch (Exception ex) { MelonLogger.Warning("BoneAI update check failed safely: " + ex.GetBaseException().Message); }
    }

    public override void OnApplicationQuit()
    {
        if (_pendingZip == null || _pendingHash == null) return;
        var modDirectory = MainModDirectory();
        var script = Path.Combine(modDirectory, "start_boneai_bridge.py");
        if (!File.Exists(script)) { MelonLogger.Warning("Cannot apply BoneAI update: bridge helper is missing."); return; }
        foreach (var candidate in new[] { "python.exe", "python", "py.exe" })
        {
            try
            {
                var info = new ProcessStartInfo { FileName = candidate, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = modDirectory };
                if (candidate == "py.exe") info.ArgumentList.Add("-3");
                info.ArgumentList.Add(script); info.ArgumentList.Add("--install-update"); info.ArgumentList.Add(_pendingZip);
                info.ArgumentList.Add("--expected-sha256"); info.ArgumentList.Add(_pendingHash);
                info.ArgumentList.Add("--game-dir"); info.ArgumentList.Add(MelonEnvironment.GameRootDirectory);
                info.ArgumentList.Add("--parent-pid"); info.ArgumentList.Add(Environment.ProcessId.ToString());
                if (Process.Start(info) != null) return;
            }
            catch { }
        }
        MelonLogger.Warning("Could not launch Python to apply the downloaded BoneAI update.");
    }

    private static string MainModDirectory()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "BoneAI");
        return assembly == null ? MelonEnvironment.ModsDirectory : Path.GetDirectoryName(assembly.Location) ?? MelonEnvironment.ModsDirectory;
    }

    private static bool IsManagedModProfileInstall()
    {
        var root = Path.GetFullPath(MelonEnvironment.ModsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var installed = Path.GetFullPath(MainModDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !installed.Equals(root, StringComparison.OrdinalIgnoreCase);
    }
}
