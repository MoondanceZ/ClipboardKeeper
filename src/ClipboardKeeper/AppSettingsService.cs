using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClipboardKeeper;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;

    public AppSettingsService()
    {
        _settingsPath = Path.Combine(AppSettings.DefaultStorageDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return Normalize(new AppSettings());
            }

            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync(stream, ClipboardHistoryJsonContext.Default.AppSettings);
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            return Normalize(new AppSettings());
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, normalized, ClipboardHistoryJsonContext.Default.AppSettings);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        var storageDirectory = string.IsNullOrWhiteSpace(settings.StorageDirectory)
            ? AppSettings.DefaultStorageDirectory
            : Environment.ExpandEnvironmentVariables(settings.StorageDirectory.Trim());

        var closeButtonAction = settings.CloseButtonAction == CloseButtonActions.ExitApplication
            ? CloseButtonActions.ExitApplication
            : CloseButtonActions.MinimizeToTray;

        return new AppSettings
        {
            StorageDirectory = storageDirectory,
            MaxHistoryItems = Math.Clamp(settings.MaxHistoryItems, 10, 10_000),
            SaveImages = settings.SaveImages,
            StartupEnabled = settings.StartupEnabled,
            CloseButtonAction = closeButtonAction,
            ShowWindowHotkey = GlobalHotkeyService.NormalizeDisplayText(settings.ShowWindowHotkey) ?? "Alt+C"
        };
    }
}
