using System.Windows;

namespace LevelUpAccrue.Dialogs;

public partial class NewPeriodDialog : Window
{
    public NewPeriodDialog(DateTime suggestedMonth)
    {
        InitializeComponent();
        MonthPicker.SelectedDate = suggestedMonth;
    }

    public DateTime SelectedMonth
    {
        get
        {
            var value = MonthPicker.SelectedDate ?? DateTime.Today;
            return new DateTime(value.Year, value.Month, 1);
        }
    }

    public bool CarryForward => CarryForwardBox.IsChecked == true;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (MonthPicker.SelectedDate is null)
        {
            MessageBox.Show("请选择一个月份。", "新建账期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
