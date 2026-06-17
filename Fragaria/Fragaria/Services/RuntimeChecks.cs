using Microsoft.Win32;

namespace Fragaria.Services;

internal static class RuntimeChecks
{
    public static bool EnsureWebView2()
    {
        if (IsWebView2Installed())
            return true;

        AppLogger.Error("WebView2 Runtime is not installed");
        NativeDialog.ShowFatal(
            "Fragaria",
            "Для работы Fragaria нужен Microsoft Edge WebView2 Runtime.\n\n" +
            "Переустановите Fragaria и оставьте включённой установку WebView2,\n" +
            "или скачайте Runtime с сайта Microsoft.\n\n" +
            AppLogger.LogFilePath);
        return false;
    }

    private static bool IsWebView2Installed()
    {
        const string clientKey =
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(clientKey);
            var version = key?.GetValue("pv") as string;
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                var version = key?.GetValue("pv") as string;
                return !string.IsNullOrWhiteSpace(version);
            }
            catch
            {
                return false;
            }
        }
    }
}
