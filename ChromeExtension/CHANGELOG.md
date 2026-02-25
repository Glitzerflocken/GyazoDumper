# GyazoDumper Changelog

## Version 1.1.0

### Neue Features
- **Desktop-App Modus**: Optionaler Modus zum Speichern von Bildern an beliebigem Ort
- **Native Messaging Host**: C# Desktop-Anwendung für erweiterte Speicheroptionen
- **Toggle im Popup**: Einfaches Umschalten zwischen Browser- und Desktop-App-Modus
- **Status-Anzeige**: Zeigt an ob die Desktop-App verbunden ist
- **Automatische Installation**: Link zum GitHub Release für einfache Installation

### Änderungen
- `manifest.json`: `nativeMessaging` Permission hinzugefügt
- `background.js`: Unterstützung für Native Messaging hinzugefügt
- `SettingsPopup.html`: Desktop-App Toggle und Konfiguration
- `popup.js`: Desktop-App Modus Logik

## Version 1.0.0

### Features
- Automatisches Speichern von Gyazo-Bildern
- Konfigurierbarer Zielordner (Unterordner im Chrome Downloads)
- Doppel-Download-Verhinderung durch ID-Tracking
- Export der gespeicherten IDs als Textdatei
- Reset-Funktion für gespeicherte IDs

---

## Entwicklungsnotizen (Visual Studio)

Folgende Schritte wurden zur Erstellung dieses Projekts verwendet:
- Projektdatei (`ChromeExtension.esproj`) erstellen.
- Erstellen Sie `launch.json`, um das Debuggen zu aktivieren.
- NPM-Pakete installieren: `npm init && npm i --save-dev eslint`.
- Erstellen Sie `app.js`.
- Aktualisieren Sie de `package.json`-Einstiegspunkt.
- `eslint.config.js` erstellen, um Linting zu aktivieren.
- Projekt zur Projektmappe hinzufügen.
