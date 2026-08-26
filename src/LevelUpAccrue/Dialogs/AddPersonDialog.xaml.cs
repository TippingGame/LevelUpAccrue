using System.Windows;

namespace LevelUpAccrue.Dialogs;

public partial class AddPersonDialog : Window
{
    public AddPersonDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    public string PersonName => NameBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (PersonName.Length == 0)
        {
            MessageBox.Show("请输入人员姓名。", "添加人员", MessageBoxButton.OK, MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
