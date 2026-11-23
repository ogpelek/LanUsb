using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LanUsb.Shared;

namespace LanUsb.Client;

public sealed class ClientForm : Form
{
    private sealed class DiscoveredServer
    {
        public string Key { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string PrimaryIp { get; set; } = string.Empty;
        public bool IsManual { get; set; }
        public bool AllowRemoteBind { get; set; }
        public int ControlPort { get; set; } = LanUsbConstants.ControlTcpPort;
        public List<UsbDeviceInfo> Devices { get; set; } = new();
        public DateTime LastSeenUtc { get; set; }
    }

    private sealed class DeviceRowTag
    {
        public DiscoveredServer Server { get; init; } = null!;
        public UsbDeviceInfo Device { get; init; } = null!;
        public AttachedPortInfo? AttachedPort { get; init; }
    }

    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, DiscoveredServer> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ClientSettings _settings = ClientSettings.Load();

    // UI Header & Driver Banner
    private readonly Panel _pnlHeader = new();
    private readonly Label _lblBrand = new();
    private readonly Label _lblDriverBadge = new();
    private readonly Label _lblDetectedSummary = new();
    private readonly TextBox _txtManualIp = new();
    private readonly ModernButton _btnAddServer = new();
    private readonly ModernButton _btnRemoveServer = new();

    // Private Network Requirement Alert Panel
    private readonly Panel _pnlPrivateNetwork = new();
    private readonly Label _lblPrivateNetworkText = new();
    private readonly ModernButton _btnOpenNetworkSettings = new();
    private readonly ModernButton _btnAutoPrivate = new();
    private readonly Button _btnCloseNetworkAlert = new();

    // Driver Warning Banner
    private readonly Panel _pnlBanner = new();
    private readonly Label _lblBannerText = new();
    private readonly ModernButton _btnBannerAction = new();

    // Stats bar
    private readonly FlowLayoutPanel _pnlStats = new();
    private readonly StatCard _cardServers = new();
    private readonly StatCard _cardAvailableDevices = new();
    private readonly StatCard _cardAttachedDevices = new();
    private readonly StatCard _cardAutoRules = new();

    // Toolbar
    private readonly Panel _pnlToolbar = new();
    private readonly SearchBoxControl _searchBox = new();
    private readonly ModernButton _btnAttach = new();
    private readonly ModernButton _btnDetach = new();
    private readonly ModernButton _btnToggleAuto = new();
    private readonly ModernButton _btnDownloadDriver = new();
    private readonly ModernButton _btnRefresh = new();
    private readonly ModernButton _btnToggleNetworkInfo = new();
    private readonly CheckBox _chkMinToTray = new();

    // Devices List
    private readonly DoubleBufferedListView _lvDevices = new();
    private readonly ContextMenuStrip _contextMenu = new();

    // Footer / Log Drawer
    private readonly Panel _pnlFooter = new();
    private readonly Label _lblStatusStrip = new();
    private readonly ModernButton _btnToggleLog = new();
    private readonly ModernButton _btnClearLog = new();
    private readonly ModernButton _btnCopyLog = new();
    private readonly ModernButton _btnDonate = new();
    private readonly TextBox _txtLog = new();

    private readonly NotifyIcon _trayIcon = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new();

    private List<AttachedPortInfo> _attachedPorts = new();
    private bool _driverInstalled;
    private bool _isTickBusy;
    private bool _isLogExpanded = true;
    private bool _isInstallingDriver;

    public ClientForm()
    {
        Text = "LanUsb Client — Klient USB przez LAN (Free / Open Source)";
        Size = new Size(1140, 760);
        MinimumSize = new Size(940, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.BgDarkest;
        ForeColor = UITheme.TextPrimary;
        Font = UITheme.RegularFont;

        BuildUi();
        SetupContextMenu();
        SetupTray();

        _uiTimer.Interval = 2500;
        _uiTimer.Tick += async (_, _) => await RefreshLoopTickAsync();

        Load += async (_, _) =>
        {
            Log("Uruchamianie LanUsb Client (nasłuchiwanie serwerów na UDP 32400)...");
            _ = Task.Run(() => ListenUdpBeaconsAsync(_cts.Token));
            await RefreshLoopTickAsync();
            _uiTimer.Start();
        };

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && _chkMinToTray.Checked)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(1500, "LanUsb Client", "Klient działa w tle w zasobniku.", ToolTipIcon.Info);
                return;
            }

            _uiTimer.Stop();
            _cts.Cancel();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };
    }

    private void BuildUi()
    {
        SuspendLayout();

        // 1. HEADER
        _pnlHeader.Dock = DockStyle.Top;
        _pnlHeader.Height = 84;
        _pnlHeader.BackColor = UITheme.BgHeader;
        _pnlHeader.Padding = new Padding(18, 10, 18, 10);

        _lblBrand.Text = "⚡ LanUsb Client";
        _lblBrand.Font = UITheme.HeaderFont;
        _lblBrand.ForeColor = UITheme.TextPrimary;
        _lblBrand.AutoSize = true;
        _lblBrand.Location = new Point(18, 12);

        var lblSub = new Label
        {
            Text = "Odbiornik urządzeń USB przez sieć lokalną do streamowania (OBS, Discord, gry)",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextMuted,
            AutoSize = true,
            Location = new Point(20, 38)
        };

        _lblDriverBadge.Text = "● SPRAWDZANIE STEROWNIKA...";
        _lblDriverBadge.Font = UITheme.SmallBoldFont;
        _lblDriverBadge.ForeColor = UITheme.Green;
        _lblDriverBadge.BackColor = UITheme.GreenBg;
        _lblDriverBadge.Padding = new Padding(8, 4, 8, 4);
        _lblDriverBadge.AutoSize = true;
        _lblDriverBadge.Location = new Point(215, 14);

        // Manual IP Connect section
        var lblManual = new Label
        {
            Text = "Ręczne IP serwera:",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            AutoSize = true,
            Location = new Point(18, 58)
        };

        _txtManualIp.Location = new Point(135, 55);
        _txtManualIp.Width = 160;
        _txtManualIp.BackColor = UITheme.BgInput;
        _txtManualIp.ForeColor = UITheme.TextPrimary;
        _txtManualIp.BorderStyle = BorderStyle.FixedSingle;
        _txtManualIp.Font = UITheme.RegularFont;
        _txtManualIp.PlaceholderText = "np. 192.168.1.50";

        _btnAddServer.Text = "+ Dodaj IP";
        _btnAddServer.Size = new Size(95, 26);
        _btnAddServer.Location = new Point(302, 53);
        _btnAddServer.NormalColor = UITheme.Blue;
        _btnAddServer.HoverColor = UITheme.BlueDark;
        _btnAddServer.Font = UITheme.SmallBoldFont;
        _btnAddServer.Click += async (_, _) => await AddManualServerAsync();

        _btnRemoveServer.Text = "Usuń IP";
        _btnRemoveServer.Size = new Size(80, 26);
        _btnRemoveServer.Location = new Point(402, 53);
        _btnRemoveServer.NormalColor = UITheme.SlateButton;
        _btnRemoveServer.HoverColor = UITheme.SlateButtonHover;
        _btnRemoveServer.Font = UITheme.SmallFont;
        _btnRemoveServer.Click += (_, _) => RemoveSelectedManualServer();

        _lblDetectedSummary.Text = "Oczekiwanie na serwery w sieci LAN...";
        _lblDetectedSummary.Font = UITheme.SmallFont;
        _lblDetectedSummary.ForeColor = UITheme.TextSecondary;
        _lblDetectedSummary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblDetectedSummary.AutoSize = true;
        _lblDetectedSummary.Location = new Point(620, 26);

        _pnlHeader.Controls.AddRange(new Control[]
        {
            _lblBrand, lblSub, _lblDriverBadge, lblManual, _txtManualIp,
            _btnAddServer, _btnRemoveServer, _lblDetectedSummary
        });

        // 2. PRIVATE NETWORK REQUIREMENT ALERT
        _pnlPrivateNetwork.Dock = DockStyle.Top;
        _pnlPrivateNetwork.Height = 48;
        _pnlPrivateNetwork.BackColor = Color.FromArgb(28, 38, 56);
        _pnlPrivateNetwork.Padding = new Padding(16, 8, 16, 8);

        _lblPrivateNetworkText.Text = "⚠️ WYMÓG: Sieć w Windows musi być ustawiona jako \"Prywatna\" (Private). W publicznej zapora blokuje ruch LAN. Po zmianie na prywatną aplikacja zadziała!";
        _lblPrivateNetworkText.Font = UITheme.RegularFont;
        _lblPrivateNetworkText.ForeColor = Color.FromArgb(186, 230, 253);
        _lblPrivateNetworkText.AutoSize = true;
        _lblPrivateNetworkText.Location = new Point(16, 14);

        _btnOpenNetworkSettings.Text = "🌐 Ustawienia sieci";
        _btnOpenNetworkSettings.Size = new Size(135, 30);
        _btnOpenNetworkSettings.Location = new Point(810, 8);
        _btnOpenNetworkSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnOpenNetworkSettings.NormalColor = UITheme.Blue;
        _btnOpenNetworkSettings.HoverColor = UITheme.BlueDark;
        _btnOpenNetworkSettings.Font = UITheme.SmallBoldFont;
        _btnOpenNetworkSettings.Click += (_, _) => DriverInstaller.OpenWindowsNetworkSettings();

        _btnAutoPrivate.Text = "⚡ Zmień na Prywatną";
        _btnAutoPrivate.Size = new Size(140, 30);
        _btnAutoPrivate.Location = new Point(952, 8);
        _btnAutoPrivate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnAutoPrivate.NormalColor = UITheme.Amber;
        _btnAutoPrivate.HoverColor = UITheme.AmberDark;
        _btnAutoPrivate.Font = UITheme.SmallBoldFont;
        _btnAutoPrivate.Click += async (_, _) => await SwitchNetworkProfileToPrivateAsync();

        _btnCloseNetworkAlert.Text = "✕";
        _btnCloseNetworkAlert.Size = new Size(22, 22);
        _btnCloseNetworkAlert.Location = new Point(1098, 12);
        _btnCloseNetworkAlert.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnCloseNetworkAlert.FlatStyle = FlatStyle.Flat;
        _btnCloseNetworkAlert.FlatAppearance.BorderSize = 0;
        _btnCloseNetworkAlert.ForeColor = UITheme.TextMuted;
        _btnCloseNetworkAlert.BackColor = Color.Transparent;
        _btnCloseNetworkAlert.Cursor = Cursors.Hand;
        _btnCloseNetworkAlert.Click += (_, _) => _pnlPrivateNetwork.Visible = false;

        _pnlPrivateNetwork.Controls.AddRange(new Control[]
        {
            _lblPrivateNetworkText, _btnOpenNetworkSettings, _btnAutoPrivate, _btnCloseNetworkAlert
        });

        // 3. DRIVER WARNING BANNER
        _pnlBanner.Dock = DockStyle.Top;
        _pnlBanner.Height = 44;
        _pnlBanner.BackColor = Color.FromArgb(85, 30, 25);
        _pnlBanner.Padding = new Padding(18, 6, 18, 6);
        _pnlBanner.Visible = false;

        _lblBannerText.Text = "⚠️ Brak sterownika klienta usbip-win2 — wymagany do montowania wirtualnych portów USB.";
        _lblBannerText.Font = UITheme.RegularFont;
        _lblBannerText.ForeColor = Color.FromArgb(254, 215, 170);
        _lblBannerText.AutoSize = true;
        _lblBannerText.Location = new Point(18, 12);

        _btnBannerAction.Text = "⬇️ Pobierz automatycznie";
        _btnBannerAction.Size = new Size(190, 30);
        _btnBannerAction.Location = new Point(910, 7);
        _btnBannerAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnBannerAction.NormalColor = UITheme.Blue;
        _btnBannerAction.HoverColor = UITheme.BlueDark;
        _btnBannerAction.Click += async (_, _) => await InstallDriverAutomaticallyAsync();

        _pnlBanner.Controls.AddRange(new Control[] { _lblBannerText, _btnBannerAction });

        // 4. STATS PANEL
        _pnlStats.Dock = DockStyle.Top;
        _pnlStats.Height = 84;
        _pnlStats.BackColor = UITheme.BgDarkest;
        _pnlStats.Padding = new Padding(16, 8, 16, 8);
        _pnlStats.WrapContents = false;

        _cardServers.IconText = "🌐";
        _cardServers.Title = "SERWERY W LAN";
        _cardServers.ValueText = "0 znalezionych";
        _cardServers.SubText = "UDP 32400 / Ręczne";
        _cardServers.AccentColor = UITheme.Blue;
        _cardServers.Width = 240;

        _cardAvailableDevices.IconText = "🔌";
        _cardAvailableDevices.Title = "DOSTĘPNE W LAN";
        _cardAvailableDevices.ValueText = "0 urządzeń";
        _cardAvailableDevices.SubText = "Zdalne porty USB";
        _cardAvailableDevices.AccentColor = UITheme.Green;
        _cardAvailableDevices.Width = 240;

        _cardAttachedDevices.IconText = "⚡";
        _cardAttachedDevices.Title = "PODŁĄCZONE (LOCAL)";
        _cardAttachedDevices.ValueText = "0 aktywnych";
        _cardAttachedDevices.SubText = "Wirtualne USB w systemie";
        _cardAttachedDevices.AccentColor = UITheme.Indigo;
        _cardAttachedDevices.Width = 250;

        _cardAutoRules.IconText = "⭐";
        _cardAutoRules.Title = "AUTO-CONNECT";
        _cardAutoRules.ValueText = "0 reguł";
        _cardAutoRules.SubText = "Automatyczne łączenie";
        _cardAutoRules.AccentColor = UITheme.Amber;
        _cardAutoRules.Width = 240;

        _pnlStats.Controls.AddRange(new Control[]
        {
            _cardServers, _cardAvailableDevices, _cardAttachedDevices, _cardAutoRules
        });

        // 5. TOOLBAR
        _pnlToolbar.Dock = DockStyle.Top;
        _pnlToolbar.Height = 52;
        _pnlToolbar.BackColor = UITheme.BgSurface;
        _pnlToolbar.Padding = new Padding(16, 10, 16, 10);

        _searchBox.Location = new Point(16, 10);
        _searchBox.Width = 220;
        _searchBox.SearchTextChanged += (_, _) => UpdateUi();

        _btnAttach.Text = "🚀 Podłącz (Attach)";
        _btnAttach.Size = new Size(150, 32);
        _btnAttach.Location = new Point(246, 10);
        _btnAttach.NormalColor = UITheme.Green;
        _btnAttach.HoverColor = UITheme.GreenDark;
        _btnAttach.Click += async (_, _) => await AttachSelectedAsync();

        _btnDetach.Text = "🔌 Odłącz (Detach)";
        _btnDetach.Size = new Size(140, 32);
        _btnDetach.Location = new Point(402, 10);
        _btnDetach.NormalColor = UITheme.Red;
        _btnDetach.HoverColor = UITheme.RedDark;
        _btnDetach.Click += async (_, _) => await DetachSelectedAsync();

        _btnToggleAuto.Text = "⭐ Auto-łącz";
        _btnToggleAuto.Size = new Size(125, 32);
        _btnToggleAuto.Location = new Point(548, 10);
        _btnToggleAuto.NormalColor = UITheme.Amber;
        _btnToggleAuto.HoverColor = UITheme.AmberDark;
        _btnToggleAuto.Click += (_, _) => ToggleAutoConnectSelected();

        _btnDownloadDriver.Text = "⬇️ Pobierz sterownik";
        _btnDownloadDriver.Size = new Size(150, 32);
        _btnDownloadDriver.Location = new Point(679, 10);
        _btnDownloadDriver.NormalColor = UITheme.Blue;
        _btnDownloadDriver.HoverColor = UITheme.BlueDark;
        _btnDownloadDriver.Click += async (_, _) => await InstallDriverAutomaticallyAsync();

        _btnRefresh.Text = "🔄";
        _btnRefresh.Size = new Size(42, 32);
        _btnRefresh.Location = new Point(835, 10);
        _btnRefresh.NormalColor = UITheme.SlateButton;
        _btnRefresh.HoverColor = UITheme.SlateButtonHover;
        _btnRefresh.Click += async (_, _) => await RefreshLoopTickAsync();

        _btnToggleNetworkInfo.Text = "🛡️ Sieć prywatna";
        _btnToggleNetworkInfo.Size = new Size(115, 32);
        _btnToggleNetworkInfo.Location = new Point(883, 10);
        _btnToggleNetworkInfo.NormalColor = Color.FromArgb(40, 54, 80);
        _btnToggleNetworkInfo.HoverColor = Color.FromArgb(50, 68, 100);
        _btnToggleNetworkInfo.Font = UITheme.SmallFont;
        _btnToggleNetworkInfo.Click += (_, _) => _pnlPrivateNetwork.Visible = !_pnlPrivateNetwork.Visible;

        _chkMinToTray.Text = "Do Tray";
        _chkMinToTray.Font = UITheme.SmallFont;
        _chkMinToTray.ForeColor = UITheme.TextSecondary;
        _chkMinToTray.Checked = true;
        _chkMinToTray.AutoSize = true;
        _chkMinToTray.Location = new Point(1040, 18);
        _chkMinToTray.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _pnlToolbar.Controls.AddRange(new Control[]
        {
            _searchBox, _btnAttach, _btnDetach, _btnToggleAuto, _btnDownloadDriver,
            _btnRefresh, _btnToggleNetworkInfo, _chkMinToTray
        });

        // 6. DEVICE LISTVIEW
        _lvDevices.Dock = DockStyle.Fill;
        _lvDevices.Columns.Add("Serwer LAN", 175);
        _lvDevices.Columns.Add("Kategoria", 160);
        _lvDevices.Columns.Add("Nazwa urządzenia USB", 320);
        _lvDevices.Columns.Add("BusID", 80);
        _lvDevices.Columns.Add("VID:PID", 95);
        _lvDevices.Columns.Add("Stan połączenia", 190);
        _lvDevices.Columns.Add("Auto-łącz", 80);
        _lvDevices.DoubleClick += async (_, _) =>
        {
            if (_lvDevices.SelectedItems.Count == 0) return;
            if (_lvDevices.SelectedItems[0].Tag is not DeviceRowTag tag) return;
            if (tag.AttachedPort != null)
                await DetachSelectedAsync();
            else
                await AttachSelectedAsync();
        };

        // 7. FOOTER / LOG DRAWER
        _pnlFooter.Dock = DockStyle.Bottom;
        _pnlFooter.Height = 175;
        _pnlFooter.BackColor = UITheme.BgSurface;

        var pnlStatusStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = UITheme.BgHeader,
            Padding = new Padding(14, 6, 14, 6)
        };

        _lblStatusStrip.Text = "Status: Gotowy | Szybki dwuklik na urządzeniu = natychmiastowe Podłącz / Odłącz";
        _lblStatusStrip.Font = UITheme.SmallFont;
        _lblStatusStrip.ForeColor = UITheme.TextSecondary;
        _lblStatusStrip.AutoSize = true;
        _lblStatusStrip.Location = new Point(14, 8);

        _btnDonate.Text = "☕ Wesprzyj";
        _btnDonate.Size = new Size(95, 24);
        _btnDonate.Font = UITheme.SmallBoldFont;
        _btnDonate.NormalColor = UITheme.Amber;
        _btnDonate.HoverColor = UITheme.AmberDark;
        _btnDonate.Location = new Point(675, 4);
        _btnDonate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnDonate.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://buycoffee.to/pelek2g") { UseShellExecute = true });
            }
            catch { }
        };

        _btnToggleLog.Text = "Logi (Zwiń ▲)";
        _btnToggleLog.Size = new Size(110, 24);
        _btnToggleLog.Font = UITheme.SmallFont;
        _btnToggleLog.Location = new Point(780, 4);
        _btnToggleLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnToggleLog.Click += (_, _) => ToggleLogDrawer();

        _btnClearLog.Text = "Wyczyść";
        _btnClearLog.Size = new Size(80, 24);
        _btnClearLog.Font = UITheme.SmallFont;
        _btnClearLog.Location = new Point(896, 4);
        _btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnClearLog.Click += (_, _) => _txtLog.Clear();

        _btnCopyLog.Text = "Kopiuj";
        _btnCopyLog.Size = new Size(70, 24);
        _btnCopyLog.Font = UITheme.SmallFont;
        _btnCopyLog.Location = new Point(982, 4);
        _btnCopyLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnCopyLog.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_txtLog.Text))
            {
                Clipboard.SetText(_txtLog.Text);
                Log("Skopiowano logi do schowka.");
            }
        };

        pnlStatusStrip.Controls.AddRange(new Control[]
        {
            _lblStatusStrip, _btnDonate, _btnToggleLog, _btnClearLog, _btnCopyLog
        });

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.Multiline = true;
        _txtLog.ReadOnly = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.BackColor = UITheme.BgDarkest;
        _txtLog.ForeColor = UITheme.TextSecondary;
        _txtLog.BorderStyle = BorderStyle.None;
        _txtLog.Font = UITheme.CodeFont;

        _pnlFooter.Controls.Add(_txtLog);
        _pnlFooter.Controls.Add(pnlStatusStrip);

        // Add to Form
        Controls.Add(_lvDevices);
        Controls.Add(_pnlFooter);
        Controls.Add(_pnlToolbar);
        Controls.Add(_pnlStats);
        Controls.Add(_pnlBanner);
        Controls.Add(_pnlPrivateNetwork);
        Controls.Add(_pnlHeader);

        ResumeLayout(false);
    }

    private void SetupContextMenu()
    {
        var itemAttach = new ToolStripMenuItem("🚀 Podłącz urządzenie (Attach)");
        itemAttach.Click += async (_, _) => await AttachSelectedAsync();

        var itemDetach = new ToolStripMenuItem("🔌 Odłącz urządzenie (Detach)");
        itemDetach.Click += async (_, _) => await DetachSelectedAsync();

        var itemAuto = new ToolStripMenuItem("⭐ Przełącz Auto-łącz");
        itemAuto.Click += (_, _) => ToggleAutoConnectSelected();

        var itemDownload = new ToolStripMenuItem("⬇️ Pobierz automatycznie sterownik usbip-win2");
        itemDownload.Click += async (_, _) => await InstallDriverAutomaticallyAsync();

        var itemNetwork = new ToolStripMenuItem("🛡️ Otwórz ustawienia sieci Windows (Wymóg: Prywatna)");
        itemNetwork.Click += (_, _) => DriverInstaller.OpenWindowsNetworkSettings();

        var itemDonate = new ToolStripMenuItem("☕ Wesprzyj projekt (buycoffee.to/pelek2g)");
        itemDonate.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://buycoffee.to/pelek2g") { UseShellExecute = true });
            }
            catch { }
        };

        var itemCopy = new ToolStripMenuItem("📋 Kopiuj dane urządzenia i serwera");
        itemCopy.Click += (_, _) =>
        {
            if (_lvDevices.SelectedItems.Count > 0 && _lvDevices.SelectedItems[0].Tag is DeviceRowTag tag)
            {
                Clipboard.SetText($"Serwer: {tag.Server.PrimaryIp} | BusID: {tag.Device.BusId} | VID:PID: {tag.Device.VidPid} | {tag.Device.Description}");
                Log($"Skopiowano: {tag.Device.Description}");
            }
        };

        var itemRefresh = new ToolStripMenuItem("🔄 Odśwież listę");
        itemRefresh.Click += async (_, _) => await RefreshLoopTickAsync();

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            itemAttach, itemDetach, itemAuto, new ToolStripSeparator(),
            itemDownload, itemNetwork, itemDonate, new ToolStripSeparator(), itemCopy, itemRefresh
        });

        _lvDevices.ContextMenuStrip = _contextMenu;
    }

    private void ToggleLogDrawer()
    {
        _isLogExpanded = !_isLogExpanded;
        if (_isLogExpanded)
        {
            _pnlFooter.Height = 175;
            _btnToggleLog.Text = "Logi (Zwiń ▲)";
        }
        else
        {
            _pnlFooter.Height = 32;
            _btnToggleLog.Text = "Logi (Rozwiń ▼)";
        }
    }

    private void SetupTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Pokaż okno", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        menu.Items.Add("Zamknij klienta", null, (_, _) =>
        {
            _chkMinToTray.Checked = false;
            Close();
        });

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "LanUsb Client";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private async Task ListenUdpBeaconsAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, LanUsbConstants.DiscoveryUdpPort));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var beacon = JsonSerializer.Deserialize<ServerBeacon>(json, NetworkHelper.JsonOptions);
                if (beacon == null || beacon.Magic != LanUsbConstants.ProtocolMagic)
                    continue;

                var remoteIp = result.RemoteEndPoint.Address.ToString();
                var key = string.IsNullOrWhiteSpace(beacon.ServerId) ? remoteIp : beacon.ServerId;

                _servers.AddOrUpdate(
                    key,
                    _ => new DiscoveredServer
                    {
                        Key = key,
                        HostName = beacon.HostName,
                        PrimaryIp = remoteIp,
                        AllowRemoteBind = beacon.AllowRemoteBind,
                        ControlPort = beacon.ControlPort,
                        Devices = beacon.Devices,
                        LastSeenUtc = DateTime.UtcNow
                    },
                    (_, existing) =>
                    {
                        existing.HostName = beacon.HostName;
                        existing.PrimaryIp = remoteIp;
                        existing.AllowRemoteBind = beacon.AllowRemoteBind;
                        existing.ControlPort = beacon.ControlPort;
                        existing.Devices = beacon.Devices;
                        existing.LastSeenUtc = DateTime.UtcNow;
                        return existing;
                    });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore malformed UDP packets
            }
        }
    }

    private async Task RefreshLoopTickAsync()
    {
        if (_isTickBusy) return;
        _isTickBusy = true;
        try
        {
            _driverInstalled = UsbipClientCli.FindUsbipPath() != null;
            _attachedPorts = _driverInstalled
                ? await UsbipClientCli.GetAttachedPortsAsync(_cts.Token)
                : new List<AttachedPortInfo>();

            // Query manual servers
            foreach (var ip in _settings.ManualServers.ToList())
            {
                var resp = await NetworkHelper.SendControlRequestAsync(
                    ip,
                    LanUsbConstants.ControlTcpPort,
                    new ControlRequest { Action = "list" },
                    2000,
                    _cts.Token);

                List<UsbDeviceInfo> devs;
                bool allowRemote = false;
                if (resp.Success && resp.Devices != null)
                {
                    devs = resp.Devices;
                    allowRemote = true;
                }
                else
                {
                    var (_, _, rawDevs) = await UsbipClientCli.ListRemoteAsync(ip, _cts.Token);
                    devs = rawDevs;
                }

                var key = "manual:" + ip;
                _servers.AddOrUpdate(
                    key,
                    _ => new DiscoveredServer
                    {
                        Key = key,
                        HostName = ip,
                        PrimaryIp = ip,
                        IsManual = true,
                        AllowRemoteBind = allowRemote,
                        Devices = devs,
                        LastSeenUtc = DateTime.UtcNow
                    },
                    (_, existing) =>
                    {
                        existing.Devices = devs;
                        existing.AllowRemoteBind = allowRemote;
                        existing.LastSeenUtc = DateTime.UtcNow;
                        return existing;
                    });
            }

            // Expire stale UDP servers (> 12 seconds)
            var cutoff = DateTime.UtcNow.AddSeconds(-12);
            foreach (var kv in _servers.ToArray())
            {
                if (!kv.Value.IsManual && kv.Value.LastSeenUtc < cutoff)
                    _servers.TryRemove(kv.Key, out _);
            }

            // Auto-connect check
            if (_driverInstalled)
            {
                await RunAutoConnectPassAsync();
            }

            UpdateUi();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log($"Błąd odświeżania: {ex.Message}");
        }
        finally
        {
            _isTickBusy = false;
        }
    }

    private async Task RunAutoConnectPassAsync()
    {
        foreach (var srv in _servers.Values)
        {
            foreach (var dev in srv.Devices)
            {
                var autoKey = ClientSettings.MakeAutoKey(srv.PrimaryIp, dev.VidPid);
                if (!_settings.AutoConnectKeys.Contains(autoKey))
                    continue;

                var alreadyAttached = _attachedPorts.Any(p =>
                    string.Equals(p.RemoteHost, srv.PrimaryIp, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.BusId, dev.BusId, StringComparison.OrdinalIgnoreCase));

                if (alreadyAttached)
                    continue;

                if (dev.State == UsbDeviceState.NotShared && srv.AllowRemoteBind)
                {
                    await NetworkHelper.SendControlRequestAsync(
                        srv.PrimaryIp,
                        srv.ControlPort,
                        new ControlRequest { Action = "bind", BusId = dev.BusId },
                        3000,
                        _cts.Token);
                }

                if (dev.State is UsbDeviceState.Shared or UsbDeviceState.NotShared)
                {
                    var res = await UsbipClientCli.AttachAsync(srv.PrimaryIp, dev.BusId, _cts.Token);
                    if (res.Success)
                    {
                        Log($"[Auto-łącz] Podłączono {dev.Description} ({dev.BusId}) z {srv.PrimaryIp}");
                        _attachedPorts = await UsbipClientCli.GetAttachedPortsAsync(_cts.Token);
                    }
                }
            }
        }
    }

    private void UpdateUi()
    {
        if (InvokeRequired)
        {
            BeginInvoke(UpdateUi);
            return;
        }

        if (_driverInstalled)
        {
            _pnlBanner.Visible = false;
            _lblDriverBadge.Text = "● STEROWNIK USBIP-WIN2 AKTYWNY";
            _lblDriverBadge.ForeColor = UITheme.Green;
            _lblDriverBadge.BackColor = UITheme.GreenBg;
        }
        else
        {
            _pnlBanner.Visible = true;
            _lblDriverBadge.Text = "● BRAK STEROWNIKA";
            _lblDriverBadge.ForeColor = UITheme.Red;
            _lblDriverBadge.BackColor = UITheme.RedBg;
        }

        var serverCount = _servers.Count;
        var totalAvailable = _servers.Values.Sum(s => s.Devices.Count);
        var attachedCount = _attachedPorts.Count;
        var autoRuleCount = _settings.AutoConnectKeys.Count;

        _cardServers.ValueText = $"{serverCount} serwerów";
        _cardAvailableDevices.ValueText = $"{totalAvailable} urządzeń";
        _cardAttachedDevices.ValueText = $"{attachedCount} podłączonych";
        _cardAutoRules.ValueText = $"{autoRuleCount} aktywnych";

        _lblDetectedSummary.Text = serverCount > 0
            ? $"Wykryto {serverCount} serwer(ów) LAN: {string.Join(", ", _servers.Values.Select(s => s.HostName))}"
            : "Brak wykrytych serwerów (Upewnij się, że sieć w Windows to \"Prywatna\")";

        _lblStatusStrip.Text = $"Status: Gotowy | Aktywne porty USB: {attachedCount} | Ostatnie sprawdzenie: {DateTime.Now:HH:mm:ss}";

        var selectedTag = _lvDevices.SelectedItems.Count > 0
            ? _lvDevices.SelectedItems[0].Tag as DeviceRowTag
            : null;

        var filter = _searchBox.SearchText.Trim();

        _lvDevices.BeginUpdate();
        _lvDevices.Items.Clear();

        foreach (var srv in _servers.Values.OrderBy(s => s.HostName))
        {
            foreach (var dev in srv.Devices)
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    var matches =
                        dev.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        dev.BusId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        dev.VidPid.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        srv.HostName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        srv.PrimaryIp.Contains(filter, StringComparison.OrdinalIgnoreCase);

                    if (!matches) continue;
                }

                var attached = _attachedPorts.FirstOrDefault(p =>
                    string.Equals(p.RemoteHost, srv.PrimaryIp, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.BusId, dev.BusId, StringComparison.OrdinalIgnoreCase));

                var autoKey = ClientSettings.MakeAutoKey(srv.PrimaryIp, dev.VidPid);
                var isAuto = _settings.AutoConnectKeys.Contains(autoKey);

                var serverLabel = srv.HostName == srv.PrimaryIp
                    ? $"🖥️ {srv.PrimaryIp}"
                    : $"🖥️ {srv.HostName} ({srv.PrimaryIp})";

                var (catName, icon, _) = DeviceCategoryHelper.GetCategory(dev.Description, dev.VidPid);

                var stateText = attached != null
                    ? $"🔵 PODŁĄCZONO (Port {attached.Port})"
                    : dev.State switch
                    {
                        UsbDeviceState.Shared => "🟢 Dostępne w LAN (Gotowe)",
                        UsbDeviceState.Attached => "⚠️ Zajęte przez innego klienta",
                        UsbDeviceState.NotShared => srv.AllowRemoteBind ? "⚪ Nieudostępnione (Zdalne Bind OK)" : "⚪ Nieudostępnione",
                        _ => dev.RawState
                    };

                var item = new ListViewItem(serverLabel)
                {
                    Tag = new DeviceRowTag { Server = srv, Device = dev, AttachedPort = attached }
                };

                item.SubItems.Add($"{icon} {catName}");
                item.SubItems.Add(dev.Description);
                item.SubItems.Add(attached != null ? $"{dev.BusId} (Port {attached.Port})" : dev.BusId);
                item.SubItems.Add(dev.VidPid);
                item.SubItems.Add(stateText);
                item.SubItems.Add(isAuto ? "⭐ TAK" : "—");

                item.ForeColor = attached != null
                    ? UITheme.Blue
                    : dev.State == UsbDeviceState.Shared
                        ? UITheme.Green
                        : UITheme.TextSecondary;

                _lvDevices.Items.Add(item);

                if (selectedTag != null &&
                    selectedTag.Server.PrimaryIp == srv.PrimaryIp &&
                    selectedTag.Device.BusId == dev.BusId)
                {
                    item.Selected = true;
                }
            }
        }

        _lvDevices.EndUpdate();
    }

    private async Task AttachSelectedAsync()
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not DeviceRowTag tag) return;

        if (!_driverInstalled)
        {
            var choice = MessageBox.Show(
                "Wymagany sterownik usbip-win2 nie jest zainstalowany.\n\nCzy chcesz go pobrać i zainstalować automatycznie teraz?",
                "Brak sterownika klienta",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (choice == DialogResult.Yes)
            {
                await InstallDriverAutomaticallyAsync();
            }
            return;
        }

        if (tag.Device.State == UsbDeviceState.NotShared)
        {
            if (!tag.Server.AllowRemoteBind)
            {
                Log($"Urządzenie {tag.Device.BusId} nie jest udostępnione na serwerze i serwer blokuje zdalne Bind.");
                return;
            }

            Log($"Wysyłanie zdalnego BIND do serwera {tag.Server.PrimaryIp} dla {tag.Device.BusId}...");
            var bindResp = await NetworkHelper.SendControlRequestAsync(
                tag.Server.PrimaryIp,
                tag.Server.ControlPort,
                new ControlRequest { Action = "bind", BusId = tag.Device.BusId },
                5000,
                _cts.Token);

            if (!bindResp.Success)
            {
                Log($"Zdalne BIND nie powiodło się: {bindResp.Message}");
                return;
            }
        }

        Log($"Podłączanie urządzenia: {tag.Device.Description} ({tag.Server.PrimaryIp} / {tag.Device.BusId})...");
        var res = await UsbipClientCli.AttachAsync(tag.Server.PrimaryIp, tag.Device.BusId, _cts.Token);
        Log(res.Success
            ? $"OK: Urządzenie {tag.Device.Description} zostało pomyślnie zamontowane w systemie Windows!"
            : $"Błąd attach: {res.CombinedOutput}");

        await RefreshLoopTickAsync();
    }

    private async Task DetachSelectedAsync()
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not DeviceRowTag tag) return;

        if (tag.AttachedPort == null)
        {
            Log("Wybrane urządzenie nie jest obecnie podłączone na tym kliencie.");
            return;
        }

        Log($"Odłączanie portu {tag.AttachedPort.Port} ({tag.Device.Description})...");
        var res = await UsbipClientCli.DetachAsync(tag.AttachedPort.Port, _cts.Token);
        Log(res.Success
            ? $"OK: Odłączono wirtualny port {tag.AttachedPort.Port}."
            : $"Błąd detach: {res.CombinedOutput}");

        await RefreshLoopTickAsync();
    }

    private void ToggleAutoConnectSelected()
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not DeviceRowTag tag) return;

        var key = ClientSettings.MakeAutoKey(tag.Server.PrimaryIp, tag.Device.VidPid);
        if (_settings.AutoConnectKeys.Contains(key))
        {
            _settings.AutoConnectKeys.Remove(key);
            Log($"Wyłączono Auto-łącz dla {tag.Device.Description} ({tag.Device.VidPid}).");
        }
        else
        {
            _settings.AutoConnectKeys.Add(key);
            Log($"Włączono Auto-łącz dla {tag.Device.Description} ({tag.Device.VidPid}).");
        }

        _settings.Save();
        UpdateUi();
    }

    private async Task AddManualServerAsync()
    {
        var ip = _txtManualIp.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip)) return;

        if (!_settings.ManualServers.Contains(ip, StringComparer.OrdinalIgnoreCase))
        {
            _settings.ManualServers.Add(ip);
            _settings.Save();
            Log($"Dodano ręczny serwer: {ip}");
        }

        _txtManualIp.Clear();
        await RefreshLoopTickAsync();
    }

    private void RemoveSelectedManualServer()
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not DeviceRowTag tag) return;

        if (_settings.ManualServers.RemoveAll(s => s.Equals(tag.Server.PrimaryIp, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            _settings.Save();
            _servers.TryRemove("manual:" + tag.Server.PrimaryIp, out _);
            Log($"Usunięto ręczny serwer: {tag.Server.PrimaryIp}");
            UpdateUi();
        }
    }

    private async Task InstallDriverAutomaticallyAsync()
    {
        if (_isInstallingDriver) return;
        _isInstallingDriver = true;
        _btnDownloadDriver.Enabled = false;

        Log("Rozpoczynanie automatycznego pobierania i instalacji sterownika usbip-win2...");
        var (ok, msg) = await DriverInstaller.InstallClientDriverAsync(Log, _cts.Token);

        if (ok)
        {
            Log($"Sukces: {msg}");
            MessageBox.Show(
                msg + "\n\nSterownik klienta jest gotowy do montowania wirtualnych portów USB.",
                "Sterownik zainstalowany",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            Log($"Błąd: {msg}");
            MessageBox.Show(
                msg + "\n\nMożesz także uruchomić plik 'Pobierz-Sterownik-Klienta.bat' z folderu aplikacji.",
                "Błąd instalacji sterownika",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _btnDownloadDriver.Enabled = true;
        _isInstallingDriver = false;
        await RefreshLoopTickAsync();
    }

    private async Task SwitchNetworkProfileToPrivateAsync()
    {
        Log("Ustawianie profili połączeń sieciowych na 'Prywatna' (Private)...");
        var res = await DriverInstaller.SetNetworkProfileToPrivateAsync(_cts.Token);
        if (res.Success)
        {
            Log("Pomyślnie zmieniono profile sieciowe Windows na Prywatne!");
            MessageBox.Show(
                "Profile sieciowe Windows zostały pomyślnie przestawione na 'Prywatna' (Private).\nZapora zezwala teraz na ruch LAN między komputerami.",
                "Sieć prywatna włączona",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            Log($"Nie udało się automatycznie zmienić profilu: {res.CombinedOutput}");
            Log("Otwieram ustawienia sieci Windows, aby zmienić profil ręcznie...");
            DriverInstaller.OpenWindowsNetworkSettings();
        }
    }

    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message));
            return;
        }

        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
