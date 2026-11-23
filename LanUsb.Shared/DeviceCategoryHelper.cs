namespace LanUsb.Shared;

public static class DeviceCategoryHelper
{
    public static (string CategoryName, string Icon, Color AccentColor) GetCategory(string description, string vidPid)
    {
        var text = (description + " " + vidPid).ToLowerInvariant();

        // 1. Stream Deck / Kontrolery streamerskie
        if (text.Contains("stream deck") || text.Contains("loupedeck") || text.Contains("pedal") ||
            text.Contains("0fd9:") || text.Contains("hotas") || text.Contains("joystick") ||
            text.Contains("gamepad") || text.Contains("flight") || text.Contains("macro"))
        {
            return ("Kontroler / Stream Deck", "🎛️", UITheme.Indigo);
        }

        // 2. Kamery i przechwytywanie wideo
        if (text.Contains("camera") || text.Contains("kamera") || text.Contains("cam") ||
            text.Contains("webcam") || text.Contains("c920") || text.Contains("brio") ||
            text.Contains("kapture") || text.Contains("capture") || text.Contains("obsbot") ||
            text.Contains("avermedia") || text.Contains("elgato cam") || text.Contains("video"))
        {
            return ("Kamera / Wideo", "📷", UITheme.Blue);
        }

        // 3. Audio, mikrofony, słuchawki
        if (text.Contains("audio") || text.Contains("sound") || text.Contains("arctis") ||
            text.Contains("nova") || text.Contains("headset") || text.Contains("headphone") ||
            text.Contains("yeti") || text.Contains("shure") || text.Contains("microphone") ||
            text.Contains("mikrofon") || text.Contains("scarlett") || text.Contains("rode") ||
            text.Contains("dac") || text.Contains("speakers") || text.Contains("g933") ||
            text.Contains("g733") || text.Contains("quadcast"))
        {
            return ("Audio / Mikrofon", "🎙️", UITheme.Green);
        }

        // 4. Sieć, Wi-Fi, Bluetooth
        if (text.Contains("wlan") || text.Contains("wireless") || text.Contains("802.11") ||
            text.Contains("bluetooth") || text.Contains("nic") || text.Contains("ethernet") ||
            text.Contains("lan") || text.Contains("realtek 8812"))
        {
            return ("Sieć / Łączność", "📶", Color.FromArgb(14, 165, 233));
        }

        // 5. Pamięć masowa USB
        if (text.Contains("pamięć masowa") || text.Contains("mass storage") || text.Contains("scsi") ||
            text.Contains("uas") || text.Contains("flash") || text.Contains("disk") ||
            text.Contains("sandisk") || text.Contains("kingston") || text.Contains("crucial") ||
            text.Contains("seagate") || text.Contains("drive"))
        {
            return ("Pamięć masowa", "💾", UITheme.Amber);
        }

        // 6. Urządzenia wejściowe (mysz, klawiatura, tablet)
        if (text.Contains("wejściowe usb") || text.Contains("klawiatura") || text.Contains("keyboard") ||
            text.Contains("mysz") || text.Contains("mouse") || text.Contains("hid") ||
            text.Contains("tablet") || text.Contains("wacom"))
        {
            return ("Urządzenie wejściowe", "⌨️", Color.FromArgb(168, 85, 247));
        }

        // 7. Domyślne USB
        return ("Urządzenie USB", "🔌", UITheme.TextSecondary);
    }
}
