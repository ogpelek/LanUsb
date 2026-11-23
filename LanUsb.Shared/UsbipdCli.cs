using System.Text.RegularExpressions;

namespace LanUsb.Shared;

public static class UsbipdCli
{
    private static readonly Regex ConnectedRowRegex = new(
        @"^\s*(\d+-\d+(?:\.\d+)*)\s+([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\s+(.+?)\s{2,}(Not shared|Shared(?:\s*\([^)]+\))?|Attached.*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? FindUsbipdPath() =>
        ProcessRunner.FindExecutable(
            "usbipd.exe",
            @"C:\Program Files\usbipd-win\usbipd.exe",
            @"C:\Program Files (x86)\usbipd-win\usbipd.exe");

    public static async Task<bool> IsServiceRunningAsync(CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync("sc.exe", "query usbipd", 5000, ct).ConfigureAwait(false);
        return result.Success && result.StdOut.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<CommandResult> StartServiceAsync(CancellationToken ct = default)
    {
        var res = await ProcessRunner.RunAsync("sc.exe", "start usbipd", 10000, ct).ConfigureAwait(false);
        if (!res.Success && NeedsElevation(res.CombinedOutput))
        {
            return await ProcessRunner.RunElevatedAsync("sc.exe", "start usbipd", 20000, ct).ConfigureAwait(false);
        }
        return res;
    }

    public static async Task<(bool Ok, string? Error, List<UsbDeviceInfo> Devices)> GetDevicesAsync(CancellationToken ct = default)
    {
        var exe = FindUsbipdPath();
        if (exe == null)
            return (false, "Nie znaleziono usbipd-win (usbipd.exe).", new List<UsbDeviceInfo>());

        var result = await ProcessRunner.RunAsync(exe, "list", 10000, ct).ConfigureAwait(false);
        var devices = ParseListOutput(result.StdOut);

        if (!result.Success && devices.Count == 0)
        {
            return (false, result.CombinedOutput, devices);
        }

        return (true, null, devices);
    }

    public static List<UsbDeviceInfo> ParseListOutput(string output)
    {
        var devices = new List<UsbDeviceInfo>();
        if (string.IsNullOrWhiteSpace(output))
            return devices;

        var inConnected = false;
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("Connected:", StringComparison.OrdinalIgnoreCase))
            {
                inConnected = true;
                continue;
            }
            if (line.StartsWith("Persisted:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (!inConnected || string.IsNullOrWhiteSpace(line) || line.StartsWith("BUSID", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = ConnectedRowRegex.Match(line);
            if (!match.Success)
                continue;

            var rawState = match.Groups[4].Value.Trim();
            devices.Add(new UsbDeviceInfo
            {
                BusId = match.Groups[1].Value.Trim(),
                VidPid = match.Groups[2].Value.Trim().ToLowerInvariant(),
                Description = match.Groups[3].Value.Trim(),
                RawState = rawState,
                State = ParseState(rawState)
            });
        }

        return devices;
    }

    private static UsbDeviceState ParseState(string raw)
    {
        if (raw.StartsWith("Not shared", StringComparison.OrdinalIgnoreCase))
            return UsbDeviceState.NotShared;
        if (raw.StartsWith("Attached", StringComparison.OrdinalIgnoreCase))
            return UsbDeviceState.Attached;
        if (raw.StartsWith("Shared", StringComparison.OrdinalIgnoreCase))
            return UsbDeviceState.Shared;
        return UsbDeviceState.Unknown;
    }

    public static async Task<CommandResult> BindAsync(string busId, bool force = false, CancellationToken ct = default)
    {
        var exe = FindUsbipdPath();
        if (exe == null)
            return new CommandResult { Success = false, ExitCode = -1, StdErr = "Brak usbipd.exe" };

        var args = force ? $"bind --busid {busId} --force" : $"bind --busid {busId}";
        var res = await ProcessRunner.RunAsync(exe, args, 15000, ct).ConfigureAwait(false);
        if (!res.Success && NeedsElevation(res.CombinedOutput))
        {
            res = await ProcessRunner.RunElevatedAsync(exe, args, 30000, ct).ConfigureAwait(false);
        }
        return res;
    }

    public static async Task<CommandResult> UnbindAsync(string busId, CancellationToken ct = default)
    {
        var exe = FindUsbipdPath();
        if (exe == null)
            return new CommandResult { Success = false, ExitCode = -1, StdErr = "Brak usbipd.exe" };

        var args = $"unbind --busid {busId}";
        var res = await ProcessRunner.RunAsync(exe, args, 15000, ct).ConfigureAwait(false);
        if (!res.Success && NeedsElevation(res.CombinedOutput))
        {
            res = await ProcessRunner.RunElevatedAsync(exe, args, 30000, ct).ConfigureAwait(false);
        }
        return res;
    }

    public static Task<CommandResult> InstallWithWingetAsync(CancellationToken ct = default)
    {
        return ProcessRunner.RunAsync(
            "winget.exe",
            "install --exact --id dorssel.usbipd-win --accept-package-agreements --accept-source-agreements",
            180000,
            ct);
    }

    private static bool NeedsElevation(string output) =>
        output.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("elevated", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Odmowa dostępu", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("uprawnień", StringComparison.OrdinalIgnoreCase);
}
