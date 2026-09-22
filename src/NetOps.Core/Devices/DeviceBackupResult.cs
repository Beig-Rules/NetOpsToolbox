namespace NetOps.Core.Devices;

public sealed class DeviceBackupResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? LocalPath { get; init; }
    public string? Preview { get; init; }
}
