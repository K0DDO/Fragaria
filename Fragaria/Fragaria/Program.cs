using Fragaria.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace Fragaria;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Info("Fragaria starting");

        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            AppContext.BaseDirectory);

        if (!RuntimeChecks.EnsureWebView2())
            return;

        try
        {
            ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Application.Start failed", ex);
            NativeDialog.ShowFatal(
                "Fragaria",
                $"Ошибка запуска: {ex.Message}\n\n{AppLogger.LogFilePath}");
        }
    }
}
