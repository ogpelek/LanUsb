using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LanUsb.Shared;

public static class NetworkHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static List<string> GetLocalLanIPv4Addresses()
    {
        var ips = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    var ipStr = addr.Address.ToString();
                    if (ipStr.StartsWith("127.", StringComparison.Ordinal) ||
                        ipStr.StartsWith("169.254.", StringComparison.Ordinal))
                        continue;

                    ips.Add(ipStr);
                }
            }
        }
        catch
        {
            // Ignore network enumeration errors on restricted environments
        }

        return ips.Distinct().ToList();
    }

    public static async Task BroadcastUdpAsync(byte[] payload, int port, CancellationToken ct = default)
    {
        using var udp = new UdpClient { EnableBroadcast = true };
        var targets = new HashSet<IPAddress> { IPAddress.Broadcast };

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork || addr.IPv4Mask == null)
                        continue;

                    var ipBytes = addr.Address.GetAddressBytes();
                    var maskBytes = addr.IPv4Mask.GetAddressBytes();
                    var bcastBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                        bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

                    targets.Add(new IPAddress(bcastBytes));
                }
            }
        }
        catch
        {
            // Fall back to 255.255.255.255
        }

        foreach (var target in targets)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await udp.SendAsync(payload, payload.Length, new IPEndPoint(target, port)).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort per interface
            }
        }
    }

    public static async Task<ControlResponse> SendControlRequestAsync(
        string host,
        int port,
        ControlRequest request,
        int timeoutMs = 4000,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            var reqJson = JsonSerializer.Serialize(request, JsonOptions) + "\n";
            var reqBytes = Encoding.UTF8.GetBytes(reqJson);
            await stream.WriteAsync(reqBytes, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                return new ControlResponse { Success = false, Message = "Pusta odpowiedź serwera." };

            return JsonSerializer.Deserialize<ControlResponse>(line, JsonOptions)
                ?? new ControlResponse { Success = false, Message = "Nieprawidłowa odpowiedź JSON." };
        }
        catch (Exception ex)
        {
            return new ControlResponse { Success = false, Message = ex.Message };
        }
    }
}
