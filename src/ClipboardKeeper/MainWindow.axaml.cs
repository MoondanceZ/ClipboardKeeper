using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace ClipboardKeeper;

public partial class MainWindow : Window
{
    private const double TopDragHeight = 82;
    private readonly bool _startMinimized;
    private MainWindowViewModel? _viewModel;
    private readonly TrayIcon _trayIcon;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private bool _allowClose;

    public MainWindow()
    {
        _startMinimized = ShouldStartMinimized();
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowActivated = !_startMinimized;
        WindowState = _startMinimized ? WindowState.Minimized : WindowState.Normal;
        ExtendClientAreaToDecorationsHint = true;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.None
        ];
        Background = Brushes.Transparent;

        InitializeComponent();
        SetAppImages();
        Surface.AddHandler(PointerPressedEvent, DragWindowFromTopArea, RoutingStrategies.Tunnel);
        TitleBar.AddHandler(PointerPressedEvent, DragWindow, RoutingStrategies.Tunnel);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _trayIcon = CreateTrayIcon();
        _hotkeyService.Pressed += (_, _) => ShowFromTray();
        Closed += (_, _) =>
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _hotkeyService.Dispose();
            _viewModel?.Stop();
        };
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        _viewModel = new MainWindowViewModel(clipboard);
        DataContext = _viewModel;
        await _viewModel.InitializeAsync();
        RegisterShowWindowHotkey();
        if (_startMinimized)
        {
            Hide();
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.CloseButtonAction == CloseButtonActions.ExitApplication)
        {
            ExitApplication();
            return;
        }

        Hide();
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        await ShowSettingsAsync();
    }

    private async Task ShowSettingsAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        var dialog = new SettingsWindow(_viewModel.CreateSettingsSnapshot());
        var result = await ShowWithModalBackdropAsync(() => dialog.ShowDialog<AppSettings?>(this));
        if (result is not null)
        {
            await _viewModel.ApplySettingsAsync(result);
            RegisterShowWindowHotkey();
        }
    }

    private void RegisterShowWindowHotkey()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!_hotkeyService.Register(_viewModel.ShowWindowHotkey))
        {
            _viewModel.StatusText = $"快捷键 {_viewModel.ShowWindowHotkey} 注册失败，可能已被其他程序占用";
        }
    }

    private async void HistoryList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.CopySelectedItemAsync();
        }
    }

    private async void ImageThumbnail_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ClipboardHistoryItem item }
            || !item.IsImage
            || string.IsNullOrWhiteSpace(item.ImagePath))
        {
            return;
        }

        e.Handled = true;
        var preview = new ImagePreviewWindow(item.ImagePath);
        await ShowWithModalBackdropAsync(() => preview.ShowDialog(this));
    }

    private async Task<T?> ShowWithModalBackdropAsync<T>(Func<Task<T?>> showDialog)
    {
        SetModalBackdropVisible(true);
        try
        {
            return await showDialog();
        }
        finally
        {
            SetModalBackdropVisible(false);
        }
    }

    private async Task ShowWithModalBackdropAsync(Func<Task> showDialog)
    {
        SetModalBackdropVisible(true);
        try
        {
            await showDialog();
        }
        finally
        {
            SetModalBackdropVisible(false);
        }
    }

    private void SetModalBackdropVisible(bool isVisible)
    {
        ModalScrim.IsVisible = isVisible;
        Surface.Opacity = isVisible ? 0.68 : 1;
    }

    private void HistoryItemCard_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        card.BorderBrush = Brush.Parse("#1769E0");
        card.Background = Brush.Parse("#FBFDFF");
        card.BoxShadow = new BoxShadows(new BoxShadow
        {
            Blur = 18,
            OffsetY = 8,
            Color = Color.Parse("#1769E026")
        });
        SetItemActionOpacity(card, 1);
    }

    private void HistoryItemCard_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        card.BorderBrush = Brush.Parse("#DDE6F1");
        card.Background = Brushes.White;
        card.BoxShadow = default;
        SetItemActionOpacity(card, 0);
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

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.GetPosition(this).Y <= TopDragHeight)
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

    private static void SetItemActionOpacity(Visual visual, double opacity)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Button button && button.Classes.Contains("itemAction"))
            {
                button.Opacity = opacity;
            }

            SetItemActionOpacity(child, opacity);
        }
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (_viewModel?.CloseButtonAction == CloseButtonActions.ExitApplication)
        {
            _allowClose = true;
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void SetAppImages()
    {
        Icon = LoadWindowIcon();
        LogoImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://ClipboardKeeper/Assets/app.png")));
    }

    private TrayIcon CreateTrayIcon()
    {
        var showItem = new NativeMenuItem("显示窗口");
        showItem.Click += (_, _) => ShowFromTray();

        var settingsItem = new NativeMenuItem("设置");
        settingsItem.Click += async (_, _) =>
        {
            ShowFromTray();
            await ShowSettingsAsync();
        };

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        var trayIcon = new TrayIcon
        {
            Icon = LoadWindowIcon(),
            ToolTipText = "ClipboardKeeper",
            Menu = menu,
            IsVisible = true
        };
        trayIcon.Clicked += (_, _) => ShowFromTray();
        return trayIcon;
    }

    private static WindowIcon LoadWindowIcon()
    {
        return new WindowIcon(AssetLoader.Open(new Uri("avares://ClipboardKeeper/Assets/app.ico")));
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private static bool ShouldStartMinimized()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (string.Equals(arg, StartupService.StartupMinimizedArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
