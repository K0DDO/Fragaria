using Fragaria.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using WinRT;

namespace Fragaria;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Info("Fragaria starting");

        if (!RuntimeChecks.EnsureWebView2())
            return;

        if (!Bootstrap.TryInitialize(0x00010006, out int hr))
        {
            AppLogger.Error($"Bootstrap.TryInitialize failed: 0x{hr:X8}");
            NativeDialog.ShowFatal(
                "Fragaria",
                $"Не удалось инициализировать Windows App SDK (0x{hr:X8}).\n\n" +
                $"Переустановите Fragaria или установите WebView2 Runtime.\n\n{AppLogger.LogFilePath}");
            return;
        }

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
        finally
        {
            Bootstrap.Shutdown();
        }
    }
}
