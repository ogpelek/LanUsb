using System.Text.Json.Serialization;

namespace LanUsb.Shared;

public static class LanUsbConstants
{
    public const int DiscoveryUdpPort = 32400;
    public const int ControlTcpPort = 32401;
    public const int UsbipTcpPort = 3240;
    public const string ProtocolMagic = "LANUSB_V1";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UsbDeviceState
{
    Unknown,
    NotShared,
    Shared,
    Attached
}

public sealed class UsbDeviceInfo
{
    public string BusId { get; set; } = string.Empty;
    public string VidPid { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public UsbDeviceState State { get; set; } = UsbDeviceState.Unknown;
    public string RawState { get; set; } = string.Empty;
    public bool IsPersisted { get; set; }
}

public sealed class ServerBeacon
{
    public string Magic { get; set; } = LanUsbConstants.ProtocolMagic;
    public string ServerId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    public int UsbipPort { get; set; } = LanUsbConstants.UsbipTcpPort;
    public int ControlPort { get; set; } = LanUsbConstants.ControlTcpPort;
    public bool AllowRemoteBind { get; set; }
    public bool UsbipdInstalled { get; set; }
    public bool UsbipdRunning { get; set; }
    public List<UsbDeviceInfo> Devices { get; set; } = new();
    public long TimestampUnixMs { get; set; }
}

public sealed class ControlRequest
{
    public string Action { get; set; } = string.Empty; // "list", "bind", "unbind"
    public string? BusId { get; set; }
    public bool Force { get; set; }
}

public sealed class ControlResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<UsbDeviceInfo>? Devices { get; set; }
}

public sealed class AttachedPortInfo
{
    public int Port { get; set; }
    public string BusId { get; set; } = string.Empty;
    public string? RemoteHost { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class CommandResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;

    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : $"{StdOut}\n{StdErr}".Trim();
}
