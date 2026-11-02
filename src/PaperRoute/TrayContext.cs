using PaperRoute.Config;
using PaperRoute.Delivery;
using PaperRoute.Devices;
using PaperRoute.Devices.Conversion;
using PaperRoute.Devices.MassStorage;
using PaperRoute.Devices.Mtp;
using PaperRoute.Logging;
using PaperRoute.Smtp;

namespace PaperRoute.App;

/// <summary>The tray icon, its menu and notifications; owns the SMTP host.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;

    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(5);

    private readonly AppPaths _paths;
    private readonly DpapiProtector _protector = new();
    private readonly FileActivityLog _log;
    private readonly CompositeLocator _locator;
    private readonly Icon _readyIcon = AppInfo.LoadIcon("PaperRoute");
    private readonly Icon _errorIcon = AppInfo.LoadIcon("PaperRouteError");
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _status = new() { Enabled = false };
    private readonly ToolStripMenuItem _kindle = new() { Enabled = false };
    private readonly ToolStripMenuItem _calibre = new() { Enabled = false };
    private readonly ToolStripMenuItem _autostart = new("Start with Windows");
    private readonly SynchronizationContext _ui;
    private SmtpHost? _host;
    private SettingsForm? _settings;

    public TrayContext(AppPaths paths)
    {
        _paths = paths;
        _log = new FileActivityLog(paths.LogDirectory);
        _locator = new CompositeLocator(
            problem => _log.Write($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} DEVICE {problem}"),
            new MassStorageLocator(),
            new MtpLocator());
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(_kindle);
        menu.Items.Add(_calibre);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", image: null, (_, _) => ShowSettings());
        menu.Items.Add("Open log folder", image: null, (_, _) => OpenFolder(paths.LogDirectory));
        menu.Items.Add("Open settings file", image: null, (_, _) => OpenFile(paths.ConfigFile));
        menu.Items.Add(_autostart);
        menu.Items.Add("About", image: null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", image: null, (_, _) => _ = ExitAsync());
        menu.Opening += (_, _) => RefreshMenu();
        _autostart.Click += (_, _) => ToggleAutostart();

        _tray = new NotifyIcon { Icon = _readyIcon, ContextMenuStrip = menu, Visible = true, Text = AppInfo.Name };
        _tray.DoubleClick += (_, _) => ShowSettings();
        SetStatus("starting...", ok: true);
        _ = StartSafelyAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _readyIcon.Dispose();
            _errorIcon.Dispose();
            _settings?.Dispose();
            _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _host = null;
        }

        base.Dispose(disposing);
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Shell(path);
    }

    private static void OpenFile(string path)
    {
        if (File.Exists(path))
        {
            Shell(path);
        }
    }

    private static void Shell(string target) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();

    private static void ShowAbout() => MessageBox.Show(
        $"{AppInfo.Name} {AppInfo.Version}{Environment.NewLine}{Environment.NewLine}"
        + $"Local \"Send to eReader\" for Calibre-Web / CWA: delivers books to a USB-connected Kindle.{Environment.NewLine}"
        + $"Unofficial - not affiliated with the Calibre-Web-Automated project.{Environment.NewLine}"
        + $"Support for newer (MTP) Kindles is experimental.{Environment.NewLine}{Environment.NewLine}"
        + $"Provided as is, without warranty of any kind. Use at your own risk.{Environment.NewLine}"
        + $"MIT License - {AppInfo.ProjectUrl}",
        $"About {AppInfo.Name}",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Startup must always end in a visible state (ready or error), never hang at 'starting...'.")]
    private async Task StartSafelyAsync()
    {
        try
        {
            await StartAsync().ConfigureAwait(true);
        }
        catch (Exception e)
        {
            SetStatus(e.Message, ok: false);
            Notify(e.Message, ToolTipIcon.Error);
        }
    }

    private async Task StartAsync()
    {
        var firstRun = !File.Exists(_paths.ConfigFile);
        AppConfig config;
        try
        {
            config = ConfigStore.LoadOrCreate(_paths.ConfigFile, _protector, out var passwordReset);
            if (passwordReset)
            {
                Notify("The saved SMTP password could not be read on this Windows account, so Paper Route made a new one. Open Settings and paste it into CWA's e-mail settings.", ToolTipIcon.Warning);
            }
        }
        catch (ConfigException e)
        {
            SetStatus(e.Message, ok: false);
            Notify(e.Message, ToolTipIcon.Error);
            return;
        }

        _host = new SmtpHost(config, _locator, _log, CreateConverter);
        _host.Delivered += (_, e) => _ui.Post(_ => OnDelivered(e.Outcome), null);
        _host.SecurityWarning += (_, e) => _ui.Post(_ => Notify(e.Message, ToolTipIcon.Warning), null);
        var result = await _host.StartAsync().ConfigureAwait(true);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "could not start", ok: false);
            Notify(result.Error ?? "Could not start", ToolTipIcon.Error);
            return;
        }

        SetStatus($"ready on {config.Listen}", ok: true);
        if (firstRun)
        {
            Notify("Paper Route is ready. Copy its settings into CWA's e-mail settings to start sending books.", ToolTipIcon.Info);
            ShowSettings();
        }
    }

    private void OnDelivered(DeliveryOutcome outcome)
    {
        if (_host?.Config.Notifications == false)
        {
            return;
        }

        var icon = outcome.Kind switch
        {
            OutcomeKind.Delivered or OutcomeKind.TestMessage => ToolTipIcon.Info,
            _ => ToolTipIcon.Warning,
        };
        Notify(outcome.Text, icon);
    }

    private void RefreshMenu()
    {
        _autostart.Checked = Autostart.IsEnabled();
        _kindle.Text = "Kindle: checking...";
        _calibre.Text = "Calibre: checking...";
        var configured = _host?.Config.CalibrePath;
        _ = Task.Run(() => (Readers: _locator.FindReaders(), Calibre: CalibreLocator.Find(configured)))
            .ContinueWith(scan => ShowScan(scan.Result.Readers, scan.Result.Calibre), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // Device scanning (USB drives, WMI, WPD) can take a moment, so it runs off the UI thread.
    private void ShowScan(IReadOnlyList<IReaderDevice> readers, string? calibre)
    {
        _kindle.Text = readers.Count switch
        {
            0 => "Kindle: none connected",
            1 => $"Kindle: {readers[0].DisplayName}" + (readers[0].Kind == DeviceKind.Mtp ? " (MTP, experimental)" : string.Empty),
            _ => "Kindle: more than one connected",
        };
        _calibre.Text = calibre is null
            ? "Calibre: not found - EPUB books can't be converted"
            : "Calibre: found - EPUB books are converted to AZW3";
    }

    // Looked up for every EPUB, so installing Calibre while Paper Route runs needs no restart.
    private static InstalledCalibre CreateConverter(AppConfig config) =>
        new(() => CalibreLocator.Find(config.CalibrePath), ConversionTimeout);

    private void ToggleAutostart()
    {
        Autostart.Set(!Autostart.IsEnabled());
        _autostart.Checked = Autostart.IsEnabled();
    }

    private void ShowSettings()
    {
        if (_host is null)
        {
            OpenFile(_paths.ConfigFile);
            return;
        }

        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsForm(_host.Config, ApplySettingsAsync);
        _settings.Icon = _readyIcon;
        _settings.Show();
    }

    private async Task<StartResult> ApplySettingsAsync(AppConfig config)
    {
        if (_host is null)
        {
            return new StartResult(false, "Paper Route is shutting down");
        }

        var result = await _host.RestartAsync(config).ConfigureAwait(true);
        if (!result.Ok)
        {
            return result;
        }

        SetStatus($"ready on {config.Listen}", ok: true);
        try
        {
            ConfigStore.Save(_paths.ConfigFile, config, _protector);
            return result;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new StartResult(false, $"The new settings are active but could not be saved ({e.Message}) - they will be lost when Paper Route restarts");
        }
    }

    private void SetStatus(string text, bool ok)
    {
        _status.Text = $"{AppInfo.Name} - {text}";
        _tray.Icon = ok ? _readyIcon : _errorIcon;
        var tooltip = ok ? $"{AppInfo.Name} - {text}" : $"{AppInfo.Name} - error: {text}";
        _tray.Text = tooltip.Length > MaxTooltipLength ? tooltip[..(MaxTooltipLength - 3)] + "..." : tooltip;
    }

    private void Notify(string text, ToolTipIcon icon) => _tray.ShowBalloonTip(5000, AppInfo.Name, text, icon);

    private async Task ExitAsync()
    {
        _settings?.Close();
        _tray.Visible = false;
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(true);
            _host = null;
        }

        ExitThread();
    }
}
