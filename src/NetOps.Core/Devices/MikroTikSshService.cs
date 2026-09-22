using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

public sealed class DeviceBackupResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? LocalPath { get; init; }
    public string? Preview { get; init; }
}

/// <summary>
/// SSH session helpers for MikroTik RouterOS.
/// Credentials are never written to disk by this service — pass only in-memory.
/// </summary>
public sealed class MikroTikSshService
{
    public async Task<DeviceBackupResult> ExportConfigAsync(
        string host,
        string username,
        string password,
        string localDirectory,
        int port = 22,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return Fail("Host is required.");
        if (string.IsNullOrWhiteSpace(username))
            return Fail("Username is required.");

        Directory.CreateDirectory(localDirectory);
        var safeHost = string.Concat(host.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
        var fileName = $"mikrotik-{safeHost}-{DateTime.Now:yyyyMMdd-HHmmss}.rsc";
        var path = Path.Combine(localDirectory, fileName);

        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(20);
                client.Connect();
                if (!client.IsConnected)
                    return Fail("SSH connect failed.");

                // Verbose export without sensitive values when possible — still operator responsibility
                using var cmd = client.CreateCommand("/export show-sensitive=no terse");
                cmd.CommandTimeout = TimeSpan.FromSeconds(60);
                var output = cmd.Execute();
                var err = cmd.Error;
                client.Disconnect();

                if (cmd.ExitStatus is not null and not 0 && string.IsNullOrWhiteSpace(output))
                    return Fail($"Remote command exit {cmd.ExitStatus}: {err}");

                File.WriteAllText(path, output, Encoding.UTF8);
                var preview = output.Length > 1200 ? output[..1200] + "\n…(truncated)" : output;

                return new DeviceBackupResult
                {
                    Success = true,
                    Message = $"Export saved ({output.Length} chars).",
                    LocalPath = path,
                    Preview = preview
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail("SSH/export error: " + ex.Message);
        }
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
                return new DeviceBackupResult
                {
                    Success = true,
                    Message = "Identity OK",
                    Preview = output.Trim()
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static DeviceBackupResult Fail(string m) => new() { Success = false, Message = m };
}
