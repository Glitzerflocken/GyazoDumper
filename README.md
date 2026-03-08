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

**GyazoDumper** is a browser extension for Chrome and Edge that automatically downloads every [Gyazo](https://gyazo.com) image you open — no clicking required. Each image is only saved once, even if you revisit the page.

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

### Desktop App (Optional)

1. [Download GyazoDumper-Setup.exe](https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe)
2. Run the EXE — the setup wizard handles everything automatically
3. Restart the browser

---

## Uninstall

**Extension:** Remove via `chrome://extensions/`.

**Desktop App:**
```powershell
"%APPDATA%\GyazoDumper\GyazoDumper.exe" --uninstall
```
