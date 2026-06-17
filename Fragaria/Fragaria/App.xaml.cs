using Microsoft.UI.Xaml;
using Fragaria.Services;

namespace Fragaria;

public partial class App : Application
{
    public App()
    {
        UnhandledException += (_, e) =>
        {
            AppLogger.Error("Unhandled UI exception", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLogger.Error("Unhandled domain exception", e.ExceptionObject as Exception);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            AppLogger.Info("OnLaunched");
            var window = new MainWindow();
            window.Activate();
        }
        catch (Exception ex)
        {
            AppLogger.Error("OnLaunched failed", ex);
            NativeDialog.ShowFatal(
                "Fragaria",
                $"Не удалось открыть окно: {ex.Message}\n\n{AppLogger.LogFilePath}");
        }
    }
}
