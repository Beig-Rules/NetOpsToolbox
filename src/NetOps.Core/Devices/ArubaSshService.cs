using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>HPE Aruba AOS-CX / AOS-S SSH — show commands, optional enable.</summary>
public sealed class ArubaSshService
{
    public Task<DeviceBackupResult> ShowVersionAsync(
        string host, string username, string password,
        int port = 22, string? enablePassword = null, CancellationToken ct = default)
        => RunShowAsync(host, username, password, port, "show version", null, enablePassword, ct);

    public Task<DeviceBackupResult> ShowRunningConfigAsync(
        string host, string username, string password, string localDirectory,
        int port = 22, string? enablePassword = null, CancellationToken ct = default)
        => RunShowAsync(host, username, password, port, "show running-config", localDirectory, enablePassword, ct);

    private async Task<DeviceBackupResult> RunShowAsync(
        string host, string username, string password, int port,
        string showCommand, string? localDirectory, string? enablePassword, CancellationToken ct)
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

                using var shell = client.CreateShellStream("vt100", 120, 40, 800, 600, 2048);
                Thread.Sleep(500);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(3));
                if (!string.IsNullOrEmpty(enablePassword))
                {
                    shell.Write("enable\n");
                    Thread.Sleep(300);
                    shell.Write(enablePassword + "\n");
                    Thread.Sleep(400);
                    _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(2));
                }
                shell.Write("no page\n");
                Thread.Sleep(200);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(1));
                shell.Write(showCommand + "\n");
                var output = ReadUntilQuiet(shell, TimeSpan.FromSeconds(50));
                client.Disconnect();

                string? path = null;
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                    var safe = string.Join("_", host.Split(Path.GetInvalidFileNameChars()));
                    path = Path.Combine(localDirectory, $"aruba_{safe}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                }
                var preview = output.Length > 4000 ? output[..4000] + "\n…" : output;
                return new DeviceBackupResult
                {
                    Success = true,
                    Message = path is null ? "Aruba command OK" : "Saved: " + path,
                    LocalPath = path,
                    Preview = preview
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static string ReadUntilQuiet(ShellStream shell, TimeSpan maxWait)
    {
        var sb = new StringBuilder();
        var deadline = DateTime.UtcNow + maxWait;
        var idle = 0;
        while (DateTime.UtcNow < deadline && idle < 8)
        {
            var chunk = shell.Read();
            if (!string.IsNullOrEmpty(chunk)) { sb.Append(chunk); idle = 0; }
            else { idle++; Thread.Sleep(150); }
        }
        return sb.ToString();
    }

    private static DeviceBackupResult Fail(string msg) => new() { Success = false, Message = msg };
}
