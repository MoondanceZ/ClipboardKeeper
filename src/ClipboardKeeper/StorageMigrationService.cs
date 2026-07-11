using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ClipboardKeeper;

public static class StorageMigrationService
{
    public static async Task MigrateAsync(
        string oldDirectory,
        string newDirectory,
        IReadOnlyCollection<ClipboardHistoryItem> items)
    {
        oldDirectory = Environment.ExpandEnvironmentVariables(oldDirectory);
        newDirectory = Environment.ExpandEnvironmentVariables(newDirectory);

        if (string.Equals(
                Path.GetFullPath(oldDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(newDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(newDirectory);
        await CopyIfExistsAsync(Path.Combine(oldDirectory, "history.json"), Path.Combine(newDirectory, "history.json"));

        var oldImagesDirectory = Path.Combine(oldDirectory, "images");
        var newImagesDirectory = Path.Combine(newDirectory, "images");
        if (Directory.Exists(oldImagesDirectory))
        {
            Directory.CreateDirectory(newImagesDirectory);
            foreach (var imageFile in Directory.EnumerateFiles(oldImagesDirectory))
            {
                var target = Path.Combine(newImagesDirectory, Path.GetFileName(imageFile));
                await CopyIfExistsAsync(imageFile, target);
            }
        }

        foreach (var item in items)
        {
            if (!item.IsImage || string.IsNullOrWhiteSpace(item.ImagePath))
            {
                continue;
            }

            var fileName = Path.GetFileName(item.ImagePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var targetPath = Path.Combine(newImagesDirectory, fileName);
            if (File.Exists(targetPath))
            {
                item.SetImagePath(targetPath);
            }
        }
    }

    private static async Task CopyIfExistsAsync(string source, string target)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var sourceStream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var targetStream = File.Create(target);
        await sourceStream.CopyToAsync(targetStream);
    }
}
