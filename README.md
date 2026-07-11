# ClipboardKeeper

[English](README.md) | [中文](README.zh-CN.md)

ClipboardKeeper is a lightweight Windows clipboard history app built with Avalonia. It keeps local text and optional image clipboard history, supports search and pinning, and can live quietly in the system tray.

## Features

- Text clipboard history with duplicate detection.
- Optional clipboard image history with thumbnail display and image preview.
- Search across saved text history.
- Pin, copy, and delete history items from hover actions.
- Double-click an item to copy it back to the system clipboard.
- Tray icon with show, settings, and exit actions.
- Configurable close button behavior: minimize to tray or exit.
- Configurable global hotkey for showing the window, default `Alt+C`.
- Optional startup launch, with startup runs minimized to tray.
- Local JSON persistence for settings and clipboard history.
- Windows MSI packaging through WiX.

## Installation

Download the latest MSI from:

https://github.com/MoondanceZ/ClipboardKeeper/releases

Run `ClipboardKeeper-1.0.0-x64.msi` and follow the installer. The installer remembers the previous install directory when upgrading.

## Usage

Start ClipboardKeeper and copy text or images as usual. New clipboard content is captured automatically.

- Double-click a history item to copy it back.
- Hover an item to show copy, pin, and delete buttons.
- Click an image thumbnail to preview the full image.
- Use the tray icon to restore the window after minimizing.
- Open settings to change storage directory, history limit, image saving, startup behavior, close behavior, and hotkey.

## Data Storage

Settings are stored at:

```text
%LOCALAPPDATA%\ClipboardKeeper\settings.json
```

History is stored in the configured storage directory:

```text
<storage-directory>\history.json
<storage-directory>\images\
```

Changing the storage directory migrates existing `history.json` and saved images to the new location.

## Development

Requirements:

- Windows
- .NET 10 SDK
- WiX CLI for MSI packaging. The packaging script can install/use WiX `5.0.2`.

Build:

```powershell
dotnet build D:\Workspace\Test\ClipboardKeeper\ClipboardKeeper.slnx
```

Publish:

```powershell
dotnet publish D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\ClipboardKeeper.csproj -c Release -r win-x64
```

Build MSI:

```powershell
powershell -ExecutionPolicy Bypass -File D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\scripts\build-msi.ps1
```

The MSI is written to:

```text
D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\artifacts\ClipboardKeeper-1.0.0-x64.msi
```

## Project Layout

```text
ClipboardKeeper.slnx
src/
  ClipboardKeeper/
    App.axaml
    MainWindow.axaml
    SettingsWindow.axaml
    ImagePreviewWindow.axaml
    ClipboardHistoryItem.cs
    ClipboardHistoryStore.cs
    ClipboardMonitorService.cs
    AppSettingsService.cs
    StartupService.cs
    GlobalHotkeyService.cs
    Installer/
      Product.wxs
      License.rtf
    scripts/
      build-msi.ps1
```

## Technology

- Avalonia 12
- .NET 10, `net10.0-windows`
- Fluent theme
- CommunityToolkit.Mvvm
- Source-generated `System.Text.Json` serialization
- NativeAOT and single-file publish oriented configuration
- WiX MSI installer
