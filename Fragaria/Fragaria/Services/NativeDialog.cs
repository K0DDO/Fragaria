namespace Fragaria.Services;

internal static class NativeDialog
{
    public static void ShowFatal(string title, string message)
    {
        try
        {
            System.Windows.Forms.MessageBox.Show(
                message,
                title,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
        catch
        {
            try { Console.Error.WriteLine($"{title}: {message}"); } catch { }
        }
    }
}
