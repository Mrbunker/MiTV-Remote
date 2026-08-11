using MiTVRemote.Platform;

namespace MiTVRemote;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var context = new TrayApplicationContext(AppConfig.Load());
        Application.Run(context);
    }
}
