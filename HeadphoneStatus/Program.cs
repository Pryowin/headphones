using System.Windows.Forms;

namespace HeadphoneStatus;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var tray = new TrayApp();
        Application.Run();
    }
}
