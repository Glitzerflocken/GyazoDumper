<p align="center">
  <img src="GyazoDumper.png" alt="GyazoDumper Logo" width="128">
</p>

<h1 align="center">GyazoDumper</h1>

<p align="center">
  Browser-Extension die automatisch jedes Gyazo-Bild herunterlädt das du öffnest — direkt auf deine Festplatte.
</p>

<p align="center">
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe">
    <img src="https://img.shields.io/badge/⬇_Desktop--App-7c4dff?style=for-the-badge&logoColor=white" alt="Desktop-App herunterladen" height="36">
  </a>
  &nbsp;
  <a href="https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/BrowserExtension.zip">
    <img src="https://img.shields.io/badge/⬇_Browser--Extension-4285F4?style=for-the-badge&logo=googlechrome&logoColor=white" alt="Browser Extension herunterladen" height="36">
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

## Was ist GyazoDumper?

**GyazoDumper** ist eine Browser-Extension für Chrome und Edge die erkennt wenn du eine [Gyazo](https://gyazo.com)-Bildseite öffnest und das Bild automatisch im Hintergrund herunterlädt — ohne dass du irgendetwas klicken musst.

### Das Problem

Gyazo speichert Screenshots in der Cloud. Wenn du ein Bild lokal sichern willst, musst du jedes Mal manuell „Rechtsklick → Speichern unter" machen. Bei vielen Bildern wird das schnell lästig.

### Die Lösung

Installiere die Extension, öffne eine Gyazo-Seite — fertig. Das Bild landet automatisch auf deiner Festplatte. Jedes Bild wird nur einmal heruntergeladen, auch wenn du die Seite mehrfach besuchst.

---

## Zwei Modi

| | **Browser-Modus** | **Desktop-App-Modus** |
|---|---|---|
| **Speicherort** | Unterordner im Download-Verzeichnis | Beliebiger Ordner auf der Festplatte |
| **Benötigt** | Nur die Extension | Extension + Desktop-App |
| **Einrichtung** | Sofort einsatzbereit | Setup-EXE einmalig starten |

---

## Installation

### Browser-Extension

1. [BrowserExtension.zip herunterladen](https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/BrowserExtension.zip) und entpacken
2. `chrome://extensions/` öffnen (Chrome) oder `edge://extensions/` (Edge)
3. **Entwicklermodus** aktivieren (Schalter oben rechts)
4. **Entpackte Erweiterung laden** → den entpackten Ordner auswählen
5. Fertig — die Extension erscheint in der Toolbar

> Im **Browser-Modus** funktioniert alles sofort. Die Bilder werden im Download-Ordner unter einem konfigurierbaren Unterordner gespeichert.

📖 **Details:** [BrowserExtension/README.md](BrowserExtension/README.md)

### Desktop-App (Optional)

> Die Desktop-App hebt die Browser-Limitierung auf und erlaubt das Speichern an einem **beliebigen Ort** auf der Festplatte.

1. [GyazoDumper-Setup.exe herunterladen](https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe)
2. Starte die EXE per Doppelklick
3. Gib die im Extension-Popup angezeigte **Extension-ID** ein
4. Starte den Browser neu

📖 **Details:** [Application/README.md](Application/README.md)

---

## Technischer Ablauf

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

---

## Deinstallation

**Extension:** Über `chrome://extensions/` entfernen.

**Desktop-App:**
```powershell
"%APPDATA%\GyazoDumper\GyazoDumper.exe" --uninstall
```
