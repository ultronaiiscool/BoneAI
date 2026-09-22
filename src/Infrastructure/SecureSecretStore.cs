using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace BoneAI.Infrastructure;

/// <summary>Encrypts secrets with Windows DPAPI or authenticated device-bound Quest encryption. Plaintext is never written.</summary>
internal sealed class SecureSecretStore
{
    private readonly string _path = Path.Combine(MelonEnvironment.UserDataDirectory, "BoneAI", "secrets.v1.json");

    public Dictionary<string, string> LoadAll()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_path)) return result;
        var saved = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new();
        foreach (var pair in saved)
        {
            try { result[pair.Key] = Unprotect(Convert.FromBase64String(pair.Value)); }
            catch (Exception ex) { AgentLog.Warn("Ignored unreadable saved key for " + pair.Key + ": " + ex.GetBaseException().Message); }
        }
        return result;
    }

    public void Save(string provider, string secret) { var saved = Read(); saved[provider] = Convert.ToBase64String(Protect(secret)); Write(saved); }
    public void Remove(string provider) { var saved = Read(); if (!saved.Remove(provider)) return; if (saved.Count == 0) { if (File.Exists(_path)) File.Delete(_path); } else Write(saved); }
    public void Clear() { if (File.Exists(_path)) File.Delete(_path); }
    private Dictionary<string, string> Read() => File.Exists(_path) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new(StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
    private void Write(Dictionary<string, string> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(data, Formatting.Indented));
        File.Move(temporary, _path, true);
    }

    private static byte[] Protect(string value) => PlatformInfo.IsAndroid ? AndroidProtect(value) : WindowsProtect(value);
    private static string Unprotect(byte[] value) => PlatformInfo.IsAndroid ? AndroidUnprotect(value) : WindowsUnprotect(value);

    // Quest IL2CPP does not expose Android Keystore calls through a stable managed ABI. Use
    // authenticated, device-bound encryption and never fall back to plaintext.
    private static byte[] AndroidProtect(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(16); var iv = RandomNumberGenerator.GetBytes(16); var keys = AndroidKeys(salt);
        using var aes = Aes.Create(); aes.Key = keys[..32]; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        byte[] encrypted; using (var transform = aes.CreateEncryptor()) encrypted = transform.TransformFinalBlock(System.Text.Encoding.UTF8.GetBytes(value), 0, System.Text.Encoding.UTF8.GetByteCount(value));
        using var stream = new MemoryStream(); stream.WriteByte(2); stream.Write(salt); stream.Write(iv); stream.Write(encrypted);
        using var hmac = new HMACSHA256(keys[32..]); var unsigned = stream.ToArray(); stream.Write(hmac.ComputeHash(unsigned)); return stream.ToArray();
    }

    private static string AndroidUnprotect(byte[] envelope)
    {
        if (envelope.Length < 81 || envelope[0] != 2) throw new InvalidDataException("Unsupported encrypted-key format.");
        var unsigned = envelope[..^32]; var salt = envelope[1..17]; var iv = envelope[17..33]; var encrypted = envelope[33..^32]; var keys = AndroidKeys(salt);
        using var hmac = new HMACSHA256(keys[32..]); if (!CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(unsigned), envelope[^32..])) throw new CryptographicException("Encrypted key authentication failed.");
        using var aes = Aes.Create(); aes.Key = keys[..32]; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var transform = aes.CreateDecryptor(); var plain = transform.TransformFinalBlock(encrypted, 0, encrypted.Length); return System.Text.Encoding.UTF8.GetString(plain);
    }

    private static byte[] AndroidKeys(byte[] salt)
    {
        var identity = UnityEngine.SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrWhiteSpace(identity)) throw new PlatformNotSupportedException("Quest did not provide a stable device identity; refusing plaintext key storage.");
        using var derive = new Rfc2898DeriveBytes("BoneAI|" + identity + "|ProviderKeys.v1", salt, 120_000, HashAlgorithmName.SHA256); return derive.GetBytes(64);
    }

    [StructLayout(LayoutKind.Sequential)] private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] WindowsProtect(string value) => WindowsCrypt(System.Text.Encoding.UTF8.GetBytes(value), true);
    private static string WindowsUnprotect(byte[] value) => System.Text.Encoding.UTF8.GetString(WindowsCrypt(value, false));
    private static byte[] WindowsCrypt(byte[] bytes, bool protect)
    {
        var input = new DataBlob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length); DataBlob output;
            var ok = protect ? CryptProtectData(ref input, "BoneAI provider key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, output.Size); return result; } finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }
}
