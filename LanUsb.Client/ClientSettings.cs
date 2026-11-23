using System.Text.Json;
using LanUsb.Shared;

namespace LanUsb.Client;

public sealed class ClientSettings
{
    public List<string> ManualServers { get; set; } = new();
    public HashSet<string> AutoConnectKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanUsb", "client-settings.json");

    public static string MakeAutoKey(string serverHostOrIp, string vidPid) =>
        $"{serverHostOrIp}|{vidPid}".ToLowerInvariant();

    public static ClientSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ClientSettings>(json, NetworkHelper.JsonOptions) ?? new ClientSettings();
            }
        }
        catch
        {
        }
        return new ClientSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
        }
    }
}
