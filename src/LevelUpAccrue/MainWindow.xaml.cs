using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LevelUpAccrue.Dialogs;
using LevelUpAccrue.Models;
using LevelUpAccrue.Services;
using LevelUpAccrue.ViewModels;
using Microsoft.Win32;

namespace LevelUpAccrue;

public partial class MainWindow : Window
{
    private readonly LedgerStore _store;
    private readonly DispatcherTimer _saveTimer;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        _store = new LedgerStore();
        ViewModel = new MainViewModel(_store.LoadOrCreate());
        ViewModel.DataChanged += ViewModel_DataChanged;
        DataContext = ViewModel;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += SaveTimer_Tick;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    public MainViewModel ViewModel { get; }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_store.RecoveryMessage))
        {
            MessageBox.Show(_store.RecoveryMessage, "数据恢复", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ViewModel_DataChanged(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        SaveNow(showError: true);
    }

    private void SaveNow(bool showError)
    {
        try
        {
            _store.Save(ViewModel.Data);
            ViewModel.StatusMessage = $"已自动保存 · {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ViewModel.StatusMessage = "保存失败";
            if (showError)
            {
                MessageBox.Show($"数据保存失败：\n\n{ex.Message}", "增量记账", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void NewPeriod_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewPeriodDialog(ViewModel.SuggestedNextMonth) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (ViewModel.HasPeriod(dialog.SelectedMonth))
        {
            MessageBox.Show("这个月份已经有账期了。", "新建账期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ViewModel.CreatePeriod(dialog.SelectedMonth, dialog.CarryForward);
    }

    private void DeletePeriod_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPeriod is null)
        {
            return;
        }

        if (ViewModel.Periods.Count <= 1)
        {
            MessageBox.Show("至少要保留一个账期。", "删除账期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定删除 {ViewModel.SelectedPeriod.DisplayName} 吗？\n\n删除后，该月金额和报销状态将无法恢复。",
            "删除账期",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            ViewModel.DeleteSelectedPeriod();
        }
    }

    private void AddPerson_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPeriod is null)
        {
            MessageBox.Show("请先新建账期，再添加人员。", "添加人员", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new AddPersonDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ViewModel.AddPerson(dialog.PersonName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(ex.Message, "添加人员", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeletePerson_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null || ViewModel.SelectedPeriod is null)
        {
            MessageBox.Show("请先在表格中选中一名人员。", "删除人员", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定删除 {ViewModel.SelectedEntry.Name} 吗？\n\n历史账期会保留；此人将从当前及后续账期中移除。",
            "删除人员",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            ViewModel.DeactivateSelectedPerson();
        }
    }

    private void ImportTxt_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "选择报销文本",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            Multiselect = true
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var parsed = picker.FileNames
                .SelectMany(path => TextLedgerService.Parse(File.ReadAllText(path), Path.GetFileName(path)))
                .ToList();
            if (parsed.Count == 0)
            {
                MessageBox.Show("没有识别到“姓名：金额”格式的数据。", "导入 TXT", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ImportDialog(parsed, picker.FileNames.Length) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                ViewModel.ImportAmounts(parsed, dialog.AmountMode, dialog.MarkReimbursed);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"读取文件失败：\n\n{ex.Message}", "导入 TXT", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        SaveNow(showError: true);
        var picker = new SaveFileDialog
        {
            Title = "导出完整备份",
            Filter = "增量记账备份 (*.json)|*.json",
            FileName = $"增量记账备份_{DateTime.Now:yyyyMMdd_HHmm}.json",
            AddExtension = true,
            DefaultExt = ".json"
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _store.ExportBackup(ViewModel.Data, picker.FileName);
            ViewModel.StatusMessage = $"备份已导出：{Path.GetFileName(picker.FileName)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"备份导出失败：\n\n{ex.Message}", "备份", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "选择完整备份",
            Filter = "增量记账备份 (*.json)|*.json|所有文件 (*.*)|*.*"
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        var result = MessageBox.Show(
            "恢复备份会替换当前账本。程序会先保留当前数据，是否继续？",
            "恢复备份",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _store.ExportBackup(ViewModel.Data, Path.Combine(
                _store.BackupDirectory,
                $"恢复前备份_{DateTime.Now:yyyyMMdd_HHmmss}.json"));
            var restored = _store.LoadFromFile(picker.FileName);
            ViewModel.ReplaceData(restored);
            SaveNow(showError: true);
            ViewModel.StatusMessage = "备份恢复完成";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            MessageBox.Show($"备份恢复失败：\n\n{ex.Message}", "恢复备份", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_store.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _store.DataDirectory) { UseShellExecute = true });
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ViewModel.SelectVisibleEntries();
    private void ClearSelection_Click(object sender, RoutedEventArgs e) => ViewModel.ClearPreviewSelection();

    private void MarkSelectedPaid_Click(object sender, RoutedEventArgs e)
    {
        if (EnsurePreviewSelection())
        {
            ViewModel.SetSelectedReimbursed(true);
        }
    }

    private void MarkSelectedPending_Click(object sender, RoutedEventArgs e)
    {
        if (EnsurePreviewSelection())
        {
            ViewModel.SetSelectedReimbursed(false);
        }
    }

    private void CopyPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsurePreviewSelection())
        {
            return;
        }

        Clipboard.SetText(ViewModel.PreviewText);
        ViewModel.StatusMessage = "清单已复制到剪贴板";
    }

    private void ExportPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsurePreviewSelection() || ViewModel.SelectedPeriod is null)
        {
            return;
        }

        var picker = new SaveFileDialog
        {
            Title = "导出金额清单",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = $"{ViewModel.SelectedPeriod.Month:yyyyMM}报销清单.txt",
            AddExtension = true,
            DefaultExt = ".txt"
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(picker.FileName, ViewModel.PreviewText);
            ViewModel.StatusMessage = $"清单已导出：{Path.GetFileName(picker.FileName)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"清单导出失败：\n\n{ex.Message}", "导出 TXT", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool EnsurePreviewSelection()
    {
        if (ViewModel.SelectedCount > 0)
        {
            return true;
        }

        MessageBox.Show("请先勾选要预览的人员。", "金额预览", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void EntriesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(ViewModel.RefreshEntriesView, DispatcherPriority.ContextIdle);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.N:
                NewPeriod_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.I:
                ImportTxt_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.B:
                Backup_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                break;
            case Key.S:
                SaveNow(showError: true);
                e.Handled = true;
                break;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _saveTimer.Stop();
        SaveNow(showError: false);
    }
}
