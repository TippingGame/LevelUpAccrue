using System.IO;
using System.Windows;

namespace LevelUpAccrue;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => HandleFatalException(args.Exception, args);

        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            HandleFatalException(ex);
        }
    }

    private void HandleFatalException(
        Exception exception,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs? dispatcherArgs = null)
    {
        if (dispatcherArgs is not null)
        {
            dispatcherArgs.Handled = true;
        }

        var dataDirectory = Environment.GetEnvironmentVariable("LEVELUP_ACCRUE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LevelUpAccrue");
        }

        string? logPath = null;
        try
        {
            Directory.CreateDirectory(dataDirectory);
            logPath = Path.Combine(dataDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(logPath, exception.ToString());
        }
        catch
        {
            // A logging failure must not hide the original startup error.
        }

        var logHint = logPath is null ? string.Empty : $"\n\n诊断日志：{logPath}";
        MessageBox.Show(
            $"程序遇到未处理的问题：\n\n{exception.Message}{logHint}",
            "增量记账",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }
}
