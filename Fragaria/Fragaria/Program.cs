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
        try
        {
            var hr = Bootstrap.TryInitialize(0x00010006);
            AppLogger.Info($"Bootstrap.TryInitialize hr=0x{hr:X8}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Bootstrap failed", ex);
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
            throw;
        }
    }
}
