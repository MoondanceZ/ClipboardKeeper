using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClipboardKeeper;

public sealed class ClipboardHistoryStore
{
    public const int DefaultMaxItems = 500;
    public const int MaxTextLength = 100_000;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public ClipboardHistoryStore(AppSettings settings)
    {
        ApplySettings(settings);
    }

    public string HistoryPath { get; private set; } = Path.Combine(AppSettings.DefaultStorageDirectory, "history.json");

    public int MaxItems { get; private set; } = DefaultMaxItems;

    public void ApplySettings(AppSettings settings)
    {
        var normalized = AppSettingsService.Normalize(settings);
        HistoryPath = Path.Combine(normalized.StorageDirectory, "history.json");
        MaxItems = normalized.MaxHistoryItems;
    }

    public async Task<List<ClipboardHistoryItem>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return [];
            }

            await using var stream = File.OpenRead(HistoryPath);
            var items = await JsonSerializer.DeserializeAsync(stream, ClipboardHistoryJsonContext.Default.ListClipboardHistoryItem);
            return Normalize(items ?? [], MaxItems);
        }
        catch
        {
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ClipboardHistoryItem> items)
    {
        await _gate.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var normalized = Normalize(items, MaxItems);
            await using var stream = File.Create(HistoryPath);
            await JsonSerializer.SerializeAsync(stream, normalized, ClipboardHistoryJsonContext.Default.ListClipboardHistoryItem);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static ClipboardHistoryItem CreateItem(string text)
    {
        var now = DateTimeOffset.Now;
        return new ClipboardHistoryItem
        {
            Text = text,
            Preview = CreatePreview(text),
            CreatedAt = now,
            LastSeenAt = now,
            ContentHash = CreateHash(text)
        };
    }

    public static ClipboardHistoryItem CreateImageItem(string imagePath, string contentHash, long byteLength)
    {
        var now = DateTimeOffset.Now;
        var fileName = Path.GetFileName(imagePath);
        var item = new ClipboardHistoryItem
        {
            Text = imagePath,
            Preview = fileName,
            CreatedAt = now,
            LastSeenAt = now,
            ContentHash = contentHash,
            ContentKind = "Image"
        };
        item.SetImagePath(imagePath);
        return item;
    }

    public static bool IsAcceptedText(string? text)
        => !string.IsNullOrWhiteSpace(text) && text.Length <= MaxTextLength;

    public static string CreateHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static string CreatePreview(string text)
    {
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 140 ? normalized : normalized[..140] + "...";
    }

    private static List<ClipboardHistoryItem> Normalize(IReadOnlyCollection<ClipboardHistoryItem> items, int maxItems)
    {
        foreach (var item in items.Where(item => item.IsImage && item.Preview.StartsWith("图片 · ", StringComparison.Ordinal)))
        {
            item.Preview = item.Preview["图片 · ".Length..];
        }

        var deduped = items
            .GroupBy(item => item.ContentHash)
            .Select(group => group.OrderByDescending(item => item.LastSeenAt).First())
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastSeenAt)
            .ToList();

        var pinned = deduped.Where(item => item.IsPinned).ToList();
        var unpinnedCapacity = Math.Max(0, maxItems - pinned.Count);
        return pinned
            .Concat(deduped.Where(item => !item.IsPinned).Take(unpinnedCapacity))
            .ToList();
    }
}
