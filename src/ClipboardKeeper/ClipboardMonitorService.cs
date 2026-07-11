using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace ClipboardKeeper;

public sealed class ClipboardMonitorService
{
    private readonly IClipboard _clipboard;
    private readonly Func<bool> _shouldSaveImages;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(900);
    private string? _lastHash;
    private string? _lastImageHash;
    private CancellationTokenSource? _cts;

    public ClipboardMonitorService(IClipboard clipboard, Func<bool> shouldSaveImages)
    {
        _clipboard = clipboard;
        _shouldSaveImages = shouldSaveImages;
    }

    public event EventHandler<string>? TextCaptured;

    public event EventHandler<ClipboardImageCapture>? ImageCaptured;

    public bool IsRunning => _cts is not null;

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var text = await Dispatcher.UIThread.InvokeAsync(() => _clipboard.TryGetTextAsync());
                if (ClipboardHistoryStore.IsAcceptedText(text))
                {
                    var hash = ClipboardHistoryStore.CreateHash(text!);
                    if (!string.Equals(hash, _lastHash, StringComparison.Ordinal))
                    {
                        _lastHash = hash;
                        TextCaptured?.Invoke(this, text!);
                    }
                }

                if (_shouldSaveImages())
                {
                    var bitmap = await Dispatcher.UIThread.InvokeAsync(() => _clipboard.TryGetBitmapAsync());
                    if (bitmap is not null)
                    {
                        await using var stream = new MemoryStream();
                        bitmap.Save(stream);
                        var bytes = stream.ToArray();
                        var hash = Convert.ToHexString(SHA256.HashData(bytes));
                        if (!string.Equals(hash, _lastImageHash, StringComparison.Ordinal))
                        {
                            _lastImageHash = hash;
                            ImageCaptured?.Invoke(this, new ClipboardImageCapture(bytes, hash));
                        }
                    }
                }
            }
            catch
            {
                // Clipboard can be temporarily locked by another process; the next poll usually succeeds.
            }

            try
            {
                await Task.Delay(_interval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}

public sealed record ClipboardImageCapture(byte[] PngBytes, string ContentHash);
