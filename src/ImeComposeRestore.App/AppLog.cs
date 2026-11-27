namespace ImeComposeRestore.App;

internal static class AppLog
{
    private static readonly object Gate = new();
    public static bool Enabled { get; set; } = true;

    public static void Write(string message)
    {
        if (!Enabled)
            return;
        try
        {
            System.IO.Directory.CreateDirectory(AppConfig.ConfigDirectory);
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine;
            lock (Gate)
            {
                System.IO.File.AppendAllText(AppConfig.LogPath, line);
            }
        }
        catch
        {
            // Logging must never break restore.
        }
    }
}
