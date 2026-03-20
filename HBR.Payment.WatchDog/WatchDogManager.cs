using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace HBR.Payment.WatchDog;

public sealed class WatchDogManager
{
    private const string ConfigFileName = "watchdog.json";

    private readonly DispatcherTimer _timer;

    public WatchDogManager()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => RefreshAllTargets(ensureRunning: true);
    }

    public ObservableCollection<WatchDogTargetItem> Targets { get; } = [];

    public int CheckIntervalSeconds { get; set; } = 3;

    public void Load()
    {
        Targets.Clear();

        var config = LoadConfig();
        CheckIntervalSeconds = config.CheckIntervalSeconds;

        foreach (var target in config.Targets.OrderBy(target => target.StartOrder).ThenBy(target => target.Name))
        {
            Targets.Add(WatchDogTargetItem.FromConfig(target));
        }

        NormalizeStartOrder();
        ApplyTimerInterval();
        RefreshAllTargets(ensureRunning: false);
    }

    public void Save()
    {
        NormalizeStartOrder();
        ValidateUniqueExecutablePaths();

        var config = new WatchDogConfig
        {
            CheckIntervalSeconds = Math.Max(1, CheckIntervalSeconds),
            Targets = Targets.Select(target => target.ToConfig()).ToList()
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(GetConfigPath(), json);
        ApplyTimerInterval();
    }

    public void StartMonitoring()
    {
        ApplyTimerInterval();
        _timer.Start();
        RefreshAllTargets(ensureRunning: true);
    }

    public void StopMonitoring()
    {
        _timer.Stop();
    }

    public bool AddTargetIfMissing(string executablePath)
    {
        var resolvedPath = ResolvePath(executablePath);
        ValidateExecutablePath(resolvedPath);

        if (TryFindTargetByResolvedPath(resolvedPath, out _))
        {
            return false;
        }

        AddTargetInternal(resolvedPath);
        return true;
    }

    public WatchDogTargetItem AddTarget(string executablePath)
    {
        var resolvedPath = ResolvePath(executablePath);
        ValidateExecutablePath(resolvedPath);

        if (TryFindTargetByResolvedPath(resolvedPath, out var existing))
        {
            return existing!;
        }

        return AddTargetInternal(resolvedPath);
    }

    public WatchDogTargetItem? FindTargetByPath(string executablePath)
    {
        var resolvedPath = ResolvePath(executablePath);
        return TryFindTargetByResolvedPath(resolvedPath, out var existing) ? existing : null;
    }

    public bool MoveUp(WatchDogTargetItem target)
    {
        var index = Targets.IndexOf(target);
        if (index <= 0)
        {
            return false;
        }

        Targets.Move(index, index - 1);
        NormalizeStartOrder();
        return true;
    }

    public bool MoveDown(WatchDogTargetItem target)
    {
        var index = Targets.IndexOf(target);
        if (index < 0 || index >= Targets.Count - 1)
        {
            return false;
        }

        Targets.Move(index, index + 1);
        NormalizeStartOrder();
        return true;
    }

    public void RemoveTarget(WatchDogTargetItem target)
    {
        Targets.Remove(target);
        NormalizeStartOrder();
    }

    public void StartTarget(WatchDogTargetItem target)
    {
        var resolvedPath = ResolvePath(target.ExecutablePath);
        ValidateExecutablePath(resolvedPath);
        target.ClearManualStopRequested();

        if (WatchDogProcessHelper.GetRunningInstanceCount(resolvedPath) == 0)
        {
            WatchDogProcessHelper.StartProcess(
                resolvedPath,
                target.Arguments,
                ResolveWorkingDirectory(target, resolvedPath));
        }

        UpdateStatus(target);
    }

    public void StopTarget(WatchDogTargetItem target)
    {
        target.MarkManualStopRequested();

        var resolvedPath = ResolvePath(target.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            WatchDogProcessHelper.KillMatchingProcesses(resolvedPath);
        }

        UpdateStatus(target);
    }

    public void RestartTarget(WatchDogTargetItem target)
    {
        var resolvedPath = ResolvePath(target.ExecutablePath);
        ValidateExecutablePath(resolvedPath);
        target.ClearManualStopRequested();

        WatchDogProcessHelper.KillMatchingProcesses(resolvedPath);
        WatchDogProcessHelper.StartProcess(
            resolvedPath,
            target.Arguments,
            ResolveWorkingDirectory(target, resolvedPath));

        UpdateStatus(target);
    }

    public void RefreshAllTargets(bool ensureRunning)
    {
        foreach (var target in Targets.OrderBy(target => target.StartOrder).ToList())
        {
            RefreshTarget(target, ensureRunning);
        }
    }

    public void StopManagedTargetsOnExit()
    {
        foreach (var target in Targets.Where(target => target.IsEnabled))
        {
            StopTarget(target);
        }
    }

    private WatchDogTargetItem AddTargetInternal(string resolvedPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(resolvedPath);
        var item = new WatchDogTargetItem
        {
            StartOrder = Targets.Count + 1,
            Name = string.IsNullOrWhiteSpace(fileName) ? "新进程" : fileName,
            ExecutablePath = resolvedPath,
            WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? string.Empty,
            IsEnabled = true
        };

        Targets.Add(item);
        item.UpdateStatus(0);
        return item;
    }

    private void NormalizeStartOrder()
    {
        for (var index = 0; index < Targets.Count; index++)
        {
            Targets[index].StartOrder = index + 1;
        }
    }

    private void ValidateUniqueExecutablePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in Targets)
        {
            var resolvedPath = ResolvePath(target.ExecutablePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                continue;
            }

            if (!seen.Add(resolvedPath))
            {
                throw new InvalidOperationException($"存在重复的程序路径：{resolvedPath}");
            }
        }
    }

    private bool TryFindTargetByResolvedPath(string resolvedPath, out WatchDogTargetItem? target)
    {
        target = Targets.FirstOrDefault(item => PathsEqual(ResolvePath(item.ExecutablePath), resolvedPath));
        return target is not null;
    }

    private void RefreshTarget(WatchDogTargetItem target, bool ensureRunning)
    {
        var resolvedPath = ResolvePath(target.ExecutablePath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            target.UpdateStatus(0, "程序路径为空");
            return;
        }

        if (!File.Exists(resolvedPath))
        {
            target.UpdateStatus(0, "程序文件不存在");
            return;
        }

        var runningCount = WatchDogProcessHelper.GetRunningInstanceCount(resolvedPath);
        if (ensureRunning && target.IsEnabled && !target.IsManualStopRequested && runningCount == 0)
        {
            WatchDogProcessHelper.StartProcess(
                resolvedPath,
                target.Arguments,
                ResolveWorkingDirectory(target, resolvedPath));

            runningCount = WatchDogProcessHelper.GetRunningInstanceCount(resolvedPath);
        }

        if (!target.IsEnabled)
        {
            target.UpdateStatus(runningCount, runningCount > 0 ? $"已禁用（运行中 {runningCount}）" : "已禁用");
            return;
        }

        if (target.IsManualStopRequested)
        {
            target.UpdateStatus(runningCount, runningCount > 0 ? $"已手动停止（仍有 {runningCount} 个实例）" : "已手动停止");
            return;
        }

        target.UpdateStatus(runningCount);
    }

    private void UpdateStatus(WatchDogTargetItem target)
    {
        RefreshTarget(target, ensureRunning: false);
    }

    private void ApplyTimerInterval()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, CheckIntervalSeconds));
    }

    private static WatchDogConfig LoadConfig()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            return new WatchDogConfig();
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<WatchDogConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? new WatchDogConfig();
    }

    private static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, ConfigFileName);
    }

    private static string ResolveWorkingDirectory(WatchDogTargetItem target, string resolvedExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
        {
            return ResolvePath(target.WorkingDirectory);
        }

        return Path.GetDirectoryName(resolvedExecutablePath) ?? AppContext.BaseDirectory;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateExecutablePath(string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            throw new InvalidOperationException("程序路径为空。");
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("程序文件不存在。", resolvedPath);
        }

        if (!string.Equals(Path.GetExtension(resolvedPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("启动参数必须是 exe 程序路径。");
        }
    }
}
