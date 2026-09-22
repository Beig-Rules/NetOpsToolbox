using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetOps.Core.Security;

public sealed class VaultEntry
{
    public string Id { get; set; } = "";
    public string Host { get; set; } = "";
    public string Username { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Vendor { get; set; } = "";
    public string? Notes { get; set; }
    /// <summary>Base64 DPAPI-protected login password.</summary>
    public string PasswordProtectedBase64 { get; set; } = "";
    /// <summary>Base64 DPAPI-protected enable password (Cisco); optional.</summary>
    public string? EnablePasswordProtectedBase64 { get; set; }
}

public sealed class CredentialVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NetOpsToolbox.Vault.v1");
    private readonly string _path;
    private List<VaultEntry> _entries = new();

    public CredentialVault(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetOpsToolbox", "vault.json");
    }

    public string Path => _path;
    public IReadOnlyList<VaultEntry> Entries => _entries;

    public void Load()
    {
        if (!File.Exists(_path)) { _entries = new(); return; }
        _entries = JsonSerializer.Deserialize<List<VaultEntry>>(File.ReadAllText(_path)) ?? new();
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Upsert(
        string id, string host, string username, string password, int port, string vendor,
        string? enablePassword = null, string? notes = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("DPAPI vault requires Windows.");

        var existing = _entries.FirstOrDefault(e => e.Id == id);
        if (existing is null)
        {
            existing = new VaultEntry { Id = id };
            _entries.Add(existing);
        }

        existing.Host = host;
        existing.Username = username;
        existing.Port = port;
        existing.Vendor = vendor;
        existing.Notes = notes;
        existing.PasswordProtectedBase64 = Protect(password);
        if (!string.IsNullOrEmpty(enablePassword))
            existing.EnablePasswordProtectedBase64 = Protect(enablePassword);
        // if enable left empty on update, keep previous enable blob

        Save();
    }

    public string? UnprotectPassword(VaultEntry entry) => Unprotect(entry.PasswordProtectedBase64);

    public string? UnprotectEnablePassword(VaultEntry entry)
        => string.IsNullOrEmpty(entry.EnablePasswordProtectedBase64)
            ? null
            : Unprotect(entry.EnablePasswordProtectedBase64);

    public bool Remove(string id)
    {
        var n = _entries.RemoveAll(e => e.Id == id);
        if (n > 0) Save();
        return n > 0;
    }

    public string DisplayLine(VaultEntry e)
        => $"{e.Host}:{e.Port}  {e.Username}  [{e.Vendor}]" +
           (string.IsNullOrEmpty(e.EnablePasswordProtectedBase64) ? "" : "  +enable");

    private static string Protect(string plain)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string? Unprotect(string? b64)
    {
        if (string.IsNullOrEmpty(b64) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;
        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(b64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }
}
