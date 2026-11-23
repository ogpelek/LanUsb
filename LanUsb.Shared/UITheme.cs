using System.Drawing.Drawing2D;

namespace LanUsb.Shared;

public static class UITheme
{
    // Deep modern dark palette (Slate / Obsidian)
    public static readonly Color BgDarkest = Color.FromArgb(13, 16, 23);     // #0D1017 window background
    public static readonly Color BgSurface = Color.FromArgb(20, 26, 38);     // #141A26 card/panel background
    public static readonly Color BgHeader = Color.FromArgb(25, 33, 49);      // #192131 elevated header/banner
    public static readonly Color BgSurfaceHover = Color.FromArgb(32, 42, 62);
    public static readonly Color BgInput = Color.FromArgb(15, 19, 29);
    
    // Borders
    public static readonly Color Border = Color.FromArgb(45, 56, 80);
    public static readonly Color BorderSubtle = Color.FromArgb(32, 40, 58);
    
    // Typography
    public static readonly Color TextPrimary = Color.FromArgb(245, 247, 252);
    public static readonly Color TextSecondary = Color.FromArgb(155, 170, 194);
    public static readonly Color TextMuted = Color.FromArgb(105, 120, 145);
    
    // Accents
    public static readonly Color Green = Color.FromArgb(16, 185, 129);       // Emerald #10B981 (Success/Bind/Online)
    public static readonly Color GreenDark = Color.FromArgb(5, 150, 105);
    public static readonly Color GreenBg = Color.FromArgb(6, 78, 59);
    
    public static readonly Color Blue = Color.FromArgb(59, 130, 246);        // Electric Blue #3B82F6 (Primary action)
    public static readonly Color BlueDark = Color.FromArgb(37, 99, 235);
    public static readonly Color BlueBg = Color.FromArgb(30, 58, 138);
    
    public static readonly Color Amber = Color.FromArgb(245, 158, 11);       // Amber #F59E0B (Force / Auto-Connect)
    public static readonly Color AmberDark = Color.FromArgb(217, 119, 6);
    public static readonly Color AmberBg = Color.FromArgb(120, 53, 15);
    
    public static readonly Color Red = Color.FromArgb(239, 68, 68);          // Rose Red #EF4444 (Unbind / Detach)
    public static readonly Color RedDark = Color.FromArgb(220, 38, 38);
    public static readonly Color RedBg = Color.FromArgb(127, 29, 29);
    
    public static readonly Color Indigo = Color.FromArgb(99, 102, 241);      // Indigo #6366F1
    public static readonly Color SlateButton = Color.FromArgb(38, 48, 70);
    public static readonly Color SlateButtonHover = Color.FromArgb(48, 60, 88);

    // Standard Fonts
    public static Font HeaderFont => new("Segoe UI", 12f, FontStyle.Bold);
    public static Font TitleFont => new("Segoe UI Semibold", 10.5f, FontStyle.Bold);
    public static Font RegularFont => new("Segoe UI", 9.25f);
    public static Font BoldFont => new("Segoe UI", 9.25f, FontStyle.Bold);
    public static Font SmallFont => new("Segoe UI", 8.25f);
    public static Font SmallBoldFont => new("Segoe UI", 8.25f, FontStyle.Bold);
    public static Font StatValueFont => new("Segoe UI", 14f, FontStyle.Bold);
    public static Font CodeFont => new("Consolas", 9f);

    public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = radius * 2;
        var r = rect;
        r.Width -= 1;
        r.Height -= 1;

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
