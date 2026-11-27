using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace ImeComposeRestore.App;

public sealed class AppConfig
{
    public const string DefaultHotkey = "Ctrl+Alt+R";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ImeComposeRestore";

    public string Hotkey { get; set; } = DefaultHotkey;
    public bool StartWithWindows { get; set; }
    public bool PreferImmApi { get; set; } = true;
    public int KeyDelayMilliseconds { get; set; } = 8;
    public bool ShowNotification { get; set; } = true;
    public bool LogEnabled { get; set; } = true;

    [JsonIgnore]
    public static string ConfigDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ImeComposeRestore");

    [JsonIgnore]
    public static string ConfigPath => System.IO.Path.Combine(ConfigDirectory, "config.json");

    [JsonIgnore]
    public static string LogPath => System.IO.Path.Combine(ConfigDirectory, "restore.log");

    public static AppConfig Load()
    {
        try
        {
            if (System.IO.File.Exists(ConfigPath))
            {
                var json = System.IO.File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Keep defaults if the file is damaged.
        }

        var fresh = new AppConfig();
        fresh.Save();
        return fresh;
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, AppConfigJsonContext.Default.AppConfig);
        System.IO.File.WriteAllText(ConfigPath, json);
        ApplyStartup();
    }

    public void ApplyStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (key is null)
                return;
            if (StartWithWindows)
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else if (key.GetValue(RunValueName) is not null)
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry may be restricted; ignore.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigJsonContext : JsonSerializerContext;
