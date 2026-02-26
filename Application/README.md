# GyazoDumper Desktop App

Native Messaging Host für die GyazoDumper Chrome Extension.

## Features

- **Beliebiger Speicherort**: Speichert Gyazo-Bilder an einem frei wählbaren Ordner (nicht nur Downloads)
- **Automatisch**: Bilder werden automatisch beim Öffnen einer Gyazo-Seite gespeichert
- **Konfigurierbar**: Speicherpfad kann im Extension-Popup oder in der Konfigurationsdatei geändert werden
- **Einfache Installation**: Setup per Doppelklick, Extension-ID wird direkt im Popup angezeigt

## Installation

### 1. Browser-Extension installieren

Lade die GyazoDumper Extension in Chrome oder Edge.

### 2. Desktop-App installieren

1. Öffne das GyazoDumper Extension-Popup
2. Aktiviere **"Desktop-App verwenden"**
3. Klicke auf **"Setup herunterladen"**
4. Starte die heruntergeladene `GyazoDumper-Setup.exe` per Doppelklick
5. Gib die im Popup angezeigte **Extension-ID** ein (Kopieren-Button nutzen)
6. Fertig! Starte den Browser neu.

Die Setup-Datei installiert alles automatisch nach `%APPDATA%\GyazoDumper\`:
- `GyazoDumper.exe` — Native Messaging Host
- `gyazodumper.nativeapp.json` — Native Messaging Manifest
- `config.json` — Konfiguration (Speicherpfad etc.)

### 3. Desktop-App Modus aktivieren

1. Öffne das GyazoDumper Extension-Popup
2. Der Status sollte jetzt **"Desktop-App verbunden"** anzeigen
3. Setze den gewünschten Speicherpfad

## Deinstallation

Starte die installierte EXE mit dem `--uninstall` Parameter:

```powershell
"%APPDATA%\GyazoDumper\GyazoDumper.exe" --uninstall
```

Dies entfernt:
- Alle Registry-Einträge
- Den gesamten `%APPDATA%\GyazoDumper\` Ordner

## Konfiguration

Die Konfiguration wird in `%APPDATA%\GyazoDumper\config.json` gespeichert:

```json
{
  "SaveDirectory": "C:\\Users\\Name\\Pictures\\Gyazo",
  "FileNamePattern": "Gyazo_{timestamp}_{hash}{ext}"
}
```

### Dateinamen-Platzhalter

- `{hash}` - Die Gyazo-ID (32 Zeichen)
- `{timestamp}` - Zeitstempel (Format: yyyy-MM-dd_HH-mm-ss)
- `{ext}` - Dateiendung (.png, .jpg, etc.)

## Fehlerbehebung

### "Desktop-App nicht gefunden"

1. Prüfe ob die Setup-Datei ausgeführt wurde
2. Prüfe ob die Extension-ID korrekt eingegeben wurde (Setup erneut starten zum Nachtragen)
3. Starte den Browser neu

### Bilder werden nicht gespeichert

1. Prüfe ob der Zielordner existiert und beschreibbar ist
2. Prüfe das Fehlerlog: `%APPDATA%\GyazoDumper\error.log`

## Entwicklung

### Bauen

```powershell
cd Application
dotnet publish -c Release
```

### Ausgabe

Die ausführbare Datei wird in `bin\Release\net10.0-windows\win-x64\publish\` erstellt.
