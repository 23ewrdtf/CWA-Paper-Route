namespace PaperRoute.App;

/// <summary>Application entry point.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\PaperRoute", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Paper Route is already running - look for its icon next to the clock.",
                AppInfo.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayContext(AppPaths.Default);
        Application.Run(context);
    }
}
