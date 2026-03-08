/**
 * GyazoDumper - Translations (i18n)
 *
 * All visible texts in the popup in German and English.
 * Language is stored in chrome.storage.local and switchable by the user.
 *
 * Usage in HTML:
 *   <span data-i18n="key">Fallback</span>
 *   <input data-i18n-placeholder="key">
 *
 * Usage in JS:
 *   t("key")
 */

const translations = {
    de: {
        // Header
        viewOnGithub: "auf GitHub ansehen",

        // Speichermodus
        saveMode: "Speichermodus",
        useDesktopApp: "Desktop-App verwenden",
        useDesktopAppDesc: "Speichert Bilder an beliebigem Ort (erfordert Installation)",

        // Desktop-App Status
        statusChecking: "Prüfe Verbindung...",
        statusConnected: "Desktop-App verbunden",
        statusNotFound: "Desktop-App nicht gefunden",
        statusError: "Verbindungsfehler",

        // Desktop-App Ordner
        targetFolderLabel: "Zielordner (vollständiger Pfad):",
        targetFolderPlaceholder: "Wird bei Installation festgelegt",
        btnBrowseFolder: "Ordner auswählen",
        btnOpenFolder: "Ordner öffnen",
        browsingFolder: "Bitte wählen...",

        // Installation
        installTitle: "Desktop-App installieren",
        installStep1: "Klicke auf <em>Setup herunterladen</em>",
        installStep2: "Starte die heruntergeladene Datei",
        installStep3: "Gib dort diese Extension-ID ein:",
        btnCopy: "Kopieren",
        btnCopied: "Kopiert!",
        btnDownloadSetup: "Setup herunterladen",

        // Browser-Modus
        browserLimitation: "Wegen Browserlimitierungen ist das automatische Speichern von Dateien aus dem Netz nur im festgelegten Download Ordner möglich.<br><br>Mit der Installation der Desktop-App kann diese Limitierung umgangen werden und ein beliebiger Ort auf der Festplatte gewählt werden.",
        folderName: "Ordnername",
        folderPlaceholder: "z.B. GyazoDumps",
        btnSave: "Speichern",

        // IDs
        exportTitle: "IDs in Datei exportieren",
        btnExport: "IDs exportieren",
        idCount: "{count} IDs gespeichert",
        resetTitle: "Gespeicherte IDs zurücksetzen",
        btnReset: "Alle IDs löschen",

        // Bestätigungsdialog
        confirmReset: "Alle gespeicherten IDs wirklich löschen?<br>Dies kann nicht rückgängig gemacht werden!",
        btnConfirmYes: "Ja, löschen",
        btnConfirmNo: "Abbrechen",

        // Statusmeldungen
        statusSaved: "Gespeichert!",
        statusEmptyPath: "Bitte einen Pfad eingeben.",
        statusSaveError: "Fehler beim Speichern!",
        statusDesktopOn: "Desktop-App Modus aktiviert",
        statusDesktopOff: "Browser-Modus aktiviert",
        statusToggleError: "Fehler beim Umschalten!",
        statusDownloadFail: "Download fehlgeschlagen!",
        statusDownloading: "Setup wird heruntergeladen...",
        statusNewPath: "Neuer Zielpfad gespeichert: ",
        statusBrowseFail: "Ordnerauswahl fehlgeschlagen.",
        statusBrowseError: "Fehler bei Ordnerauswahl.",
        statusOpenFail: "Ordner konnte nicht geöffnet werden.",
        statusOpenError: "Fehler beim Öffnen.",
        statusNoIds: "Keine IDs zum Exportieren vorhanden.",
        statusExportFail: "Export fehlgeschlagen!",
        statusExported: "{count} IDs exportiert!",
        statusResetDone: "Alle IDs gelöscht!",
        statusResetError: "Fehler beim Löschen!",
        statusIdLoadError: "Fehler beim Laden",

        // Sprache
        language: "Sprache",
    },

    en: {
        // Header
        viewOnGithub: "view on GitHub",

        // Save mode
        saveMode: "Save Mode",
        useDesktopApp: "Use Desktop App",
        useDesktopAppDesc: "Saves images to any location (requires installation)",

        // Desktop app status
        statusChecking: "Checking connection...",
        statusConnected: "Desktop app connected",
        statusNotFound: "Desktop app not found",
        statusError: "Connection error",

        // Desktop app folder
        targetFolderLabel: "Target folder (full path):",
        targetFolderPlaceholder: "Set during installation",
        btnBrowseFolder: "Browse folder",
        btnOpenFolder: "Open folder",
        browsingFolder: "Please select...",

        // Installation
        installTitle: "Install Desktop App",
        installStep1: "Click <em>Download Setup</em>",
        installStep2: "Run the downloaded file",
        installStep3: "Enter this Extension ID there:",
        btnCopy: "Copy",
        btnCopied: "Copied!",
        btnDownloadSetup: "Download Setup",

        // Browser mode
        browserLimitation: "Due to browser limitations, automatic file saving from the web is only possible in the designated download folder.<br><br>By installing the Desktop App, this limitation can be bypassed and any location on your hard drive can be chosen.",
        folderName: "Folder Name",
        folderPlaceholder: "e.g. GyazoDumps",
        btnSave: "Save",

        // IDs
        exportTitle: "Dump saved IDs to File",
        btnExport: "Export IDs",
        idCount: "{count} IDs saved",
        resetTitle: "Reset saved IDs",
        btnReset: "Delete all IDs",

        // Confirmation dialog
        confirmReset: "Really delete all saved IDs?<br>This cannot be undone!",
        btnConfirmYes: "Yes, delete",
        btnConfirmNo: "Cancel",

        // Status messages
        statusSaved: "Saved!",
        statusEmptyPath: "Please enter a path.",
        statusSaveError: "Error saving!",
        statusDesktopOn: "Desktop App mode enabled",
        statusDesktopOff: "Browser mode enabled",
        statusToggleError: "Error switching mode!",
        statusDownloadFail: "Download failed!",
        statusDownloading: "Downloading setup...",
        statusNewPath: "New target path saved: ",
        statusBrowseFail: "Folder selection failed.",
        statusBrowseError: "Error selecting folder.",
        statusOpenFail: "Could not open folder.",
        statusOpenError: "Error opening folder.",
        statusNoIds: "No IDs to export.",
        statusExportFail: "Export failed!",
        statusExported: "{count} IDs exported!",
        statusResetDone: "All IDs deleted!",
        statusResetError: "Error deleting IDs!",
        statusIdLoadError: "Error loading",

        // Language
        language: "Language",
    }
};

// Current language (default: English)
let currentLang = "en";

/**
 * Returns the translated text for a key.
 * Supports {count} placeholders.
 */
function t(key, replacements = {}) {
    let text = translations[currentLang]?.[key] || translations["en"]?.[key] || key;
    for (const [placeholder, value] of Object.entries(replacements)) {
        text = text.replace(`{${placeholder}}`, value);
    }
    return text;
}

/**
 * Applies all translations to the DOM.
 * Searches for data-i18n (innerHTML) and data-i18n-placeholder attributes.
 */
function applyTranslations() {
    document.querySelectorAll("[data-i18n]").forEach(el => {
        const key = el.getAttribute("data-i18n");
        const text = t(key);
        if (el.tagName === "INPUT" || el.tagName === "TEXTAREA") {
            el.value = text;
        } else {
            el.innerHTML = text;
        }
    });

    document.querySelectorAll("[data-i18n-placeholder]").forEach(el => {
        el.placeholder = t(el.getAttribute("data-i18n-placeholder"));
    });
}

/**
 * Sets the language and saves it.
 */
async function setLanguage(lang) {
    currentLang = lang;
    applyTranslations();
    try {
        await chrome.storage.local.set({ language: lang });
    } catch {}
}

/**
 * Loads the saved language from storage.
 */
async function loadLanguage() {
    try {
        const result = await chrome.storage.local.get("language");
        if (result.language && translations[result.language]) {
            currentLang = result.language;
        }
    } catch {}
    applyTranslations();
}
