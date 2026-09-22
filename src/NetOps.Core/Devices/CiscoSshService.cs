using System.Text;
using Renci.SshNet;

namespace NetOps.Core.Devices;

/// <summary>
/// Cisco IOS / IOS-XE over SSH with optional enable password.
/// </summary>
public sealed class CiscoSshService
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

                using var shell = client.CreateShellStream("vt100", 80, 24, 800, 600, 1024);
                Thread.Sleep(500);
                var banner = ReadUntilQuiet(shell, TimeSpan.FromSeconds(3));

                // Enter privileged EXEC if enable password provided
                if (!string.IsNullOrEmpty(enablePassword))
                {
                    shell.WriteLine("enable");
                    Thread.Sleep(400);
                    var enablePrompt = ReadUntilQuiet(shell, TimeSpan.FromSeconds(4));
                    var combined = (banner + enablePrompt).ToLowerInvariant();
                    if (combined.Contains("password"))
                    {
                        shell.WriteLine(enablePassword);
                        Thread.Sleep(500);
                        ReadUntilQuiet(shell, TimeSpan.FromSeconds(3));
                    }
                }

                shell.WriteLine("terminal length 0");
                Thread.Sleep(300);
                Drain(shell);

                shell.WriteLine(showCommand);
                var output = ReadUntilQuiet(shell, TimeSpan.FromSeconds(50));
                client.Disconnect();

                output = CleanCiscoOutput(output, showCommand);
                if (string.IsNullOrWhiteSpace(output))
                    return Fail("Empty output — try enable password, privilege 15 user, or check AAA.");

                string? path = null;
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                    var safeHost = string.Concat(host.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
                    path = Path.Combine(localDirectory, $"cisco-{safeHost}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                }

                return new DeviceBackupResult
                {
                    Success = true,
                    Message = path is null
                        ? (string.IsNullOrEmpty(enablePassword) ? "Command OK" : "Command OK (enable used)")
                        : $"Saved ({output.Length} chars).",
                    LocalPath = path,
                    Preview = output.Length > 1500 ? output[..1500] + "\n…" : output
                };
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail("Cisco SSH: " + ex.Message);
        }
    }

    private static void Drain(ShellStream shell)
    {
        while (shell.DataAvailable)
            _ = shell.Read();
    }

    private static string ReadUntilQuiet(ShellStream shell, TimeSpan maxWait)
    {
        var sb = new StringBuilder();
        var start = DateTime.UtcNow;
        var lastData = DateTime.UtcNow;
        while (DateTime.UtcNow - start < maxWait)
        {
            if (shell.DataAvailable)
            {
                sb.Append(shell.Read());
                lastData = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - lastData > TimeSpan.FromMilliseconds(850))
                break;
            else
                Thread.Sleep(70);
        }
        return sb.ToString();
    }

    private static string CleanCiscoOutput(string raw, string cmd)
    {
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var list = new List<string>();
        foreach (var line in lines)
        {
            var t = line.TrimEnd();
            if (t.Equals(cmd, StringComparison.OrdinalIgnoreCase)) continue;
            if (t.Equals("terminal length 0", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.Equals("enable", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.Contains("Password:", StringComparison.OrdinalIgnoreCase) && t.Length < 24) continue;
            list.Add(t);
        }
        return string.Join('\n', list).Trim();
    }

    private static DeviceBackupResult Fail(string m) => new() { Success = false, Message = m };
}
