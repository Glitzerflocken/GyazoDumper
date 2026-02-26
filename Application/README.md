# GyazoDumper - Desktop-App

Native Messaging Host fuer die GyazoDumper Browser-Extension. Hebt die Browser-Limitierung auf und erlaubt das Speichern von Gyazo-Bildern an einem **beliebigen Ort** auf der Festplatte.

## Features

- **Beliebiger Speicherort** - Speichert Bilder in einem frei waehlbaren Ordner
- **Windows Ordnerauswahl** - Nativer Datei-Dialog direkt aus der Extension heraus
- **Automatisch** - Bilder werden automatisch beim Oeffnen einer Gyazo-Seite gespeichert
- **Einfache Installation** - Setup per Doppelklick mit Extension-ID Eingabe

## Installation

1. Oeffne das GyazoDumper Extension-Popup
2. Aktiviere **Desktop-App verwenden**
3. Klicke auf **Setup herunterladen** oder lade die EXE von GitHub Releases
4. Starte `GyazoDumper-Setup.exe` per Doppelklick
5. Gib die im Popup angezeigte **Extension-ID** ein
6. Starte den Browser neu

Installiert nach `%APPDATA%\GyazoDumper\`:

| Datei | Beschreibung |
|---|---|
| `GyazoDumper.exe` | Native Messaging Host |
| `gyazodumper.nativeapp.json` | Native Messaging Manifest |
| `config.json` | Konfiguration (Speicherpfad, Dateinamen-Muster) |
| `Gespeicherte Bilder` | Ordnerverknuepfung zum Bilder-Ordner |

## Kommandozeile

| Befehl | Beschreibung |
|---|---|
| `GyazoDumper.exe` | Interaktiver Setup-Assistent |
| `GyazoDumper.exe --install [ID]` | Stille Installation |
| `GyazoDumper.exe --uninstall` | Deinstallation |

Wird automatisch von Chrome/Edge als Native Host gestartet wenn stdin umgeleitet ist.

## Bauen

```powershell
cd Application
dotnet publish -c Release
```
