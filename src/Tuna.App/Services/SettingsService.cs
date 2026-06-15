using System.IO;
using System.Text.Json;

namespace Tuna.App.Services;

/// <summary>用户设置(持久化到 %AppData%\Tuna\settings.json)。</summary>
public sealed class AppSettings
{
    /// <summary>"zh" / "en";空=跟随系统。</summary>
    public string Language { get; set; } = "";
    public bool AutoSwitch { get; set; }
    public bool MinimizeToTray { get; set; } = true;
}

/// <summary>轻量 JSON 设置读写,失败一律静默回退默认(不影响主流程)。</summary>
public static class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tuna");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* 读坏了用默认 */ }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 写不了不致命 */ }
    }
}
