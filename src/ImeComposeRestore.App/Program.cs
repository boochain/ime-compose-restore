namespace ImeComposeRestore.App;

internal static class Program
{
    private const string MutexName = @"Local\ImeComposeRestore.boochain";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            MessageBox.Show(
                "IME 组字恢复已经在运行。请看任务栏托盘图标（可能被收进 ^ 箭头里）。",
                "IME 组字恢复",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }
}
