using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>Juniper Junos over SSH — read-only show commands.</summary>
public sealed class JuniperSshService
{
    public Task<DeviceBackupResult> ShowVersionAsync(
        string host, string username, string password,
        int port = 22, CancellationToken ct = default)
        => RunCliAsync(host, username, password, port, "show version | no-more", null, ct);

    public Task<DeviceBackupResult> ShowConfigAsync(
        string host, string username, string password, string localDirectory,
        int port = 22, CancellationToken ct = default)
        => RunCliAsync(host, username, password, port, "show configuration | display set | no-more", localDirectory, ct);

    public Task<DeviceBackupResult> ShowInterfacesAsync(
        string host, string username, string password,
        int port = 22, CancellationToken ct = default)
        => RunCliAsync(host, username, password, port, "show interfaces terse | no-more", null, ct);

    private async Task<DeviceBackupResult> RunCliAsync(
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
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(25);
                client.Connect();
                if (!client.IsConnected) return Fail("SSH connect failed.");

                using var shell = client.CreateShellStream("vt100", 120, 40, 800, 600, 2048);
                Thread.Sleep(400);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(2));
                shell.Write("set cli screen-length 0\n");
                Thread.Sleep(200);
                _ = ReadUntilQuiet(shell, TimeSpan.FromSeconds(1));
                shell.Write(command + "\n");
                var output = ReadUntilQuiet(shell, TimeSpan.FromSeconds(45));
                client.Disconnect();

                string? path = null;
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                    var safe = string.Join("_", host.Split(Path.GetInvalidFileNameChars()));
                    path = Path.Combine(localDirectory, $"junos_{safe}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                }
                var preview = output.Length > 4000 ? output[..4000] + "\n…" : output;
                return new DeviceBackupResult
                {
                    Success = true,
                    Message = path is null ? "Junos command OK" : "Saved: " + path,
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
