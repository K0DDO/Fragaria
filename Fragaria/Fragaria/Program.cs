using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Fragaria;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p => new App());
    }
}
