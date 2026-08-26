using System.Windows;
using LevelUpAccrue.Models;

namespace LevelUpAccrue.Dialogs;

public partial class ImportDialog : Window
{
    public ImportDialog(IReadOnlyList<ImportedAmount> amounts, int fileCount)
    {
        InitializeComponent();
        PreviewGrid.ItemsSource = amounts;
        SummaryText.Text = $"已从 {fileCount} 个文件识别 {amounts.Count} 条记录，合计 ¥ {amounts.Sum(item => item.Amount):N2}";
    }

    public ImportAmountMode AmountMode => CumulativeMode.IsChecked == true
        ? ImportAmountMode.Cumulative
        : ImportAmountMode.Increment;

    public bool MarkReimbursed => MarkReimbursedBox.IsChecked == true;

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
