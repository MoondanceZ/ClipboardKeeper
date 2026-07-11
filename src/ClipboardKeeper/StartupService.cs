using System;
using Microsoft.Win32;

namespace ClipboardKeeper;

public sealed class StartupService
{
    public const string StartupMinimizedArgument = "--startup-minimized";
    private const string AppName = "ClipboardKeeper";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(AppName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public bool SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, BuildStartupCommand(), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }

        return IsEnabled();
    }

    private static string BuildStartupCommand()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "ClipboardKeeper.exe";
        }

        return $"\"{executable}\" {StartupMinimizedArgument}";
    }
}
