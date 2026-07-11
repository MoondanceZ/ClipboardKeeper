using System;
using System.IO;

namespace ClipboardKeeper;

public sealed class AppSettings
{
    public string StorageDirectory { get; set; } = DefaultStorageDirectory;

    public int MaxHistoryItems { get; set; } = ClipboardHistoryStore.DefaultMaxItems;

    public bool SaveImages { get; set; }

    public bool StartupEnabled { get; set; }

    public string CloseButtonAction { get; set; } = CloseButtonActions.MinimizeToTray;

    public string ShowWindowHotkey { get; set; } = "Alt+C";

    public static string DefaultStorageDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "ClipboardKeeper");
        }
    }
}

public static class CloseButtonActions
{
    public const string MinimizeToTray = "MinimizeToTray";

    public const string ExitApplication = "ExitApplication";
}
