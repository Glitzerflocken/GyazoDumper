# GyazoDumper Desktop App

Native Messaging Host für die GyazoDumper Chrome Extension.

## Features

- **Beliebiger Speicherort**: Speichert Gyazo-Bilder an einem frei wählbaren Ordner (nicht nur Downloads)
- **Automatisch**: Bilder werden automatisch beim Öffnen einer Gyazo-Seite gespeichert
- **Konfigurierbar**: Speicherpfad kann im Extension-Popup oder in der Konfigurationsdatei geändert werden

## Installation

### 1. Desktop-App herunterladen

Lade die neueste Version von [GitHub Releases](https://github.com/Glitzerflocken/GyazoDumper/releases) herunter.

### 2. Native Messaging Host registrieren

```powershell
GyazoDumper.exe --install
```

Dies erstellt:
- Das Native Messaging Manifest in `%APPDATA%\GyazoDumper\`
- Die Registry-Einträge für Chrome und Edge

### 3. Extension-ID eintragen

Nach der Installation musst du die Extension-ID in das Manifest eintragen:

1. Öffne `chrome://extensions/` in Chrome
2. Finde die GyazoDumper Extension und kopiere die **Extension-ID**
3. Öffne `%APPDATA%\GyazoDumper\com.gyazodumper.nativehost.json`
4. Trage die ID in `allowed_origins` ein:

```json
{
  "name": "com.gyazodumper.nativehost",
  "description": "GyazoDumper Native Messaging Host",
  "path": "C:\\Pfad\\zu\\GyazoDumper.exe",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://DEINE_EXTENSION_ID_HIER/"
  ]
}
```

### 4. Desktop-App Modus aktivieren

1. Öffne das GyazoDumper Extension-Popup
2. Aktiviere den Toggle "Desktop-App verwenden"
3. Setze den gewünschten Speicherpfad

## Deinstallation

```powershell
GyazoDumper.exe --uninstall
```

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

1. Prüfe ob `GyazoDumper.exe --install` ausgeführt wurde
2. Prüfe ob die Extension-ID im Manifest eingetragen ist
3. Starte Chrome neu

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
