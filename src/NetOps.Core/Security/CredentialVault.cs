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
    /// <summary>Base64 of DPAPI-protected password bytes (CurrentUser scope).</summary>
    public string PasswordProtectedBase64 { get; set; } = "";
}

/// <summary>
/// Local credential vault. Passwords encrypted with Windows DPAPI (CurrentUser).
/// Non-Windows: refuses to persist secrets.
/// </summary>
public sealed class CredentialVault
{
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
        if (!File.Exists(_path))
        {
            _entries = new();
            return;
        }
        var json = File.ReadAllText(_path);
        _entries = JsonSerializer.Deserialize<List<VaultEntry>>(json) ?? new();
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    public void Upsert(string id, string host, string username, string password, int port, string vendor, string? notes = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("DPAPI vault requires Windows.");

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password),
            optionalEntropy: Encoding.UTF8.GetBytes("NetOpsToolbox.Vault.v1"),
            scope: DataProtectionScope.CurrentUser);

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
        existing.PasswordProtectedBase64 = Convert.ToBase64String(protectedBytes);
        Save();
    }

    public string? UnprotectPassword(VaultEntry entry)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(entry.PasswordProtectedBase64);
            var plain = ProtectedData.Unprotect(
                bytes,
                optionalEntropy: Encoding.UTF8.GetBytes("NetOpsToolbox.Vault.v1"),
                scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public bool Remove(string id)
    {
        var n = _entries.RemoveAll(e => e.Id == id);
        if (n > 0) Save();
        return n > 0;
    }
}
