using System.ComponentModel;
using System.Windows;

namespace HBR.Payment.WatchDog;

public partial class ConfigWindow : Window
{
    private readonly WatchDogManager _manager;
    private bool _allowClose;
    private bool _isUpdatingSelectAllCheckBox;

    public ConfigWindow(WatchDogManager manager)
    {
        InitializeComponent();

        _manager = manager;
        TargetsGrid.ItemsSource = _manager.Targets;
        CheckIntervalTextBox.Text = _manager.CheckIntervalSeconds.ToString();
        UpdateSelectAllCheckBoxState();
    }

    public void ShowWindow()
    {
        CheckIntervalTextBox.Text = _manager.CheckIntervalSeconds.ToString();

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void PrepareForExit()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        try
        {
            CommitPendingEdits();
            ApplyCheckInterval();
            _manager.Save();
        }
        catch
        {
            // Hide to tray even if save fails; the user can reopen the window and fix it.
        }

        Hide();
    }

    private void SelectAllCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        SetAllChecked(true);
    }

    private void SelectAllCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelectAllCheckBox)
        {
            return;
        }

        SetAllChecked(false);
    }

    private void RowCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateSelectAllCheckBoxState();
    }

    private void MoveUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();

        var selectedTarget = GetSelectedTarget();
        if (selectedTarget is null)
        {
            return;
        }

        if (_manager.MoveUp(selectedTarget))
        {
            TargetsGrid.SelectedItem = selectedTarget;
            TargetsGrid.ScrollIntoView(selectedTarget);
            _manager.Save();
        }
    }

    private void MoveDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();

        var selectedTarget = GetSelectedTarget();
        if (selectedTarget is null)
        {
            return;
        }

        if (_manager.MoveDown(selectedTarget))
        {
            TargetsGrid.SelectedItem = selectedTarget;
            TargetsGrid.ScrollIntoView(selectedTarget);
            _manager.Save();
        }
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择需要保活的程序",
            Filter = "可执行文件 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!_manager.AddTargetIfMissing(dialog.FileName))
        {
            var existing = _manager.FindTargetByPath(dialog.FileName);
            if (existing is not null)
            {
                TargetsGrid.SelectedItem = existing;
                TargetsGrid.ScrollIntoView(existing);
            }

            ShowInfo("该程序路径已存在，未重复添加。");
            return;
        }

        var item = _manager.FindTargetByPath(dialog.FileName);
        if (item is not null)
        {
            TargetsGrid.SelectedItem = item;
            TargetsGrid.ScrollIntoView(item);
        }

        UpdateSelectAllCheckBoxState();
        _manager.Save();
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();

        var targets = GetActionTargets().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var message = targets.Count == 1
            ? $"确认删除进程“{targets[0].Name}”吗？"
            : $"确认删除选中的 {targets.Count} 个进程吗？";

        if (!ConfirmDangerousAction(message, "删除确认"))
        {
            return;
        }

        foreach (var target in targets)
        {
            _manager.RemoveTarget(target);
        }

        UpdateSelectAllCheckBoxState();
        _manager.Save();
    }

    private void RunButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExecuteTargetsAction(_manager.StartTarget);
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();

        var targets = GetActionTargets().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var message = targets.Count == 1
            ? $"确认停止进程“{targets[0].Name}”吗？{Environment.NewLine}停止后将进入手动停止状态，不会自动拉起。"
            : $"确认停止选中的 {targets.Count} 个进程吗？{Environment.NewLine}停止后将进入手动停止状态，不会自动拉起。";

        if (!ConfirmDangerousAction(message, "停止确认"))
        {
            return;
        }

        ExecuteTargetsAction(_manager.StopTarget, targets);
    }

    private void RestartButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExecuteTargetsAction(_manager.RestartTarget);
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();
        _manager.RefreshAllTargets(ensureRunning: false);
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CommitPendingEdits();
            ApplyCheckInterval();
            _manager.Save();
            _manager.RefreshAllTargets(ensureRunning: false);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ExecuteTargetsAction(Action<WatchDogTargetItem> action)
    {
        ExecuteTargetsAction(action, GetActionTargets().ToList());
    }

    private void ExecuteTargetsAction(Action<WatchDogTargetItem> action, IReadOnlyCollection<WatchDogTargetItem> targets)
    {
        try
        {
            CommitPendingEdits();

            if (targets.Count == 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                action(target);
            }

            _manager.Save();
            _manager.RefreshAllTargets(ensureRunning: false);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private IEnumerable<WatchDogTargetItem> GetActionTargets()
    {
        var checkedTargets = _manager.Targets.Where(target => target.IsChecked).ToList();
        if (checkedTargets.Count > 0)
        {
            return checkedTargets;
        }

        var selectedTarget = GetSelectedTarget();
        return selectedTarget is null ? Enumerable.Empty<WatchDogTargetItem>() : [selectedTarget];
    }

    private WatchDogTargetItem? GetSelectedTarget()
    {
        return TargetsGrid.SelectedItem as WatchDogTargetItem;
    }

    private void CommitPendingEdits()
    {
        TargetsGrid.CommitEdit();
        TargetsGrid.CommitEdit();
    }

    private void ApplyCheckInterval()
    {
        if (!int.TryParse(CheckIntervalTextBox.Text, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException("检查间隔必须是大于 0 的整数。");
        }

        _manager.CheckIntervalSeconds = seconds;
    }

    private void SetAllChecked(bool isChecked)
    {
        foreach (var target in _manager.Targets)
        {
            target.IsChecked = isChecked;
        }

        UpdateSelectAllCheckBoxState();
    }

    private void UpdateSelectAllCheckBoxState()
    {
        if (SelectAllCheckBox is null)
        {
            return;
        }

        _isUpdatingSelectAllCheckBox = true;

        if (_manager.Targets.Count == 0)
        {
            SelectAllCheckBox.IsChecked = false;
        }
        else
        {
            var checkedCount = _manager.Targets.Count(target => target.IsChecked);
            SelectAllCheckBox.IsChecked = checkedCount switch
            {
                0 => false,
                var count when count == _manager.Targets.Count => true,
                _ => null
            };
        }

        _isUpdatingSelectAllCheckBox = false;
    }

    private bool ConfirmDangerousAction(string message, string title)
    {
        return System.Windows.MessageBox.Show(
                   this,
                   message,
                   title,
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void ShowInfo(string message)
    {
        System.Windows.MessageBox.Show(
            this,
            message,
            "HBR 支付看门狗",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowError(string message)
    {
        System.Windows.MessageBox.Show(
            this,
            message,
            "HBR 支付看门狗",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
