using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace ClipboardKeeper;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow()
        : this(string.Empty)
    {
    }

    public ImagePreviewWindow(string imagePath)
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
        KeyDown += ImagePreviewWindow_KeyDown;

        if (File.Exists(imagePath))
        {
            TitleText.Text = Path.GetFileName(imagePath);
            PreviewImage.Source = new Bitmap(imagePath);
        }
        else
        {
            TitleText.Text = "图片不存在";
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ImagePreviewWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
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

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.GetPosition(this).Y <= 76)
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
