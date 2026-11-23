using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LanUsb.Shared;

namespace LanUsb.Server;

public sealed class ServerForm : Form
{
    private readonly string _serverId = Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..6];
    private readonly CancellationTokenSource _cts = new();

    // UI Header & Status Banner
    private readonly Panel _pnlHeader = new();
    private readonly Label _lblBrand = new();
    private readonly Label _lblServerStateBadge = new();
    private readonly Label _lblNetworkInfo = new();

    // Private Network Alert Panel (Musi być sieć prywatna w Windows)
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
    private readonly StatCard _cardStatus = new();
    private readonly StatCard _cardTotalDevices = new();
    private readonly StatCard _cardSharedDevices = new();
    private readonly StatCard _cardAttachedDevices = new();

    // Actions & Search Toolbar
    private readonly Panel _pnlToolbar = new();
    private readonly SearchBoxControl _searchBox = new();
    private readonly ModernButton _btnBind = new();
    private readonly ModernButton _btnBindForce = new();
    private readonly ModernButton _btnUnbind = new();
    private readonly ModernButton _btnDownloadDriver = new();
    private readonly ModernButton _btnRefresh = new();
    private readonly ModernButton _btnToggleNetworkInfo = new();
    private readonly CheckBox _chkRemoteBind = new();
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
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    private List<UsbDeviceInfo> _devices = new();
    private bool _usbipdInstalled;
    private bool _usbipdRunning;
    private bool _isRefreshing;
    private bool _isLogExpanded = true;
    private bool _isInstallingDriver;

    public ServerForm()
    {
        Text = "LanUsb Server — Udostępnianie USB przez LAN (Free / Open Source)";
        Size = new Size(1100, 750);
        MinimumSize = new Size(920, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.BgDarkest;
        ForeColor = UITheme.TextPrimary;
        Font = UITheme.RegularFont;

        BuildUi();
        SetupContextMenu();
        SetupTray();

        _refreshTimer.Interval = 2500;
        _refreshTimer.Tick += async (_, _) => await RefreshAndBroadcastAsync();

        Load += async (_, _) =>
        {
            Log("Uruchamianie LanUsb Server...");
            _ = Task.Run(() => RunControlServerAsync(_cts.Token));
            await RefreshAndBroadcastAsync();
            _refreshTimer.Start();
        };

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && _chkMinToTray.Checked)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(1500, "LanUsb Server", "Serwer działa w tle w zasobniku.", ToolTipIcon.Info);
                return;
            }

            _refreshTimer.Stop();
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
        _pnlHeader.Height = 72;
        _pnlHeader.BackColor = UITheme.BgHeader;
        _pnlHeader.Padding = new Padding(18, 12, 18, 12);

        _lblBrand.Text = "⚡ LanUsb Server";
        _lblBrand.Font = UITheme.HeaderFont;
        _lblBrand.ForeColor = UITheme.TextPrimary;
        _lblBrand.AutoSize = true;
        _lblBrand.Location = new Point(18, 14);

        var lblSub = new Label
        {
            Text = "Darmowe udostępnianie USB przez LAN do streamowania (Brak limitów / Bez licencji)",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextMuted,
            AutoSize = true,
            Location = new Point(20, 42)
        };

        _lblServerStateBadge.Text = "● NASŁUCHIWANIE...";
        _lblServerStateBadge.Font = UITheme.SmallBoldFont;
        _lblServerStateBadge.ForeColor = UITheme.Green;
        _lblServerStateBadge.BackColor = UITheme.GreenBg;
        _lblServerStateBadge.Padding = new Padding(8, 4, 8, 4);
        _lblServerStateBadge.AutoSize = true;
        _lblServerStateBadge.Location = new Point(220, 16);

        _lblNetworkInfo.Text = "LAN IP: Wykrywanie... | Porty: USB/IP 3240, Discovery UDP 32400, Control TCP 32401";
        _lblNetworkInfo.Font = UITheme.SmallFont;
        _lblNetworkInfo.ForeColor = UITheme.TextSecondary;
        _lblNetworkInfo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblNetworkInfo.AutoSize = true;
        _lblNetworkInfo.Location = new Point(500, 26);

        _pnlHeader.Controls.AddRange(new Control[] { _lblBrand, lblSub, _lblServerStateBadge, _lblNetworkInfo });

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
        _btnOpenNetworkSettings.Location = new Point(780, 8);
        _btnOpenNetworkSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnOpenNetworkSettings.NormalColor = UITheme.Blue;
        _btnOpenNetworkSettings.HoverColor = UITheme.BlueDark;
        _btnOpenNetworkSettings.Font = UITheme.SmallBoldFont;
        _btnOpenNetworkSettings.Click += (_, _) => DriverInstaller.OpenWindowsNetworkSettings();

        _btnAutoPrivate.Text = "⚡ Zmień na Prywatną";
        _btnAutoPrivate.Size = new Size(140, 30);
        _btnAutoPrivate.Location = new Point(922, 8);
        _btnAutoPrivate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnAutoPrivate.NormalColor = UITheme.Amber;
        _btnAutoPrivate.HoverColor = UITheme.AmberDark;
        _btnAutoPrivate.Font = UITheme.SmallBoldFont;
        _btnAutoPrivate.Click += async (_, _) => await SwitchNetworkProfileToPrivateAsync();

        _btnCloseNetworkAlert.Text = "✕";
        _btnCloseNetworkAlert.Size = new Size(22, 22);
        _btnCloseNetworkAlert.Location = new Point(1068, 12);
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
        _pnlBanner.BackColor = Color.FromArgb(70, 30, 20);
        _pnlBanner.Padding = new Padding(18, 6, 18, 6);
        _pnlBanner.Visible = false;

        _lblBannerText.Text = "⚠️ Wymagany sterownik serwera usbipd-win nie jest zainstalowany.";
        _lblBannerText.Font = UITheme.RegularFont;
        _lblBannerText.ForeColor = Color.FromArgb(254, 215, 170);
        _lblBannerText.AutoSize = true;
        _lblBannerText.Location = new Point(18, 12);

        _btnBannerAction.Text = "⬇️ Pobierz automatycznie";
        _btnBannerAction.Size = new Size(190, 30);
        _btnBannerAction.Location = new Point(880, 7);
        _btnBannerAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnBannerAction.NormalColor = UITheme.Blue;
        _btnBannerAction.HoverColor = UITheme.BlueDark;

        _pnlBanner.Controls.AddRange(new Control[] { _lblBannerText, _btnBannerAction });

        // 4. STATS PANEL
        _pnlStats.Dock = DockStyle.Top;
        _pnlStats.Height = 84;
        _pnlStats.BackColor = UITheme.BgDarkest;
        _pnlStats.Padding = new Padding(16, 8, 16, 8);
        _pnlStats.WrapContents = false;

        _cardStatus.IconText = "🖥️";
        _cardStatus.Title = "STAN SERWERA";
        _cardStatus.ValueText = "SPRAWDZANIE...";
        _cardStatus.SubText = "UDP 32400";
        _cardStatus.AccentColor = UITheme.Green;
        _cardStatus.Width = 240;

        _cardTotalDevices.IconText = "🔌";
        _cardTotalDevices.Title = "PORTY USB";
        _cardTotalDevices.ValueText = "0 znalezionych";
        _cardTotalDevices.SubText = "Urządzenia lokalne";
        _cardTotalDevices.AccentColor = UITheme.Blue;
        _cardTotalDevices.Width = 230;

        _cardSharedDevices.IconText = "🟢";
        _cardSharedDevices.Title = "UDOSTĘPNIONE";
        _cardSharedDevices.ValueText = "0 urządzeń";
        _cardSharedDevices.SubText = "Gotowe w LAN";
        _cardSharedDevices.AccentColor = UITheme.Green;
        _cardSharedDevices.Width = 230;

        _cardAttachedDevices.IconText = "🚀";
        _cardAttachedDevices.Title = "W UŻYCIU";
        _cardAttachedDevices.ValueText = "0 podłączonych";
        _cardAttachedDevices.SubText = "Przechwycone przez klienta";
        _cardAttachedDevices.AccentColor = UITheme.Indigo;
        _cardAttachedDevices.Width = 240;

        _pnlStats.Controls.AddRange(new Control[]
        {
            _cardStatus, _cardTotalDevices, _cardSharedDevices, _cardAttachedDevices
        });

        // 5. TOOLBAR
        _pnlToolbar.Dock = DockStyle.Top;
        _pnlToolbar.Height = 52;
        _pnlToolbar.BackColor = UITheme.BgSurface;
        _pnlToolbar.Padding = new Padding(16, 10, 16, 10);

        _searchBox.Location = new Point(16, 10);
        _searchBox.Width = 220;
        _searchBox.SearchTextChanged += (_, _) => ApplyFilter();

        _btnBind.Text = "🟢 Udostępnij (Bind)";
        _btnBind.Size = new Size(150, 32);
        _btnBind.Location = new Point(246, 10);
        _btnBind.NormalColor = UITheme.Green;
        _btnBind.HoverColor = UITheme.GreenDark;
        _btnBind.Click += async (_, _) => await BindSelectedAsync(force: false);

        _btnBindForce.Text = "🟠 Wymuś";
        _btnBindForce.Size = new Size(95, 32);
        _btnBindForce.Location = new Point(402, 10);
        _btnBindForce.NormalColor = UITheme.Amber;
        _btnBindForce.HoverColor = UITheme.AmberDark;
        _btnBindForce.Click += async (_, _) => await BindSelectedAsync(force: true);

        _btnUnbind.Text = "🔴 Zatrzymaj (Unbind)";
        _btnUnbind.Size = new Size(150, 32);
        _btnUnbind.Location = new Point(503, 10);
        _btnUnbind.NormalColor = UITheme.Red;
        _btnUnbind.HoverColor = UITheme.RedDark;
        _btnUnbind.Click += async (_, _) => await UnbindSelectedAsync();

        _btnDownloadDriver.Text = "⬇️ Pobierz sterownik";
        _btnDownloadDriver.Size = new Size(150, 32);
        _btnDownloadDriver.Location = new Point(659, 10);
        _btnDownloadDriver.NormalColor = UITheme.Blue;
        _btnDownloadDriver.HoverColor = UITheme.BlueDark;
        _btnDownloadDriver.Click += async (_, _) => await InstallDriverAutomaticallyAsync();

        _btnRefresh.Text = "🔄";
        _btnRefresh.Size = new Size(42, 32);
        _btnRefresh.Location = new Point(815, 10);
        _btnRefresh.NormalColor = UITheme.SlateButton;
        _btnRefresh.HoverColor = UITheme.SlateButtonHover;
        _btnRefresh.Click += async (_, _) => await RefreshAndBroadcastAsync();

        _btnToggleNetworkInfo.Text = "🛡️ Sieć prywatna";
        _btnToggleNetworkInfo.Size = new Size(115, 32);
        _btnToggleNetworkInfo.Location = new Point(863, 10);
        _btnToggleNetworkInfo.NormalColor = Color.FromArgb(40, 54, 80);
        _btnToggleNetworkInfo.HoverColor = Color.FromArgb(50, 68, 100);
        _btnToggleNetworkInfo.Font = UITheme.SmallFont;
        _btnToggleNetworkInfo.Click += (_, _) => _pnlPrivateNetwork.Visible = !_pnlPrivateNetwork.Visible;

        _chkRemoteBind.Text = "Zezwalaj na zdalne Bind";
        _chkRemoteBind.Font = UITheme.SmallFont;
        _chkRemoteBind.ForeColor = UITheme.TextSecondary;
        _chkRemoteBind.Checked = true;
        _chkRemoteBind.AutoSize = true;
        _chkRemoteBind.Location = new Point(988, 8);
        _chkRemoteBind.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _chkMinToTray.Text = "Do Tray";
        _chkMinToTray.Font = UITheme.SmallFont;
        _chkMinToTray.ForeColor = UITheme.TextSecondary;
        _chkMinToTray.Checked = true;
        _chkMinToTray.AutoSize = true;
        _chkMinToTray.Location = new Point(988, 28);
        _chkMinToTray.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _pnlToolbar.Controls.AddRange(new Control[]
        {
            _searchBox, _btnBind, _btnBindForce, _btnUnbind, _btnDownloadDriver,
            _btnRefresh, _btnToggleNetworkInfo, _chkRemoteBind, _chkMinToTray
        });

        // 6. DEVICE LISTVIEW
        _lvDevices.Dock = DockStyle.Fill;
        _lvDevices.Columns.Add("Kategoria / Typ", 170);
        _lvDevices.Columns.Add("Nazwa urządzenia USB", 370);
        _lvDevices.Columns.Add("BusID", 80);
        _lvDevices.Columns.Add("VID:PID", 100);
        _lvDevices.Columns.Add("Stan w sieci LAN", 230);
        _lvDevices.DoubleClick += async (_, _) =>
        {
            if (_lvDevices.SelectedItems.Count == 0) return;
            var dev = _lvDevices.SelectedItems[0].Tag as UsbDeviceInfo;
            if (dev == null) return;
            if (dev.State == UsbDeviceState.NotShared)
                await BindSelectedAsync(false);
            else if (dev.State == UsbDeviceState.Shared)
                await UnbindSelectedAsync();
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

        _lblStatusStrip.Text = "Status: Gotowy | Port TCP 3240 otwarty | Podwójne kliknięcie przełącza udostępnianie";
        _lblStatusStrip.Font = UITheme.SmallFont;
        _lblStatusStrip.ForeColor = UITheme.TextSecondary;
        _lblStatusStrip.AutoSize = true;
        _lblStatusStrip.Location = new Point(14, 8);

        _btnDonate.Text = "☕ Wesprzyj";
        _btnDonate.Size = new Size(95, 24);
        _btnDonate.Font = UITheme.SmallBoldFont;
        _btnDonate.NormalColor = UITheme.Amber;
        _btnDonate.HoverColor = UITheme.AmberDark;
        _btnDonate.Location = new Point(655, 4);
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
        _btnToggleLog.Location = new Point(760, 4);
        _btnToggleLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnToggleLog.Click += (_, _) => ToggleLogDrawer();

        _btnClearLog.Text = "Wyczyść";
        _btnClearLog.Size = new Size(80, 24);
        _btnClearLog.Font = UITheme.SmallFont;
        _btnClearLog.Location = new Point(876, 4);
        _btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnClearLog.Click += (_, _) => _txtLog.Clear();

        _btnCopyLog.Text = "Kopiuj";
        _btnCopyLog.Size = new Size(70, 24);
        _btnCopyLog.Font = UITheme.SmallFont;
        _btnCopyLog.Location = new Point(962, 4);
        _btnCopyLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnCopyLog.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_txtLog.Text))
            {
                Clipboard.SetText(_txtLog.Text);
                Log("Skopiowano zawartość logów do schowka.");
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
        var itemBind = new ToolStripMenuItem("🟢 Udostępnij (Bind)");
        itemBind.Click += async (_, _) => await BindSelectedAsync(false);

        var itemBindForce = new ToolStripMenuItem("🟠 Wymuś udostępnienie (Force Bind)");
        itemBindForce.Click += async (_, _) => await BindSelectedAsync(true);

        var itemUnbind = new ToolStripMenuItem("🔴 Zatrzymaj udostępnianie (Unbind)");
        itemUnbind.Click += async (_, _) => await UnbindSelectedAsync();

        var itemDownload = new ToolStripMenuItem("⬇️ Pobierz automatycznie sterownik usbipd-win");
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

        var itemCopy = new ToolStripMenuItem("📋 Kopiuj ID i opis urządzenia");
        itemCopy.Click += (_, _) =>
        {
            if (_lvDevices.SelectedItems.Count > 0 && _lvDevices.SelectedItems[0].Tag is UsbDeviceInfo dev)
            {
                Clipboard.SetText($"BusID: {dev.BusId} | VID:PID: {dev.VidPid} | {dev.Description}");
                Log($"Skopiowano: {dev.Description} ({dev.BusId})");
            }
        };

        var itemRefresh = new ToolStripMenuItem("🔄 Odśwież listę");
        itemRefresh.Click += async (_, _) => await RefreshAndBroadcastAsync();

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            itemBind, itemBindForce, itemUnbind, new ToolStripSeparator(),
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
        menu.Items.Add("Zamknij serwer", null, (_, _) =>
        {
            _chkMinToTray.Checked = false;
            Close();
        });

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "LanUsb Server";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private async Task RefreshAndBroadcastAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var ips = NetworkHelper.GetLocalLanIPv4Addresses();
            var usbipdPath = UsbipdCli.FindUsbipdPath();
            _usbipdInstalled = usbipdPath != null;
            _usbipdRunning = _usbipdInstalled && await UsbipdCli.IsServiceRunningAsync(_cts.Token);

            if (_usbipdInstalled)
            {
                var (ok, error, devs) = await UsbipdCli.GetDevicesAsync(_cts.Token);
                if (ok)
                    _devices = devs;
                else if (!string.IsNullOrWhiteSpace(error))
                    Log($"Błąd usbipd list: {error}");
            }
            else
            {
                _devices = new List<UsbDeviceInfo>();
            }

            UpdateUiStatus(ips);

            var beacon = new ServerBeacon
            {
                ServerId = _serverId,
                HostName = Environment.MachineName,
                IpAddresses = ips,
                UsbipPort = LanUsbConstants.UsbipTcpPort,
                ControlPort = LanUsbConstants.ControlTcpPort,
                AllowRemoteBind = _chkRemoteBind.Checked,
                UsbipdInstalled = _usbipdInstalled,
                UsbipdRunning = _usbipdRunning,
                Devices = _devices,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(beacon, NetworkHelper.JsonOptions));
            await NetworkHelper.BroadcastUdpAsync(bytes, LanUsbConstants.DiscoveryUdpPort, _cts.Token);
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
            _isRefreshing = false;
        }
    }

    private void UpdateUiStatus(List<string> ips)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateUiStatus(ips));
            return;
        }

        var ipDisplay = ips.Count > 0 ? string.Join(", ", ips) : "Brak interfejsu LAN";
        _lblNetworkInfo.Text = $"Host: {Environment.MachineName} | IP: {ipDisplay} | Porty: 3240, 32400, 32401";

        if (!_usbipdInstalled)
        {
            _pnlBanner.Visible = true;
            _pnlBanner.BackColor = Color.FromArgb(85, 30, 25);
            _lblBannerText.Text = "⚠️ Brak usbipd-win — wymagany do udostępniania urządzeń USB.";
            _btnBannerAction.Text = "⬇️ Pobierz automatycznie";
            _btnBannerAction.NormalColor = UITheme.Blue;
            _btnBannerAction.Click -= OnStartServiceClick;
            _btnBannerAction.Click -= OnInstallUsbipdClick;
            _btnBannerAction.Click += OnInstallUsbipdClick;

            _lblServerStateBadge.Text = "● BRAK STEROWNIKA";
            _lblServerStateBadge.ForeColor = UITheme.Red;
            _lblServerStateBadge.BackColor = UITheme.RedBg;

            _cardStatus.ValueText = "BRAK STEROWNIKA";
            _cardStatus.AccentColor = UITheme.Red;
            _cardStatus.SubText = "Kliknij Pobierz";
        }
        else if (!_usbipdRunning)
        {
            _pnlBanner.Visible = true;
            _pnlBanner.BackColor = Color.FromArgb(75, 45, 15);
            _lblBannerText.Text = "⚠️ Usługa 'usbipd' nie jest uruchomiona.";
            _btnBannerAction.Text = "Uruchom usługę usbipd";
            _btnBannerAction.NormalColor = UITheme.Green;
            _btnBannerAction.Click -= OnInstallUsbipdClick;
            _btnBannerAction.Click -= OnStartServiceClick;
            _btnBannerAction.Click += OnStartServiceClick;

            _lblServerStateBadge.Text = "● USŁUGA ZATRZYMANA";
            _lblServerStateBadge.ForeColor = UITheme.Amber;
            _lblServerStateBadge.BackColor = UITheme.AmberBg;

            _cardStatus.ValueText = "ZATRZYMANO";
            _cardStatus.AccentColor = UITheme.Amber;
            _cardStatus.SubText = "Wymaga startu";
        }
        else
        {
            _pnlBanner.Visible = false;

            _lblServerStateBadge.Text = "● AKTYWNY (BROADCASTING)";
            _lblServerStateBadge.ForeColor = UITheme.Green;
            _lblServerStateBadge.BackColor = UITheme.GreenBg;

            _cardStatus.ValueText = "ONLINE";
            _cardStatus.AccentColor = UITheme.Green;
            _cardStatus.SubText = "UDP 32400 / TCP 32401";
        }

        var sharedCount = _devices.Count(d => d.State == UsbDeviceState.Shared);
        var attachedCount = _devices.Count(d => d.State == UsbDeviceState.Attached);

        _cardTotalDevices.ValueText = $"{_devices.Count} urządzeń";
        _cardSharedDevices.ValueText = $"{sharedCount} w sieci LAN";
        _cardAttachedDevices.ValueText = $"{attachedCount} podłączonych";

        _lblStatusStrip.Text = $"Status: Gotowy | {_devices.Count} USB w systemie | {sharedCount} udostępnionych | Ostatnie odświeżenie: {DateTime.Now:HH:mm:ss}";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = _searchBox.SearchText.Trim();
        var selectedBusId = _lvDevices.SelectedItems.Count > 0
            ? (_lvDevices.SelectedItems[0].Tag as UsbDeviceInfo)?.BusId
            : null;

        _lvDevices.BeginUpdate();
        _lvDevices.Items.Clear();

        var query = _devices.AsEnumerable();
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(d =>
                d.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                d.BusId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                d.VidPid.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var d in query)
        {
            var (catName, icon, _) = DeviceCategoryHelper.GetCategory(d.Description, d.VidPid);

            var item = new ListViewItem($"{icon} {catName}") { Tag = d };
            item.SubItems.Add(d.Description);
            item.SubItems.Add(d.BusId);
            item.SubItems.Add(d.VidPid);
            item.SubItems.Add(FormatState(d));

            item.ForeColor = d.State switch
            {
                UsbDeviceState.Shared => UITheme.Green,
                UsbDeviceState.Attached => UITheme.Blue,
                _ => UITheme.TextSecondary
            };

            _lvDevices.Items.Add(item);
            if (d.BusId == selectedBusId)
                item.Selected = true;
        }

        _lvDevices.EndUpdate();
    }

    private static string FormatState(UsbDeviceInfo d) => d.State switch
    {
        UsbDeviceState.NotShared => "⚪ Nieudostępnione (Lokalne)",
        UsbDeviceState.Shared => "🟢 Udostępnione w LAN (Gotowe)",
        UsbDeviceState.Attached => $"🔵 W użyciu przez klienta ({d.RawState})",
        _ => d.RawState
    };

    private async Task BindSelectedAsync(bool force)
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not UsbDeviceInfo dev) return;

        Log($"Udostępnianie {dev.BusId} ({dev.Description})...");
        var res = await UsbipdCli.BindAsync(dev.BusId, force, _cts.Token);
        Log(res.Success
            ? $"OK: {dev.BusId} udostępnione."
            : $"Błąd bind {dev.BusId}: {res.CombinedOutput}");
        await RefreshAndBroadcastAsync();
    }

    private async Task UnbindSelectedAsync()
    {
        if (_lvDevices.SelectedItems.Count == 0) return;
        if (_lvDevices.SelectedItems[0].Tag is not UsbDeviceInfo dev) return;

        Log($"Zatrzymywanie udostępniania {dev.BusId}...");
        var res = await UsbipdCli.UnbindAsync(dev.BusId, _cts.Token);
        Log(res.Success
            ? $"OK: {dev.BusId} zatrzymane."
            : $"Błąd unbind {dev.BusId}: {res.CombinedOutput}");
        await RefreshAndBroadcastAsync();
    }

    private async Task InstallDriverAutomaticallyAsync()
    {
        if (_isInstallingDriver) return;
        _isInstallingDriver = true;
        _btnDownloadDriver.Enabled = false;

        Log("Rozpoczynanie automatycznego pobierania i instalacji sterownika usbipd-win...");
        var (ok, msg) = await DriverInstaller.InstallServerDriverAsync(Log, _cts.Token);

        if (ok)
        {
            Log($"Sukces: {msg}");
            MessageBox.Show(
                msg + "\n\nJeśli instalator zakończył pracę, sterownik jest gotowy do udostępniania USB.",
                "Sterownik zainstalowany",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            Log($"Błąd: {msg}");
            MessageBox.Show(
                msg + "\n\nMożesz także uruchomić plik 'Zainstaluj-Sterowniki.bat' z folderu aplikacji.",
                "Błąd instalacji sterownika",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _btnDownloadDriver.Enabled = true;
        _isInstallingDriver = false;
        await RefreshAndBroadcastAsync();
    }

    private async void OnInstallUsbipdClick(object? sender, EventArgs e)
    {
        await InstallDriverAutomaticallyAsync();
    }

    private async void OnStartServiceClick(object? sender, EventArgs e)
    {
        Log("Uruchamianie usługi usbipd...");
        var res = await UsbipdCli.StartServiceAsync(_cts.Token);
        Log(res.Success ? "Usługa usbipd uruchomiona." : $"Błąd uruchomienia usługi: {res.CombinedOutput}");
        await RefreshAndBroadcastAsync();
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

    private async Task RunControlServerAsync(CancellationToken ct)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, LanUsbConstants.ControlTcpPort);
            listener.Start();
            Log($"Serwer sterowania TCP nasłuchuje na porcie {LanUsbConstants.ControlTcpPort}.");

            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleControlClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log($"Błąd serwera TCP {LanUsbConstants.ControlTcpPort}: {ex.Message}");
        }
        finally
        {
            listener?.Stop();
        }
    }

    private async Task HandleControlClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) return;

                var req = JsonSerializer.Deserialize<ControlRequest>(line, NetworkHelper.JsonOptions);
                ControlResponse resp;

                if (req == null)
                {
                    resp = new ControlResponse { Success = false, Message = "Nieprawidłowe żądanie." };
                }
                else if (req.Action.Equals("list", StringComparison.OrdinalIgnoreCase))
                {
                    var (ok, err, devs) = await UsbipdCli.GetDevicesAsync(ct);
                    resp = new ControlResponse { Success = ok, Message = err ?? "OK", Devices = devs };
                }
                else if (req.Action.Equals("bind", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_chkRemoteBind.Checked)
                    {
                        resp = new ControlResponse { Success = false, Message = "Zdalne Bind jest wyłączone na serwerze." };
                    }
                    else if (string.IsNullOrWhiteSpace(req.BusId))
                    {
                        resp = new ControlResponse { Success = false, Message = "Brak BusId." };
                    }
                    else
                    {
                        Log($"Zdalne żądanie BIND dla {req.BusId} od {client.Client.RemoteEndPoint}");
                        var res = await UsbipdCli.BindAsync(req.BusId, req.Force, ct);
                        resp = new ControlResponse
                        {
                            Success = res.Success,
                            Message = res.Success ? "Udostępniono" : res.CombinedOutput
                        };
                        _ = RefreshAndBroadcastAsync();
                    }
                }
                else if (req.Action.Equals("unbind", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_chkRemoteBind.Checked)
                    {
                        resp = new ControlResponse { Success = false, Message = "Zdalne Unbind jest wyłączone na serwerze." };
                    }
                    else if (string.IsNullOrWhiteSpace(req.BusId))
                    {
                        resp = new ControlResponse { Success = false, Message = "Brak BusId." };
                    }
                    else
                    {
                        Log($"Zdalne żądanie UNBIND dla {req.BusId} od {client.Client.RemoteEndPoint}");
                        var res = await UsbipdCli.UnbindAsync(req.BusId, ct);
                        resp = new ControlResponse
                        {
                            Success = res.Success,
                            Message = res.Success ? "Zatrzymano" : res.CombinedOutput
                        };
                        _ = RefreshAndBroadcastAsync();
                    }
                }
                else
                {
                    resp = new ControlResponse { Success = false, Message = $"Nieznana akcja: {req.Action}" };
                }

                var json = JsonSerializer.Serialize(resp, NetworkHelper.JsonOptions) + "\n";
                var bytes = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
            }
            catch
            {
                // Ignore transient client disconnect
            }
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

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _txtLog.AppendText(line);
    }
}
