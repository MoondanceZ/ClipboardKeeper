using System;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipboardKeeper;

public sealed partial class ClipboardHistoryItem : ObservableObject
{
    [ObservableProperty]
    private bool _isPinned;

    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Text { get; init; }

    public required string Preview { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; set; }

    public required string ContentHash { get; init; }

    public string ContentKind { get; init; } = "Text";

    public string? ImagePath { get; set; }

    public int CharacterCount => Text.Length;

    public bool IsImage => string.Equals(ContentKind, "Image", StringComparison.OrdinalIgnoreCase);

    public bool IsText => !IsImage;

    public string DetailLabel => IsImage ? string.Empty : $"{CharacterCount} 字符";

    public bool HasDetailLabel => !string.IsNullOrWhiteSpace(DetailLabel);

    public string SecondaryLabel => IsImage ? Preview : DetailLabel;

    public bool HasSecondaryLabel => !string.IsNullOrWhiteSpace(SecondaryLabel);

    public string LastSeenLabel => LastSeenAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    public void SetImagePath(string imagePath)
    {
        ImagePath = imagePath;
    }

    [JsonIgnore]
    public Bitmap? ThumbnailImage
    {
        get
        {
            if (!IsImage || string.IsNullOrWhiteSpace(ImagePath))
            {
                return null;
            }

            return ThumbnailCache.Get(ImagePath);
        }
    }
}
