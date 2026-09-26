using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>
/// Palo Alto Networks PAN-OS over SSH — read-only operational commands.
/// Uses set cli pager off for long output. No config commit.
/// </summary>
public sealed class PaloAltoSshService
{
    public Task<DeviceBackupResult> ShowSystemInfoAsync(
        string host, string username, string password,
        int port = 22, CancellationToken ct = default)
        => RunAsync(host, username, password, port, "show system info", null, ct);

    public Task<DeviceBackupResult> ShowConfigRunningAsync(
        string host, string username, string password, string localDirectory,
        int port = 22, CancellationToken ct = default)
        => RunAsync(host, username, password, port, "show config running", localDirectory, ct);

    public Task<DeviceBackupResult> ShowInterfaceAllAsync(
        string host, string username, string password,
        int port = 22, CancellationToken ct = default)
        => RunAsync(host, username, password, port, "show interface all", null, ct);

    private async Task<DeviceBackupResult> RunAsync(
        string host, string username, string password, int port,
        string command, string? localDirectory, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host)) return Fail("Host is required.");
        if (string.IsNullOrWhiteSpace(username)) return Fail("Username is required.");

        try
        {
            return await Task.Run(() =>
            {
                using var client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                client.Connect();
                if (!client.IsConnected) return Fail("SSH connect failed.");

                using var shell = client.CreateShellStream("vt100", 120, 40, 800, 600, 4096);
                Thread.Sleep(700);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(4));

                shell.Write("set cli pager off\n");
                Thread.Sleep(300);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(2));

                shell.Write(command + "\n");
                var output = ReadUntilQuiet(shell, TimeSpan.FromSeconds(90));
                client.Disconnect();

                string? path = null;
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                    var safe = string.Join("_", host.Split(Path.GetInvalidFileNameChars()));
                    path = Path.Combine(localDirectory, $"palo_{safe}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                }

                var preview = output.Length > 4000 ? output[..4000] + "\n…" : output;
                return new DeviceBackupResult
                {
                    Success = true,
                    Message = path is null ? "PAN-OS command OK" : "Saved: " + path,
                    LocalPath = path,
                    Preview = preview
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static string ReadUntilQuiet(ShellStream shell, TimeSpan maxWait)
    {
        var sb = new StringBuilder();
        var deadline = DateTime.UtcNow + maxWait;
        var idle = 0;
        while (DateTime.UtcNow < deadline && idle < 10)
        {
            var chunk = shell.Read();
            if (!string.IsNullOrEmpty(chunk))
            {
                sb.Append(chunk);
                idle = 0;
            }
            else
            {
                idle++;
                Thread.Sleep(200);
            }
        }
        return sb.ToString();
    }

    private static DeviceBackupResult Fail(string msg) => new() { Success = false, Message = msg };
}
