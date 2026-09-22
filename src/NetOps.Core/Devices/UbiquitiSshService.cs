using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>
/// Ubiquiti EdgeOS / EdgeRouter and UniFi OS appliance SSH helpers.
/// EdgeOS: Vyatta-style shell. UniFi OS: Linux shell with limited commands.
/// </summary>
public sealed class UbiquitiSshService
{
    public Task<DeviceBackupResult> IdentityAsync(
        string host, string username, string password, int port = 22, CancellationToken ct = default)
        => RunAsync(host, username, password, port, IdentityCommands, null, "ubnt-id", ct);

    public Task<DeviceBackupResult> ExportConfigAsync(
        string host, string username, string password, string localDirectory,
        int port = 22, CancellationToken ct = default)
        => RunAsync(host, username, password, port, ExportCommands, localDirectory, "ubnt-cfg", ct);

    private static readonly string[] IdentityCommands =
    [
        // EdgeOS
        "show version",
        // UniFi OS / Linux fallback
        "cat /etc/version 2>/dev/null; uname -a; hostname"
    ];

    private static readonly string[] ExportCommands =
    [
        // EdgeOS full config
        "show configuration commands",
        // Fallback: config.boot
        "cat /config/config.boot 2>/dev/null",
        // UniFi OS partial
        "cat /data/udapi-config/udapi-net-cfg.json 2>/dev/null | head -c 200000"
    ];

    private async Task<DeviceBackupResult> RunAsync(
        string host, string username, string password, int port,
        string[] commands, string? localDirectory, string filePrefix, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host)) return Fail("Host is required.");
        if (string.IsNullOrWhiteSpace(username)) return Fail("Username is required.");

        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(25);
                client.Connect();
                if (!client.IsConnected) return Fail("SSH connect failed.");

                var sb = new StringBuilder();
                foreach (var cmdText in commands)
                {
                    using var cmd = client.CreateCommand(cmdText);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(45);
                    var output = cmd.Execute();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        sb.AppendLine("# CMD: " + cmdText);
                        sb.AppendLine(output.Trim());
                        sb.AppendLine();
                        // Prefer first successful substantial output for export
                        if (localDirectory is not null && output.Length > 80)
                            break;
                    }
                }

                client.Disconnect();
                var text = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return Fail("No output. Check EdgeOS vs UniFi credentials and SSH access.");

                string? path = null;
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                    var safeHost = string.Concat(host.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
                    path = Path.Combine(localDirectory, $"{filePrefix}-{safeHost}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                    File.WriteAllText(path, text, Encoding.UTF8);
                }

                return new DeviceBackupResult
                {
                    Success = true,
                    Message = path is null ? "OK" : $"Saved ({text.Length} chars).",
                    LocalPath = path,
                    Preview = text.Length > 1500 ? text[..1500] + "\n…" : text
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail("Ubiquiti SSH: " + ex.Message);
        }
    }

    private static DeviceBackupResult Fail(string m) => new() { Success = false, Message = m };
}
