using System.Text.RegularExpressions;

namespace LanUsb.Shared;

public static class UsbipClientCli
{
    private static readonly Regex PortHeaderRegex = new(
        @"^Port\s+(\d+):",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UsbipUriRegex = new(
        @"usbip://([^:/]+)(?::\d+)?/([^\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RemoteBusRowRegex = new(
        @"^\s*(\d+-\d+(?:\.\d+)*):\s*(.+?)\s*\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\)\s*$",
        RegexOptions.Compiled);

    public static string? FindUsbipPath() =>
        ProcessRunner.FindExecutable(
            "usbip.exe",
            @"C:\Program Files\usbip-win2\usbip.exe",
            @"C:\Program Files\usbip-win\usbip.exe",
            @"C:\Program Files\usbip\usbip.exe",
            @"C:\usbip\usbip.exe");

    public static async Task<CommandResult> AttachAsync(string host, string busId, CancellationToken ct = default)
    {
        var exe = FindUsbipPath();
        if (exe == null)
            return new CommandResult { Success = false, ExitCode = -1, StdErr = "Nie znaleziono usbip.exe (usbip-win2)." };

        return await ProcessRunner.RunAsync(exe, $"attach -r {host} -b {busId}", 15000, ct).ConfigureAwait(false);
    }

    public static async Task<CommandResult> DetachAsync(int port, CancellationToken ct = default)
    {
        var exe = FindUsbipPath();
        if (exe == null)
            return new CommandResult { Success = false, ExitCode = -1, StdErr = "Nie znaleziono usbip.exe (usbip-win2)." };

        return await ProcessRunner.RunAsync(exe, $"detach -p {port}", 10000, ct).ConfigureAwait(false);
    }

    public static async Task<List<AttachedPortInfo>> GetAttachedPortsAsync(CancellationToken ct = default)
    {
        var list = new List<AttachedPortInfo>();
        var exe = FindUsbipPath();
        if (exe == null)
            return list;

        var res = await ProcessRunner.RunAsync(exe, "port", 8000, ct).ConfigureAwait(false);
        if (!res.Success && string.IsNullOrWhiteSpace(res.StdOut))
            return list;

        AttachedPortInfo? current = null;
        var lines = res.StdOut.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var headerMatch = PortHeaderRegex.Match(line);
            if (headerMatch.Success && int.TryParse(headerMatch.Groups[1].Value, out var portNum))
            {
                current = new AttachedPortInfo { Port = portNum };
                list.Add(current);
                continue;
            }

            if (current == null)
                continue;

            var uriMatch = UsbipUriRegex.Match(line);
            if (uriMatch.Success)
            {
                current.RemoteHost = uriMatch.Groups[1].Value;
                current.BusId = uriMatch.Groups[2].Value;
            }
            else if (string.IsNullOrEmpty(current.Description) && !line.StartsWith("->", StringComparison.Ordinal))
            {
                current.Description = line;
            }
        }

        return list;
    }

    public static async Task<(bool Ok, string? Error, List<UsbDeviceInfo> Devices)> ListRemoteAsync(
        string host,
        CancellationToken ct = default)
    {
        var exe = FindUsbipPath();
        if (exe == null)
            return (false, "Nie znaleziono usbip.exe (usbip-win2).", new List<UsbDeviceInfo>());

        var res = await ProcessRunner.RunAsync(exe, $"list -r {host}", 8000, ct).ConfigureAwait(false);
        if (!res.Success && string.IsNullOrWhiteSpace(res.StdOut))
            return (false, res.CombinedOutput, new List<UsbDeviceInfo>());

        var devices = new List<UsbDeviceInfo>();
        var lines = res.StdOut.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var match = RemoteBusRowRegex.Match(line);
            if (!match.Success)
                continue;

            devices.Add(new UsbDeviceInfo
            {
                BusId = match.Groups[1].Value.Trim(),
                Description = match.Groups[2].Value.Trim(),
                VidPid = match.Groups[3].Value.Trim().ToLowerInvariant(),
                State = UsbDeviceState.Shared,
                RawState = "Shared"
            });
        }

        return (true, null, devices);
    }

    public static Task<CommandResult> InstallWithWingetAsync(CancellationToken ct = default)
    {
        return ProcessRunner.RunAsync(
            "winget.exe",
            "install --exact --id vadimgrn.usbip-win2 --accept-package-agreements --accept-source-agreements",
            180000,
            ct);
    }
}
