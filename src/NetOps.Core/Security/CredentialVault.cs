using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetOps.Core.Security;

public sealed class VaultEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Host { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Username { get; set; } = "";
    public int Port { get; set; } = 22;
    public string ProtectedPassword { get; set; } = "";
    public string? ProtectedEnable { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>DPAPI-protected credential vault (Windows CurrentUser scope).</summary>
public sealed class CredentialVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NetOpsToolbox.Vault.v1");
    private readonly string _path;
    private List<VaultEntry> _entries = new();

    public CredentialVault(string? path = null)
    {
        _path = path ?? System.IO.Path.Combine(
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

    public void Upsert(string host, string vendor, string username, string password, int port = 22, string? enable = null)
    {
        var existing = _entries.FirstOrDefault(e =>
            e.Host.Equals(host, StringComparison.OrdinalIgnoreCase) &&
            e.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new VaultEntry { Host = host, Vendor = vendor, Username = username, Port = port };
            _entries.Add(existing);
        }
        existing.Vendor = vendor;
        existing.Port = port;
        existing.ProtectedPassword = Protect(password);
        existing.ProtectedEnable = string.IsNullOrEmpty(enable) ? null : Protect(enable);
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        Save();
    }

    public void Delete(string id)
    {
        _entries.RemoveAll(e => e.Id == id);
        Save();
    }

    public string? UnprotectLogin(VaultEntry e)
    {
        try { return Unprotect(e.ProtectedPassword); }
        catch { return null; }
    }

    public string? UnprotectEnable(VaultEntry e)
    {
        if (string.IsNullOrEmpty(e.ProtectedEnable)) return null;
        try { return Unprotect(e.ProtectedEnable); }
        catch { return null; }
    }

    public string DisplayLine(VaultEntry e)
        => $"{e.Host}:{e.Port}  {e.Username}  [{e.Vendor}]";

    public string? UnprotectPassword(VaultEntry e) => UnprotectLogin(e);
    public string? UnprotectEnablePassword(VaultEntry e) => UnprotectEnable(e);

    public void Remove(string id) => Delete(id);

    public void Upsert(string id, string host, string user, string password, int port, string vendor, string? enable = null)
        => Upsert(host, vendor, user, password, port, enable);

    private static string Protect(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string b64)
    {
        var protectedBytes = Convert.FromBase64String(b64);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
