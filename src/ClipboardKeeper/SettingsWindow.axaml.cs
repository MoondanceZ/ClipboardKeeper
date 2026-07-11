using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace ClipboardKeeper;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
        : this(new AppSettings())
    {
    }

    public SettingsWindow(AppSettings settings)
    {
        WindowDecorations = WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.None
        ];
        Background = Brushes.Transparent;
        InitializeComponent();
        Surface.AddHandler(PointerPressedEvent, DragWindowFromTopArea, RoutingStrategies.Tunnel);
        TitleBar.AddHandler(PointerPressedEvent, DragWindow, RoutingStrategies.Tunnel);
        KeyDown += SettingsWindow_KeyDown;
        StorageDirectoryBox.Text = settings.StorageDirectory;
        MaxItemsBox.Text = settings.MaxHistoryItems.ToString();
        SaveImagesBox.IsChecked = settings.SaveImages;
        StartupBox.IsChecked = settings.StartupEnabled;
        HotkeyBox.Text = settings.ShowWindowHotkey;
        HotkeyBox.KeyDown += HotkeyBox_KeyDown;
        CloseMinimizeBox.IsChecked = settings.CloseButtonAction != CloseButtonActions.ExitApplication;
        CloseExitBox.IsChecked = settings.CloseButtonAction == CloseButtonActions.ExitApplication;
    }

    public AppSettings? Result { get; private set; }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择剪切板历史存储目录",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            StorageDirectoryBox.Text = folders[0].Path.LocalPath;
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        var storageDirectory = Environment.ExpandEnvironmentVariables(StorageDirectoryBox.Text?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            ErrorText.Text = "请填写存储目录";
            return;
        }

        if (!int.TryParse(MaxItemsBox.Text, out var maxItems) || maxItems < 10 || maxItems > 10_000)
        {
            ErrorText.Text = "保存条数需要在 10 到 10000 之间";
            return;
        }

        try
        {
            Directory.CreateDirectory(storageDirectory);
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"目录不可用: {ex.Message}";
            return;
        }

        Result = AppSettingsService.Normalize(new AppSettings
        {
            StorageDirectory = storageDirectory,
            MaxHistoryItems = maxItems,
            SaveImages = SaveImagesBox.IsChecked == true,
            StartupEnabled = StartupBox.IsChecked == true,
            CloseButtonAction = CloseExitBox.IsChecked == true
                ? CloseButtonActions.ExitApplication
                : CloseButtonActions.MinimizeToTray,
            ShowWindowHotkey = HotkeyBox.Text?.Trim() ?? string.Empty
        });
        Close(Result);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void SettingsWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key is Key.Back or Key.Delete)
        {
            HotkeyBox.Text = string.Empty;
            return;
        }

        var hotkey = FormatHotkey(e.KeyModifiers, e.Key);
        if (hotkey is not null)
        {
            HotkeyBox.Text = hotkey;
        }
    }

    private static string? FormatHotkey(KeyModifiers modifiers, Key key)
    {
        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return null;
        }

        var parts = new System.Collections.Generic.List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        var keyText = KeyToText(key);
        if (parts.Count == 0 || keyText is null)
        {
            return null;
        }

        parts.Add(keyText);
        return string.Join("+", parts);
    }

    private static string? KeyToText(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return key.ToString();
        }

        return null;
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual))
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void DragWindowFromTopArea(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual))
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.GetPosition(this).Y <= 92)
        {
            BeginMoveDrag(e);
        }
    }

    private static bool IsInsideButton(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual is Button)
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }
}
