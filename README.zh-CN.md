# ClipboardKeeper

[English](README.md) | [中文](README.zh-CN.md)

ClipboardKeeper 是一个基于 Avalonia 构建的轻量级 Windows 剪切板历史工具。它可以保存本地文本和可选的图片剪切板历史，支持搜索、置顶，并且可以安静地驻留在系统托盘。

## 功能特性

- 文本剪切板历史记录，支持重复内容检测。
- 可选保存剪切板图片，支持缩略图展示和大图预览。
- 支持搜索已保存的文本历史。
- 鼠标悬停条目时显示复制、置顶和删除按钮。
- 双击历史条目即可复制回系统剪切板。
- 系统托盘菜单支持显示窗口、打开设置和退出程序。
- 可配置右上角关闭按钮行为：最小化到托盘或退出程序。
- 可配置全局唤起快捷键，默认 `Alt+C`。
- 支持开机自启，开机启动时会自动最小化到托盘。
- 使用本地 JSON 保存设置和剪切板历史。
- 通过 WiX 提供 Windows MSI 安装包。

## 安装

从 Release 页面下载最新 MSI：

https://github.com/MoondanceZ/ClipboardKeeper/releases

运行 `ClipboardKeeper-1.0.0-x64.msi` 并按照安装向导操作。升级安装时，安装器会记住上一次的安装目录。

## 使用

启动 ClipboardKeeper 后，像平常一样复制文本或图片即可，新内容会自动记录。

- 双击历史条目：复制回系统剪切板。
- 鼠标悬停条目：显示复制、置顶和删除按钮。
- 点击图片缩略图：预览完整图片。
- 通过托盘图标：恢复最小化后的窗口。
- 打开设置：修改存储目录、保存条数、是否保存图片、开机自启、关闭行为和快捷键。

## 数据存储

设置文件存储在：

```text
%LOCALAPPDATA%\ClipboardKeeper\settings.json
```

历史记录存储在设置中配置的目录：

```text
<存储目录>\history.json
<存储目录>\images\
```

修改存储目录后，程序会自动迁移已有的 `history.json` 和已保存图片。

## 开发

环境要求：

- Windows
- .NET 10 SDK
- WiX CLI，用于 MSI 打包。打包脚本可以安装或使用 WiX `5.0.2`。

构建：

```powershell
dotnet build D:\Workspace\Test\ClipboardKeeper\ClipboardKeeper.slnx
```

发布：

```powershell
dotnet publish D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\ClipboardKeeper.csproj -c Release -r win-x64
```

构建 MSI：

```powershell
powershell -ExecutionPolicy Bypass -File D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\scripts\build-msi.ps1
```

MSI 输出位置：

```text
D:\Workspace\Test\ClipboardKeeper\src\ClipboardKeeper\artifacts\ClipboardKeeper-1.0.0-x64.msi
```

## 项目结构

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

## 技术栈

- Avalonia 12
- .NET 10，`net10.0-windows`
- FluentTheme
- CommunityToolkit.Mvvm
- `System.Text.Json` 源生成序列化
- 面向 NativeAOT 和单文件发布的配置
- WiX MSI 安装包
