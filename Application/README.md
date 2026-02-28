# GyazoDumper - Desktop App

Native Messaging Host for the GyazoDumper browser extension. Removes the browser limitation and allows saving Gyazo images to **any location** on the hard drive.

## Features

- **Any save location** — Saves images to a freely chosen folder
- **Windows folder picker** — Native file dialog directly from the extension
- **Automatic** — Images are saved automatically when opening a Gyazo page
- **Easy installation** — Setup by double-click with Extension ID input

## Installation

1. Open the GyazoDumper extension popup
2. Enable **Use Desktop App**
3. Click **Download Setup** or download the EXE from GitHub Releases
4. Run `GyazoDumper-Setup.exe` by double-clicking
5. Enter the **Extension ID** shown in the popup
6. Restart the browser

Installs to `%APPDATA%\GyazoDumper\`:

| File | Description |
|---|---|
| `GyazoDumper.exe` | Native Messaging Host |
| `gyazodumper.nativeapp.json` | Native Messaging Manifest |
| `config.json` | Configuration (save path, filename pattern) |
| `Uninstall.bat` | Uninstall script |
| `Gespeicherte Bilder` | Folder shortcut to the images folder |

## Command Line

| Command | Description |
|---|---|
| `GyazoDumper.exe` | Interactive setup wizard |
| `GyazoDumper.exe --install [ID]` | Silent installation |
| `GyazoDumper.exe --uninstall` | Uninstall |

Automatically started by Chrome/Edge as a Native Host when stdin is redirected.

## Build

```powershell
cd Application
dotnet publish -c Release
```
