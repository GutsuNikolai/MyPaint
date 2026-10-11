using System.Text.Json;

namespace Paint;

internal sealed class AppSettings
{
    // Zero means that new canvases use the available window area.
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "mypaint");
    public static string DrawingsFolder => Path.Combine(AppFolder, "drawings");
    public static string FontsFolder => Path.Combine(AppFolder, "fonts");
    public string SaveFolder { get; set; } = DrawingsFolder;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyPaint", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (settings == null || settings.CanvasWidth < 0 || settings.CanvasWidth > 4096
                || settings.CanvasHeight < 0 || settings.CanvasHeight > 4096
                || (settings.CanvasWidth == 0) != (settings.CanvasHeight == 0)
                || string.IsNullOrWhiteSpace(settings.SaveFolder)) return new AppSettings();
            settings.SaveFolder = Path.GetFullPath(settings.SaveFolder);
            if (string.Equals(settings.SaveFolder.TrimEnd(Path.DirectorySeparatorChar),
                AppFolder, StringComparison.OrdinalIgnoreCase))
                settings.SaveFolder = DrawingsFolder;
            return settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
