using System.Windows;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace HBR.Payment.WatchDog;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Global\HBR.Payment.WatchDog.SingleInstance";

    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private WatchDogManager? _manager;
    private ConfigWindow? _configWindow;
    private Mutex? _singleInstanceMutex;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "看门狗程序已经在运行。",
                "HBR.Payment.WatchDog",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _manager = new WatchDogManager();

        try
        {
            _manager.Load();
            RegisterTargetsFromArguments(e.Args);
            _manager.StartMonitoring();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"初始化看门狗失败。{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "HBR.Payment.WatchDog",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        InitializeTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _trayMenu?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
                // Ignore release failures on shutdown.
            }

            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void RegisterTargetsFromArguments(string[] args)
    {
        if (_manager is null || args.Length == 0)
        {
            return;
        }

        var addedAny = false;
        var errors = new List<string>();

        foreach (var argument in args)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            try
            {
                if (_manager.AddTargetIfMissing(argument))
                {
                    addedAny = true;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{argument}{Environment.NewLine}{ex.Message}");
            }
        }

        if (addedAny)
        {
            _manager.Save();
        }

        if (errors.Count > 0)
        {
            System.Windows.MessageBox.Show(
                $"以下启动参数写入配置失败：{Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", errors)}",
                "HBR.Payment.WatchDog",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void InitializeTrayIcon()
    {
        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add("配置", null, (_, _) => ShowConfigurationWindow());
        _trayMenu.Items.Add("退出", null, (_, _) => ExitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "HBR 支付看门狗",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = _trayMenu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowConfigurationWindow();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {

            Uri uri = new Uri("z3du2-0fupz-001.ico", UriKind.Relative);

            var info = Application.GetResourceStream(uri);

            using var stream = info?.Stream;
            if (stream != null)
            {
                return new Icon(stream);
            }

            
        }
        catch
        {
            // Ignore errors when loading icon from resources
        }

        return SystemIcons.Application;
    }

    private void ShowConfigurationWindow()
    {
        if (_manager is null || _isExiting)
        {
            return;
        }

        _configWindow ??= new ConfigWindow(_manager);
        _configWindow.ShowWindow();
    }

    private void ExitApplication()
    {
        if (_isExiting || _manager is null)
        {
            return;
        }

        _isExiting = true;

        _manager.StopMonitoring();
        _manager.StopManagedTargetsOnExit();

        if (_configWindow is not null)
        {
            _configWindow.PrepareForExit();
            _configWindow.Close();
        }

        Shutdown();
    }
}
