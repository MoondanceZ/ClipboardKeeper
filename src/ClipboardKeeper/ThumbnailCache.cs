using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace ClipboardKeeper;

public static class ThumbnailCache
{
    private const int MaxCachedThumbnails = 48;
    private const int DecodeWidth = 160;
    private static readonly object Gate = new();
    private static readonly Dictionary<string, LinkedListNode<Entry>> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<Entry> Lru = [];

    public static Bitmap? Get(string imagePath)
    {
        lock (Gate)
        {
            if (Entries.TryGetValue(imagePath, out var existing))
            {
                Lru.Remove(existing);
                Lru.AddFirst(existing);
                return existing.Value.Bitmap;
            }
        }

        if (!File.Exists(imagePath))
        {
            return null;
        }

        Bitmap bitmap;
        try
        {
            using var stream = File.OpenRead(imagePath);
            bitmap = Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
        catch
        {
            return null;
        }

        lock (Gate)
        {
            if (Entries.TryGetValue(imagePath, out var existing))
            {
                bitmap.Dispose();
                Lru.Remove(existing);
                Lru.AddFirst(existing);
                return existing.Value.Bitmap;
            }

            var node = new LinkedListNode<Entry>(new Entry(imagePath, bitmap));
            Entries[imagePath] = node;
            Lru.AddFirst(node);

            while (Entries.Count > MaxCachedThumbnails && Lru.Last is not null)
            {
                var evicted = Lru.Last;
                Lru.RemoveLast();
                Entries.Remove(evicted.Value.Path);
                evicted.Value.Bitmap.Dispose();
            }

            return bitmap;
        }
    }

    private sealed record Entry(string Path, Bitmap Bitmap);
}
