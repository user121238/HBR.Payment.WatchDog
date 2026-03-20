using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HBR.Payment.WatchDog;

public sealed class WatchDogConfig
{
    public int CheckIntervalSeconds { get; set; } = 3;

    public List<WatchDogTargetConfig> Targets { get; set; } = [];
}

public sealed class WatchDogTargetConfig
{
    public int StartOrder { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

public sealed class WatchDogTargetItem : INotifyPropertyChanged
{
    private bool _isChecked;
    private int _startOrder;
    private string _name = string.Empty;
    private string _executablePath = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private bool _isEnabled = true;
    private bool _isManualStopRequested;
    private int _runningInstanceCount;
    private string _statusText = "已停止";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public int StartOrder
    {
        get => _startOrder;
        set => SetField(ref _startOrder, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetField(ref _executablePath, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetField(ref _arguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetField(ref _workingDirectory, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public bool IsManualStopRequested
    {
        get => _isManualStopRequested;
        private set => SetField(ref _isManualStopRequested, value);
    }

    public int RunningInstanceCount
    {
        get => _runningInstanceCount;
        private set => SetField(ref _runningInstanceCount, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public void MarkManualStopRequested()
    {
        IsManualStopRequested = true;
    }

    public void ClearManualStopRequested()
    {
        IsManualStopRequested = false;
    }

    public void UpdateStatus(int runningInstanceCount, string? statusOverride = null)
    {
        RunningInstanceCount = runningInstanceCount;
        StatusText = statusOverride ?? (runningInstanceCount > 0 ? $"运行中（{runningInstanceCount}）" : "已停止");
    }

    public WatchDogTargetConfig ToConfig()
    {
        return new WatchDogTargetConfig
        {
            StartOrder = StartOrder,
            Name = Name,
            ExecutablePath = ExecutablePath,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            IsEnabled = IsEnabled
        };
    }

    public static WatchDogTargetItem FromConfig(WatchDogTargetConfig config)
    {
        return new WatchDogTargetItem
        {
            StartOrder = config.StartOrder,
            Name = config.Name,
            ExecutablePath = config.ExecutablePath,
            Arguments = config.Arguments,
            WorkingDirectory = config.WorkingDirectory,
            IsEnabled = config.IsEnabled
        };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
