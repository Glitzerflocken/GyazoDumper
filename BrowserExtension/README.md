# GyazoDumper — Browser Extension

Chrome/Edge extension that automatically downloads Gyazo images.

## How It Works

When you open a Gyazo image page (`gyazo.com/[32-hex-ID]`), the extension detects it automatically:

1. **Content Script** (`Content.js`) reads the image URL from the DOM
2. **Background Worker** (`background.js`) checks for duplicates and downloads the image
3. Depending on the mode, the image is saved via the `chrome.downloads` API or through the Native Messaging Host

### SPA Navigation

Gyazo uses React with `history.pushState` for arrow navigation between images. Since Content Scripts run in an isolated world, `pushState` cannot be intercepted directly. Instead, **URL polling** (every 500ms) is used to reliably detect navigations.

## Two Modes

| | **Browser Mode** | **Desktop App Mode** |
|---|---|---|
| Save location | Subfolder in the download directory | Any folder on the hard drive |
| Requires | Only the extension | Extension + Desktop App |
| Setup | Ready to use immediately | Run setup EXE once |

## Installation

1. Open `chrome://extensions/` (Chrome) or `edge://extensions/` (Edge)
2. Enable **Developer mode** (toggle in the top right)
3. Click **Load unpacked** → select this folder
4. Done — the extension appears in the toolbar

## File Structure

| File | Description |
|---|---|
| `manifest.json` | Extension configuration (Manifest V3) |
| `Content.js` | Content Script — runs on gyazo.com, reads image URLs |
| `background.js` | Service Worker — download logic, Native Messaging |
| `SettingsPopup.html` | Popup UI for settings |
| `popup.js` | Popup logic |
| `popup.css` | Popup styles |
| `translations.js` | i18n — German/English translations |
| `GyazoDumper.png` | Extension icon |

## Permissions

- `activeTab` — Access to the active Gyazo page
- `downloads` — Download images in browser mode
- `storage` — Save settings and downloaded IDs
- `nativeMessaging` — Communication with the Desktop App
