using System.Windows;

namespace LevelUpAccrue.Dialogs;

public partial class NewPeriodDialog : Window
{
    public NewPeriodDialog(DateTime suggestedDate)
    {
        InitializeComponent();
        DatePicker.SelectedDate = suggestedDate;
    }

    public DateTime SelectedDateTime { get; private set; }

    public bool CarryForward => CarryForwardBox.IsChecked == true;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DatePicker.SelectedDate is null)
        {
            MessageBox.Show("请选择账期日期。", "新建账期", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedDateTime = DatePicker.SelectedDate.Value.Date.Add(DateTime.Now.TimeOfDay);
        DialogResult = true;
    }
}
