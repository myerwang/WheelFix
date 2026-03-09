using System;
using System.Windows.Forms;

namespace WheelFix;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.Run(new TrayAppContext());
    }
}
