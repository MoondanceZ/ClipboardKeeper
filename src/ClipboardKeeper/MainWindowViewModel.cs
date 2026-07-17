using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipboardKeeper;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly IClipboard _clipboard;
    private readonly IStorageProvider? _storageProvider;
    private readonly ClipboardMonitorService _monitor;
    private readonly ObservableCollection<ClipboardHistoryItem> _allItems = [];
    private ClipboardHistoryStore _store = new(new AppSettings());
    private AppSettings _settings = new();

    [ObservableProperty]
    private ObservableCollection<ClipboardHistoryItem> _items = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClipboardHistoryItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = "正在监听系统剪切板";

    [ObservableProperty]
    private int _maxHistoryItems = ClipboardHistoryStore.DefaultMaxItems;

    public MainWindowViewModel(IClipboard clipboard, IStorageProvider? storageProvider = null)
    {
        _clipboard = clipboard;
        _storageProvider = storageProvider;
        _monitor = new ClipboardMonitorService(clipboard, () => _settings.SaveImages);
        _monitor.TextCaptured += async (_, text) => await CaptureTextAsync(text);
        _monitor.ImageCaptured += async (_, image) => await CaptureImageAsync(image);
    }

    public bool HasSelection => SelectedItem is not null;

    public int TotalCount => _allItems.Count;

    public string StoragePath => _store.HistoryPath;

    public string CloseButtonAction => _settings.CloseButtonAction;

    public string ShowWindowHotkey => _settings.ShowWindowHotkey;

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        _settings.StartupEnabled = _startupService.IsEnabled();
        if (_settings.StartupEnabled)
        {
            _settings.StartupEnabled = _startupService.SetEnabled(enabled: true);
        }

        MaxHistoryItems = _settings.MaxHistoryItems;
        _store = new ClipboardHistoryStore(_settings);
        OnPropertyChanged(nameof(StoragePath));

        var loaded = await _store.LoadAsync();
        _allItems.Clear();
        foreach (var item in loaded)
        {
            _allItems.Add(item);
        }

        RefreshFilter();
        SelectedItem = Items.FirstOrDefault();
        _monitor.Start();
        StatusText = $"已载入 {_allItems.Count} 条历史，正在监听系统剪切板";
    }

    public void Stop()
    {
        _monitor.Stop();
    }

    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        await CopyItemAsync(SelectedItem);
    }

    public async Task CopySelectedItemAsync()
    {
        await CopySelectedAsync();
    }

    public AppSettings CreateSettingsSnapshot()
        => new()
        {
            StorageDirectory = _settings.StorageDirectory,
            MaxHistoryItems = _settings.MaxHistoryItems,
            SaveImages = _settings.SaveImages,
            StartupEnabled = _settings.StartupEnabled,
            CloseButtonAction = _settings.CloseButtonAction,
            ShowWindowHotkey = _settings.ShowWindowHotkey
        };

    public async Task ApplySettingsAsync(AppSettings settings)
    {
        var oldSettings = _settings;
        _settings = AppSettingsService.Normalize(settings);
        await StorageMigrationService.MigrateAsync(oldSettings.StorageDirectory, _settings.StorageDirectory, _allItems);
        _settings.StartupEnabled = _startupService.SetEnabled(_settings.StartupEnabled);
        MaxHistoryItems = _settings.MaxHistoryItems;
        _store.ApplySettings(_settings);
        TrimHistory();
        SortItems();
        await _settingsService.SaveAsync(_settings);
        await PersistAsync();
        OnPropertyChanged(nameof(StoragePath));
        OnPropertyChanged(nameof(CloseButtonAction));
        OnPropertyChanged(nameof(ShowWindowHotkey));
        StatusText = $"设置已保存，最多保留 {_settings.MaxHistoryItems} 条历史";
    }

    [RelayCommand]
    private async Task TogglePinAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        await TogglePinItemAsync(SelectedItem);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        await DeleteItemAsync(SelectedItem);
    }

    [RelayCommand]
    private async Task CopyItemAsync(ClipboardHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.IsImage)
        {
            if (!await CopyImageItemAsync(item))
            {
                return;
            }
        }
        else
        {
            await _clipboard.SetTextAsync(item.Text);
        }

        item.LastSeenAt = DateTimeOffset.Now;
        SelectedItem = item;
        SortItems();
        await PersistAsync();
        StatusText = item.IsImage ? "已复制图片回系统剪切板" : "已复制文本回系统剪切板";
    }

    private async Task<bool> CopyImageItemAsync(ClipboardHistoryItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
        {
            StatusText = "图片文件不存在，无法复制";
            return false;
        }

        using var bitmap = new Bitmap(item.ImagePath);
        if (_storageProvider is not null)
        {
            var storageFile = await _storageProvider.TryGetFileFromPathAsync(item.ImagePath);
            if (storageFile is not null)
            {
                var dataTransfer = new DataTransfer();
                var dataItem = new DataTransferItem();
                dataItem.SetBitmap(bitmap);
                dataItem.SetFile(storageFile);
                dataTransfer.Add(dataItem);
                await _clipboard.SetDataAsync(dataTransfer);
                _monitor.SuppressNextImageCapture();
                return true;
            }
        }

        await _clipboard.SetBitmapAsync(bitmap);
        _monitor.SuppressNextImageCapture();
        return true;
    }

    [RelayCommand]
    private async Task TogglePinItemAsync(ClipboardHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        var wasPinned = item.IsPinned;
        var nextPinnedItem = Items.SkipWhile(candidate => candidate != item).Skip(1).FirstOrDefault();
        item.IsPinned = !item.IsPinned;
        SelectedItem = wasPinned ? nextPinnedItem : item;
        SortItems();
        await PersistAsync();
        StatusText = item.IsPinned ? "已置顶当前条目" : "已取消置顶";
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ClipboardHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        var next = Items.SkipWhile(candidate => candidate != item).Skip(1).FirstOrDefault()
            ?? Items.FirstOrDefault(candidate => candidate != item);
        _allItems.Remove(item);
        RefreshFilter();
        SelectedItem = next ?? Items.FirstOrDefault();
        await PersistAsync();
        StatusText = "已删除当前条目";
    }

    [RelayCommand]
    private async Task ClearUnpinnedAsync()
    {
        var pinned = _allItems.Where(item => item.IsPinned).ToList();
        _allItems.Clear();
        foreach (var item in pinned)
        {
            _allItems.Add(item);
        }

        RefreshFilter();
        SelectedItem = Items.FirstOrDefault();
        await PersistAsync();
        StatusText = "已清空非置顶历史";
    }

    private async Task CaptureTextAsync(string text)
    {
        var hash = ClipboardHistoryStore.CreateHash(text);
        var existing = _allItems.FirstOrDefault(item => item.ContentHash == hash);
        if (existing is not null)
        {
            existing.LastSeenAt = DateTimeOffset.Now;
            StatusText = "重复内容已更新到最近位置";
        }
        else
        {
            _allItems.Add(ClipboardHistoryStore.CreateItem(text));
            StatusText = "已记录新的剪切板文本";
        }

        TrimHistory();
        SortItems();
        await PersistAsync();
    }

    private async Task CaptureImageAsync(ClipboardImageCapture image)
    {
        var existing = _allItems.FirstOrDefault(item => item.ContentHash == image.ContentHash);
        if (existing is not null)
        {
            existing.LastSeenAt = DateTimeOffset.Now;
            StatusText = "重复图片已更新到最近位置";
        }
        else
        {
            var imageDirectory = Path.Combine(_settings.StorageDirectory, "images");
            Directory.CreateDirectory(imageDirectory);
            var imagePath = Path.Combine(imageDirectory, $"{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.png");
            await File.WriteAllBytesAsync(imagePath, image.PngBytes);
            _allItems.Add(ClipboardHistoryStore.CreateImageItem(imagePath, image.ContentHash, image.PngBytes.LongLength));
            StatusText = "已记录新的剪切板图片";
        }

        TrimHistory();
        SortItems();
        await PersistAsync();
    }

    private void SortItems()
    {
        var selectedId = SelectedItem?.Id;
        var ordered = _allItems
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastSeenAt)
            .ToList();

        _allItems.Clear();
        foreach (var item in ordered)
        {
            _allItems.Add(item);
        }

        RefreshFilter();
        SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId) ?? Items.FirstOrDefault();
    }

    private void TrimHistory()
    {
        var excess = _allItems
            .Where(item => !item.IsPinned)
            .OrderBy(item => item.LastSeenAt)
            .Take(Math.Max(0, _allItems.Count - MaxHistoryItems))
            .ToList();

        foreach (var item in excess)
        {
            _allItems.Remove(item);
        }
    }

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems.ToList()
            : _allItems
                .Where(item => item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Items = new ObservableCollection<ClipboardHistoryItem>(filtered);
        OnPropertyChanged(nameof(TotalCount));
    }

    private Task PersistAsync() => _store.SaveAsync(_allItems);
}
