# GyazoDumper — Browser-Extension

Chrome/Edge Extension die automatisch Gyazo-Bilder herunterlädt.

## Funktionsweise

Wenn du eine Gyazo-Bildseite (`gyazo.com/[32-hex-ID]`) öffnest, erkennt die Extension das automatisch:

1. **Content Script** (`Content.js`) liest die Bild-URL aus dem DOM
2. **Background Worker** (`background.js`) prüft auf Duplikate und lädt das Bild herunter
3. Je nach Modus wird das Bild per `chrome.downloads` API oder über den Native Messaging Host gespeichert

### SPA-Navigation

Gyazo nutzt React mit `history.pushState` für die Pfeil-Navigation zwischen Bildern. Da Content Scripts in einer isolierten Welt laufen, kann `pushState` nicht direkt abgefangen werden. Stattdessen wird **URL-Polling** (alle 500ms) verwendet um Navigationen zuverlässig zu erkennen.

## Zwei Modi

| | **Browser-Modus** | **Desktop-App-Modus** |
|---|---|---|
| Speicherort | Unterordner im Download-Verzeichnis | Beliebiger Ordner auf der Festplatte |
| Benötigt | Nur die Extension | Extension + Desktop-App |
| Einrichtung | Sofort einsatzbereit | Setup-EXE einmalig starten |

## Installation

1. `chrome://extensions/` öffnen (Chrome) oder `edge://extensions/` (Edge)
2. **Entwicklermodus** aktivieren (Schalter oben rechts)
3. **Entpackte Erweiterung laden** → diesen Ordner auswählen
4. Fertig — die Extension erscheint in der Toolbar

## Dateistruktur

| Datei | Beschreibung |
|---|---|
| `manifest.json` | Extension-Konfiguration (Manifest V3) |
| `Content.js` | Content Script — läuft auf gyazo.com, liest Bild-URLs |
| `background.js` | Service Worker — Download-Logik, Native Messaging |
| `SettingsPopup.html` | Popup-UI für Einstellungen |
| `popup.js` | Popup-Logik |
| `popup.css` | Popup-Styles |
| `GyazoDumper.png` | Extension-Icon |

## Permissions

- `activeTab` — Zugriff auf die aktive Gyazo-Seite
- `downloads` — Bilder im Browser-Modus herunterladen
- `storage` — Einstellungen und heruntergeladene IDs speichern
- `nativeMessaging` — Kommunikation mit der Desktop-App
