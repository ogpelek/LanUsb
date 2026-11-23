using System.Diagnostics;
using System.Text.Json;

namespace LanUsb.Shared;

public static class DriverInstaller
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    static DriverInstaller()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("LanUsb-DriverInstaller/1.0");
    }

    public static async Task<(bool Success, string Message)> InstallClientDriverAsync(
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Invoke("Sprawdzanie obecności sterownika usbip-win2...");
        if (UsbipClientCli.FindUsbipPath() != null)
        {
            return (true, "Sterownik usbip-win2 jest już zainstalowany!");
        }

        // 1. Try winget
        progress?.Invoke("[1/2] Próba instalacji za pomocą winget...");
        var wingetRes = await UsbipClientCli.InstallWithWingetAsync(ct).ConfigureAwait(false);
        if (wingetRes.Success && UsbipClientCli.FindUsbipPath() != null)
        {
            return (true, "Pomyślnie zainstalowano sterownik usbip-win2 za pomocą winget.");
        }

        // 2. Direct download from GitHub
        progress?.Invoke("[2/2] Pobieranie najnowszego instalatora usbip-win2 bezpośrednio z GitHub...");
        try
        {
            var (dlOk, filePath, dlMsg) = await DownloadGitHubReleaseAssetAsync(
                "vadimgrn",
                "usbip-win2",
                name => name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                progress,
                ct).ConfigureAwait(false);

            if (!dlOk || string.IsNullOrEmpty(filePath))
            {
                return (false, $"Nie udało się pobrać instalatora: {dlMsg}");
            }

            progress?.Invoke($"Uruchamianie instalatora: {Path.GetFileName(filePath)} (zaakceptuj uprawnienia UAC)...");
            var execRes = await ExecuteInstallerAsync(filePath, ct).ConfigureAwait(false);

            if (UsbipClientCli.FindUsbipPath() != null || execRes.Success)
            {
                return (true, "Sterownik usbip-win2 został pomyślnie zainstalowany.");
            }

            return (false, $"Instalator zakończył działanie z kodem {execRes.ExitCode}: {execRes.CombinedOutput}");
        }
        catch (Exception ex)
        {
            return (false, $"Błąd instalacji usbip-win2: {ex.Message}");
        }
    }

    public static async Task<(bool Success, string Message)> InstallServerDriverAsync(
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Invoke("Sprawdzanie obecności sterownika usbipd-win...");
        if (UsbipdCli.FindUsbipdPath() != null)
        {
            return (true, "Sterownik usbipd-win jest już zainstalowany!");
        }

        // 1. Try winget
        progress?.Invoke("[1/2] Próba instalacji za pomocą winget...");
        var wingetRes = await UsbipdCli.InstallWithWingetAsync(ct).ConfigureAwait(false);
        if (wingetRes.Success && UsbipdCli.FindUsbipdPath() != null)
        {
            return (true, "Pomyślnie zainstalowano sterownik usbipd-win za pomocą winget.");
        }

        // 2. Direct download from GitHub
        progress?.Invoke("[2/2] Pobieranie najnowszego instalatora usbipd-win bezpośrednio z GitHub...");
        try
        {
            var (dlOk, filePath, dlMsg) = await DownloadGitHubReleaseAssetAsync(
                "dorssel",
                "usbipd-win",
                name => name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase),
                progress,
                ct).ConfigureAwait(false);

            if (!dlOk || string.IsNullOrEmpty(filePath))
            {
                return (false, $"Nie udało się pobrać instalatora: {dlMsg}");
            }

            progress?.Invoke($"Uruchamianie instalatora: {Path.GetFileName(filePath)} (zaakceptuj uprawnienia UAC)...");
            var execRes = await ExecuteInstallerAsync(filePath, ct).ConfigureAwait(false);

            if (UsbipdCli.FindUsbipdPath() != null || execRes.Success)
            {
                return (true, "Sterownik usbipd-win został pomyślnie zainstalowany.");
            }

            return (false, $"Instalator zakończył działanie z kodem {execRes.ExitCode}: {execRes.CombinedOutput}");
        }
        catch (Exception ex)
        {
            return (false, $"Błąd instalacji usbipd-win: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string? FilePath, string Message)> DownloadGitHubReleaseAssetAsync(
        string owner,
        string repo,
        Func<string, bool> assetFilter,
        Action<string>? progress,
        CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0)
        {
            return (false, null, "Wydanie GitHub nie zawiera załączników instalatora.");
        }

        string? downloadUrl = null;
        string? fileName = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (!string.IsNullOrEmpty(name) && assetFilter(name))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                fileName = name;
                break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
        {
            return (false, null, "Nie znaleziono pasującego pliku instalatora .msi/.exe.");
        }

        var tempDir = Path.GetTempPath();
        var destinationPath = Path.Combine(tempDir, fileName);

        progress?.Invoke($"Pobieranie {fileName}...");
        using var resp = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        progress?.Invoke($"Pobrano pomyślnie: {destinationPath}");
        return (true, destinationPath, "Pobrano plik.");
    }

    private static async Task<CommandResult> ExecuteInstallerAsync(string filePath, CancellationToken ct)
    {
        if (filePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessRunner.RunElevatedAsync("msiexec.exe", $"/i \"{filePath}\"", 120000, ct).ConfigureAwait(false);
        }

        return await ProcessRunner.RunElevatedAsync(filePath, "", 120000, ct).ConfigureAwait(false);
    }

    public static void OpenWindowsNetworkSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:network",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.NetworkAndSharingCenter",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
    }

    public static async Task<CommandResult> SetNetworkProfileToPrivateAsync(CancellationToken ct = default)
    {
        var psCommand = "Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private";
        return await ProcessRunner.RunElevatedAsync("powershell.exe", $"-NoProfile -Command \"{psCommand}\"", 20000, ct).ConfigureAwait(false);
    }
}
