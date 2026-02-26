<p align="center">
  <img src="GyazoDumper.png" alt="GyazoDumper Logo" width="128">
</p>

<h1 align="center">GyazoDumper</h1>

<p align="center">
  Browser-Extension die automatisch jedes Gyazo-Bild herunterlädt das du öffnest — direkt auf deine Festplatte.
</p>

<p align="center">
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe">
    <img src="https://img.shields.io/badge/⬇_Desktop--App_herunterladen-7c4dff?style=for-the-badge&logoColor=white" alt="Desktop-App herunterladen" height="40">
  </a>
</p>

<p align="center">
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest">
    <img src="https://img.shields.io/github/v/release/Glitzerflocken/GyazoDumper?style=flat-square&label=Version" alt="Latest Release">
  </a>
  <img src="https://img.shields.io/badge/Chrome-Extension-4285F4?style=flat-square&logo=googlechrome&logoColor=white" alt="Chrome Extension">
  <img src="https://img.shields.io/badge/Edge-Extension-0078D7?style=flat-square&logo=microsoftedge&logoColor=white" alt="Edge Extension">
</p>

---

## Was ist GyazoDumper?

**GyazoDumper** ist eine Browser-Extension für Chrome und Edge die erkennt wenn du eine [Gyazo](https://gyazo.com)-Bildseite öffnest und das Bild automatisch im Hintergrund herunterlädt — ohne dass du irgendetwas klicken musst.

### Das Problem

Gyazo speichert Screenshots in der Cloud. Wenn du ein Bild lokal sichern willst, musst du jedes Mal manuell „Rechtsklick → Speichern unter" machen. Bei vielen Bildern wird das schnell lästig.

### Die Lösung

Installiere die Extension, öffne eine Gyazo-Seite — fertig. Das Bild landet automatisch auf deiner Festplatte. Jedes Bild wird nur einmal heruntergeladen, auch wenn du die Seite mehrfach besuchst.

---

## So funktioniert es

### Zwei Modi

| | **Browser-Modus** | **Desktop-App-Modus** |
|---|---|---|
| **Speicherort** | Unterordner im Download-Verzeichnis | Beliebiger Ordner auf der Festplatte |
| **Benötigt** | Nur die Extension | Extension + Desktop-App |
| **Einrichtung** | Sofort einsatzbereit | Setup-EXE einmalig starten |

### Technischer Ablauf

```
Gyazo-Seite öffnen
       │
       ▼
┌─────────────────────┐
│    Content Script    │  Erkennt gyazo.com/[32-hex-ID] im URL
│    (Content.js)      │  Liest die Bild-URL aus dem DOM
└────────┬────────────┘
         │ chrome.runtime.sendMessage
         ▼
┌─────────────────────┐
│  Background Worker   │  Prüft ob das Bild schon heruntergeladen wurde
│  (background.js)     │  Speichert die ID zur Duplikat-Erkennung
└────────┬────────────┘
         │
    ┌────┴────┐
    ▼         ▼
Browser    Desktop-App
 Modus       Modus
    │         │
    ▼         ▼
Downloads  Beliebiger
 Ordner      Ordner
```

1. **Content Script** (`Content.js`) — Läuft auf jeder `gyazo.com`-Seite. Prüft ob die URL das Muster `gyazo.com/[32-stellige Hex-ID]` hat. Findet das Bild-Element im DOM (`img.image-viewer`) und schickt die URL an den Background Worker. Falls das Bild noch nicht geladen ist, wartet ein `MutationObserver` bis es erscheint.

2. **Background Service Worker** (`background.js`) — Empfängt die Bild-URL und prüft anhand der gespeicherten IDs ob das Bild bereits heruntergeladen wurde (Duplikat-Erkennung). Je nach Modus wird das Bild entweder über die `chrome.downloads` API (Browser-Modus) oder über den Native Messaging Host (Desktop-App-Modus) gespeichert.

3. **Popup** (`SettingsPopup.html`) — Einstellungen: Modus wählen, Ordnernamen festlegen, Desktop-App installieren, gespeicherte IDs exportieren oder zurücksetzen.

### Duplikat-Erkennung

Jede heruntergeladene Gyazo-ID wird in `chrome.storage.local` gespeichert. Beim nächsten Besuch der gleichen Seite wird der Download übersprungen. Die ID-Liste kann über das Popup als Textdatei exportiert oder zurückgesetzt werden.

---

## Installation

### Browser-Extension

1. Repository klonen oder als ZIP herunterladen
2. `chrome://extensions/` öffnen (Chrome) oder `edge://extensions/` (Edge)
3. **Entwicklermodus** aktivieren (Schalter oben rechts)
4. **Entpackte Erweiterung laden** → den `ChromeExtension`-Ordner auswählen
5. Fertig — die Extension erscheint in der Toolbar

> Im **Browser-Modus** funktioniert alles sofort. Die Bilder werden im Download-Ordner unter einem konfigurierbaren Unterordner gespeichert (Standard: `GyazooDumper`).

---

## Desktop-App (Optionaler Modus)

> Die Desktop-App hebt die Browser-Limitierung auf und erlaubt das Speichern an einem **beliebigen Ort** auf der Festplatte.

<p>
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe">
    <img src="https://img.shields.io/badge/⬇_GyazoDumper--Setup.exe_herunterladen-7c4dff?style=for-the-badge" alt="Download" height="32">
  </a>
</p>

### Features

- **Beliebiger Speicherort** — Speichert Gyazo-Bilder in einem frei wählbaren Ordner (nicht nur Downloads)
- **Automatisch** — Bilder werden automatisch beim Öffnen einer Gyazo-Seite gespeichert
- **Konfigurierbar** — Speicherpfad kann im Extension-Popup oder in der Konfigurationsdatei geändert werden
- **Einfache Installation** — Setup per Doppelklick, Extension-ID wird direkt im Popup angezeigt

### Installation

1. Öffne das GyazoDumper Extension-Popup
2. Aktiviere **„Desktop-App verwenden"**
3. Klicke auf **„Setup herunterladen"** (oder nutze den Button oben)
4. Starte die heruntergeladene `GyazoDumper-Setup.exe` per Doppelklick
5. Gib die im Popup angezeigte **Extension-ID** ein (Kopieren-Button nutzen)
6. Starte den Browser neu — fertig!

Die Setup-Datei installiert alles automatisch nach `%APPDATA%\GyazoDumper\`:

| Datei | Beschreibung |
|---|---|
| `GyazoDumper.exe` | Native Messaging Host |
| `gyazodumper.nativeApp.json` | Native Messaging Manifest |
| `config.json` | Konfiguration (Speicherpfad etc.) |

### Desktop-App Modus aktivieren

1. Öffne das GyazoDumper Extension-Popup
2. Der Status sollte jetzt **„Desktop-App verbunden"** (grüner Punkt) anzeigen
3. Setze den gewünschten Speicherpfad

### Konfiguration

Die Konfiguration wird in `%APPDATA%\GyazoDumper\config.json` gespeichert:

```json
{
  "SaveDirectory": "C:\\Users\\Name\\Pictures\\Gyazo",
  "FileNamePattern": "Gyazo_{timestamp}_{hash}{ext}"
}
```

#### Dateinamen-Platzhalter

| Platzhalter | Beschreibung | Beispiel |
|---|---|---|
| `{hash}` | Gyazo-ID (32 Zeichen) | `a1b2c3d4e5f6...` |
| `{timestamp}` | Zeitstempel | `2025-01-15_14-30-00` |
| `{ext}` | Dateiendung | `.png`, `.jpg` |

### Deinstallation

```powershell
"%APPDATA%\GyazoDumper\GyazoDumper.exe" --uninstall
```

Dies entfernt alle Registry-Einträge und den gesamten `%APPDATA%\GyazoDumper\` Ordner.

---

## Fehlerbehebung

### „Desktop-App nicht gefunden"

1. Prüfe ob die Setup-Datei ausgeführt wurde
2. Prüfe ob die Extension-ID korrekt eingegeben wurde (Setup erneut starten zum Nachtragen)
3. Starte den Browser neu

### Bilder werden nicht gespeichert

1. Prüfe ob der Zielordner existiert und beschreibbar ist
2. Prüfe das Fehlerlog: `%APPDATA%\GyazoDumper\error.log`

### Bilder werden doppelt heruntergeladen

Die ID-Liste ist möglicherweise zurückgesetzt worden. Bereits vorhandene Dateien werden durch `conflictAction: uniquify` nicht überschrieben.

---

## Entwicklung

### Projektstruktur

```
GyazoDumper/
├── ChromeExtension/          Browser-Extension (Manifest V3)
│   ├── manifest.json         Extension-Konfiguration
│   ├── Content.js            Content Script (Gyazo-Seiten)
│   ├── background.js         Background Service Worker
│   ├── SettingsPopup.html    Popup-UI
│   ├── popup.js              Popup-Logik
│   ├── popup.css             Popup-Styles
│   └── GyazoDumper.png       Extension-Icon
│
├── Application/              Desktop-App (.NET)
│   ├── Program.cs            Einstiegspunkt (Setup / Native Host)
│   └── Services/
│       ├── NativeMessagingHost.cs   Chrome Native Messaging Protokoll
│       ├── NativeHostInstaller.cs   Setup-Installer
│       ├── ConfigurationService.cs  Konfigurationsverwaltung
│       └── ImageDownloader.cs       Bild-Download
│
└── GyazoDumper.png           Projekt-Icon
```

### Desktop-App bauen

```powershell
cd Application
dotnet publish -c Release
```

Die ausführbare Datei wird in `bin\Release\net10.0-windows\win-x64\publish\` erstellt.

### Technologien

- **Browser-Extension**: JavaScript, Chrome Extension Manifest V3, Chrome APIs (`downloads`, `storage`, `nativeMessaging`)
- **Desktop-App**: C#, .NET 10, Windows Registry, Chrome Native Messaging Protocol
