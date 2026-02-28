<p align="center">
  <img src="GyazoDumper.png" alt="GyazoDumper Logo" width="128">
</p>

<h1 align="center">GyazoDumper</h1>

<p align="center">
  A browser extension that automatically downloads every Gyazo image you open — straight to your hard drive.
</p>

<p align="center">
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe">
    <img src="https://img.shields.io/badge/⬇_Desktop--App-7c4dff?style=for-the-badge&logoColor=white" alt="Download Desktop App" height="36">
  </a>
  &nbsp;
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/BrowserExtension.zip">
    <img src="https://img.shields.io/badge/⬇_Browser--Extension-4285F4?style=for-the-badge&logo=googlechrome&logoColor=white" alt="Download Browser Extension" height="36">
  </a>
</p>

<p align="center">
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest">
    <img src="https://img.shields.io/github/v/release/Glitzerflocken/GyazoDumper?style=flat-square&label=Version" alt="Latest Release">
  </a>
  <img src="https://img.shields.io/badge/Chrome_%7C_Edge-Manifest_V3-34a853?style=flat-square&logo=googlechrome&logoColor=white" alt="Manifest V3">
  <img src="https://img.shields.io/badge/.NET_10-Windows-512bd4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 10">
</p>

---

## What is GyazoDumper?

**GyazoDumper** is a browser extension for Chrome and Edge that detects when you open a [Gyazo](https://gyazo.com) image page and automatically downloads the image in the background — without you having to click anything.

### The Problem

Gyazo stores screenshots in the cloud. If you want to save an image locally, you have to manually "Right-click → Save as" every time. With many images, this quickly becomes tedious.

### The Solution

Install the extension, open a Gyazo page — done. The image is automatically saved to your hard drive. Each image is only downloaded once, even if you visit the page multiple times.

---

## Two Modes

| | **Browser Mode** | **Desktop App Mode** |
|---|---|---|
| **Save location** | Subfolder in the download directory | Any folder on the hard drive |
| **Requires** | Only the extension | Extension + Desktop App |
| **Setup** | Ready to use immediately | Run setup EXE once |

---

## Installation

### Browser Extension

1. [Download BrowserExtension.zip](https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/BrowserExtension.zip) and extract
2. Open `chrome://extensions/` (Chrome) or `edge://extensions/` (Edge)
3. Enable **Developer mode** (toggle in the top right)
4. Click **Load unpacked** → select the extracted folder
5. Done — the extension appears in the toolbar

> In **Browser Mode** everything works immediately. Images are saved in the download folder under a configurable subfolder.

📖 **Details:** [BrowserExtension/README.md](BrowserExtension/README.md)

### Desktop App (Optional)

> The Desktop App removes the browser limitation and allows saving to **any location** on the hard drive.

1. [Download GyazoDumper-Setup.exe](https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe)
2. Run the EXE by double-clicking
3. Enter the **Extension ID** shown in the extension popup
4. Restart the browser

📖 **Details:** [Application/README.md](Application/README.md)

---

## Technical Flow

```
Open Gyazo page
       │
       ▼
┌─────────────────────┐
│    Content Script    │  Detects gyazo.com/[32-hex-ID] in URL
│    (Content.js)      │  Reads the image URL from the DOM
└────────┬────────────┘
         │ chrome.runtime.sendMessage
         ▼
┌─────────────────────┐
│  Background Worker   │  Checks if the image was already downloaded
│  (background.js)     │  Saves the ID for duplicate detection
└────────┬────────────┘
         │
    ┌────┴────┐
    ▼         ▼
 Browser   Desktop App
  Mode       Mode
    │         │
    ▼         ▼
Downloads   Any
 Folder    Folder
```

---

## Uninstall

**Extension:** Remove via `chrome://extensions/`.

**Desktop App:**
```powershell
"%APPDATA%\GyazoDumper\GyazoDumper.exe" --uninstall
```
