using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>SSH helpers for MikroTik RouterOS. Passwords in-memory only.</summary>
public sealed class MikroTikSshService
{
    public async Task<DeviceBackupResult> ExportConfigAsync(
        string host, string username, string password, string localDirectory,
        int port = 22, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return Fail("Host is required.");
        if (string.IsNullOrWhiteSpace(username)) return Fail("Username is required.");

        Directory.CreateDirectory(localDirectory);
        var safeHost = string.Concat(host.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
        var path = Path.Combine(localDirectory, $"mikrotik-{safeHost}-{DateTime.Now:yyyyMMdd-HHmmss}.rsc");

        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(20);
                client.Connect();
                using var cmd = client.CreateCommand("/export show-sensitive=no terse");
                cmd.CommandTimeout = TimeSpan.FromSeconds(60);
                var output = cmd.Execute();
                client.Disconnect();
                if (string.IsNullOrWhiteSpace(output))
                    return Fail("Empty export: " + cmd.Error);
                File.WriteAllText(path, output, Encoding.UTF8);
                return new DeviceBackupResult
                {
                    Success = true,
                    Message = $"Export saved ({output.Length} chars).",
                    LocalPath = path,
                    Preview = output.Length > 1200 ? output[..1200] + "\n…" : output
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { return Fail("SSH/export: " + ex.Message); }
    }

    public async Task<DeviceBackupResult> IdentityAsync(
        string host, string username, string password, int port = 22, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
                client.Connect();
                using var cmd = client.CreateCommand(":put [/system identity get name]; :put [/system resource get version]");
                var output = cmd.Execute();
                client.Disconnect();
                return new DeviceBackupResult { Success = true, Message = "Identity OK", Preview = output.Trim() };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static DeviceBackupResult Fail(string m) => new() { Success = false, Message = m };
}
