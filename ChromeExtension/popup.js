/**
 * GyazoDumper - Popup Script
 * 
 * Verwaltet die Einstellungen im Popup-Menue der Extension.
 *   - Zielordner laden/speichern (Browser-Modus)
 *   - Desktop-App Modus aktivieren/deaktivieren
 *   - Desktop-App Status pruefen und Installation anbieten
 *   - Extension-ID anzeigen und kopieren (fuer Setup)
 *   - Setup-Download direkt aus dem Popup starten
 *   - Gespeicherte IDs als Textdatei exportieren (saveAs-Dialog)
 *   - Alle gespeicherten IDs loeschen (mit Bestaetigungsdialog)
 */

// Standard-Zielordner (Unterordner im Chrome-Download-Verzeichnis)
const DEFAULT_ZIELORDNER = "GyazooDumper";
const DEFAULT_DESKTOP_ZIELORDNER = "";

// GitHub Release URL fuer automatischen Download der Setup-Datei
const SETUP_DOWNLOAD_URL = "https://github.com/Glitzerflocken/GyazoDumper/releases/latest/download/GyazoDumper-Setup.exe";

// UI-Elemente
const inputZielordner = document.getElementById("inputZielordner");
const btnSave = document.getElementById("btnSave");
const statusEl = document.getElementById("status");
const idCountEl = document.getElementById("idCount");
const btnExport = document.getElementById("btnExport");
const btnReset = document.getElementById("btnReset");
const confirmOverlay = document.getElementById("confirmOverlay");
const btnConfirmYes = document.getElementById("btnConfirmYes");
const btnConfirmNo = document.getElementById("btnConfirmNo");

// Desktop-App UI-Elemente
const toggleDesktopApp = document.getElementById("toggleDesktopApp");
const desktopAppSection = document.getElementById("desktopAppSection");
const browserZielordnerSection = document.getElementById("browserZielordnerSection");
const appStatusIndicator = document.getElementById("appStatusIndicator");
const appStatusText = document.getElementById("appStatusText");
const installSection = document.getElementById("installSection");
const btnInstallApp = document.getElementById("btnInstallApp");
const btnCopyId = document.getElementById("btnCopyId");
const extensionIdEl = document.getElementById("extensionId");
const inputDesktopZielordner = document.getElementById("inputDesktopZielordner");
const btnSaveDesktopPath = document.getElementById("btnSaveDesktopPath");

// ============================================================================
//  Extension-ID anzeigen
// ============================================================================

extensionIdEl.textContent = chrome.runtime.id;

// ============================================================================
//  Zielordner (Browser-Modus)
// ============================================================================

async function loadSettings() {
    try {
        const result = await chrome.storage.local.get([
            "zielordner", 
            "useDesktopApp", 
            "desktopZielordner"
        ]);

        inputZielordner.value = result.zielordner || DEFAULT_ZIELORDNER;
        inputDesktopZielordner.value = result.desktopZielordner || DEFAULT_DESKTOP_ZIELORDNER;

        // Desktop-App Toggle setzen
        const useDesktop = result.useDesktopApp || false;
        toggleDesktopApp.checked = useDesktop;
        updateDesktopAppUI(useDesktop);

        if (useDesktop) {
            checkNativeHostStatus();
        }
    } catch (error) {
        console.error("[GyazoDumper Popup] Fehler beim Laden:", error);
        inputZielordner.value = DEFAULT_ZIELORDNER;
    }
}

async function saveSettings() {
    const pfad = inputZielordner.value.trim();

    if (!pfad) {
        showStatus("Bitte einen Pfad eingeben.", true);
        return;
    }

    try {
        await chrome.storage.local.set({ zielordner: pfad });
        showStatus("Gespeichert!");
    } catch (error) {
        showStatus("Fehler beim Speichern!", true);
    }
}

async function saveDesktopPath() {
    const pfad = inputDesktopZielordner.value.trim();

    if (!pfad) {
        showStatus("Bitte einen vollstaendigen Pfad eingeben.", true);
        return;
    }

    try {
        await chrome.storage.local.set({ desktopZielordner: pfad });
        showStatus("Desktop-Pfad gespeichert!");
    } catch (error) {
        showStatus("Fehler beim Speichern!", true);
    }
}

// ============================================================================
//  Desktop-App Modus
// ============================================================================

function updateDesktopAppUI(enabled) {
    if (enabled) {
        desktopAppSection.style.display = "block";
        browserZielordnerSection.style.display = "none";
    } else {
        desktopAppSection.style.display = "none";
        browserZielordnerSection.style.display = "block";
    }
}

async function toggleDesktopAppMode() {
    const enabled = toggleDesktopApp.checked;

    try {
        await chrome.storage.local.set({ useDesktopApp: enabled });
        updateDesktopAppUI(enabled);

        if (enabled) {
            checkNativeHostStatus();
        }

        showStatus(enabled ? "Desktop-App Modus aktiviert" : "Browser-Modus aktiviert");
    } catch (error) {
        showStatus("Fehler beim Umschalten!", true);
        toggleDesktopApp.checked = !enabled;
    }
}

async function checkNativeHostStatus() {
    appStatusIndicator.className = "status-indicator checking";
    appStatusText.textContent = "Pruefe Verbindung...";
    installSection.style.display = "none";

    try {
        const response = await chrome.runtime.sendMessage({ action: "checkNativeHost" });

        if (response.installed) {
            appStatusIndicator.className = "status-indicator connected";
            appStatusText.textContent = "Desktop-App verbunden";
            installSection.style.display = "none";
        } else {
            appStatusIndicator.className = "status-indicator disconnected";
            appStatusText.textContent = "Desktop-App nicht gefunden";
            installSection.style.display = "block";
        }
    } catch (error) {
        appStatusIndicator.className = "status-indicator disconnected";
        appStatusText.textContent = "Verbindungsfehler";
        installSection.style.display = "block";
    }
}

function downloadSetup() {
    chrome.downloads.download({
        url: SETUP_DOWNLOAD_URL,
        filename: "GyazoDumper-Setup.exe",
        saveAs: true
    }, (downloadId) => {
        if (chrome.runtime.lastError) {
            showStatus("Download fehlgeschlagen!", true);
        } else {
            showStatus("Setup wird heruntergeladen...");
        }
    });
}

function copyExtensionId() {
    navigator.clipboard.writeText(chrome.runtime.id).then(() => {
        btnCopyId.textContent = "Kopiert!";
        btnCopyId.classList.add("copied");
        setTimeout(() => {
            btnCopyId.textContent = "Kopieren";
            btnCopyId.classList.remove("copied");
        }, 2000);
    });
}

// ============================================================================
//  ID-Zaehler
// ============================================================================

async function updateIdCount() {
    try {
        const result = await chrome.storage.local.get("downloadedIds");
        const count = (result.downloadedIds || []).length;
        idCountEl.textContent = `${count} IDs gespeichert`;
    } catch (error) {
        idCountEl.textContent = "Fehler beim Laden";
    }
}

// ============================================================================
//  Export: IDs als Textdatei speichern (saveAs-Dialog)
// ============================================================================

async function exportIds() {
    try {
        const result = await chrome.storage.local.get("downloadedIds");
        const ids = result.downloadedIds || [];

        if (ids.length === 0) {
            showStatus("Keine IDs zum Exportieren vorhanden.", true);
            return;
        }

        const content = ids.join("\n") + "\n";
        const blob = new Blob([content], { type: "text/plain" });
        const blobUrl = URL.createObjectURL(blob);

        chrome.downloads.download(
            {
                url: blobUrl,
                filename: "Saved_URLs.txt",
                saveAs: true
            },
            (downloadId) => {
                URL.revokeObjectURL(blobUrl);
                if (chrome.runtime.lastError) {
                    showStatus("Export fehlgeschlagen!", true);
                } else {
                    showStatus(`${ids.length} IDs exportiert!`);
                }
            }
        );
    } catch (error) {
        showStatus("Export fehlgeschlagen!", true);
    }
}

// ============================================================================
//  Reset: Alle IDs loeschen (mit Bestaetigungsdialog)
// ============================================================================

function showConfirmDialog() {
    confirmOverlay.classList.add("visible");
}

function hideConfirmDialog() {
    confirmOverlay.classList.remove("visible");
}

async function resetIds() {
    hideConfirmDialog();

    try {
        await chrome.storage.local.set({ downloadedIds: [] });
        showStatus("Alle IDs geloescht!");
        updateIdCount();
    } catch (error) {
        showStatus("Fehler beim Loeschen!", true);
    }
}

// ============================================================================
//  Status-Anzeige
// ============================================================================

function showStatus(message, isError = false) {
    statusEl.textContent = message;
    statusEl.className = isError ? "error" : "";

    setTimeout(() => {
        statusEl.textContent = "";
        statusEl.className = "";
    }, 3000);
}

// ============================================================================
//  Event-Listener
// ============================================================================

btnSave.addEventListener("click", saveSettings);
inputZielordner.addEventListener("keydown", (e) => {
    if (e.key === "Enter") saveSettings();
});
btnExport.addEventListener("click", exportIds);
btnReset.addEventListener("click", showConfirmDialog);
btnConfirmYes.addEventListener("click", resetIds);
btnConfirmNo.addEventListener("click", hideConfirmDialog);

// Desktop-App Event-Listener
toggleDesktopApp.addEventListener("change", toggleDesktopAppMode);
btnInstallApp.addEventListener("click", downloadSetup);
btnCopyId.addEventListener("click", copyExtensionId);
btnSaveDesktopPath.addEventListener("click", saveDesktopPath);
inputDesktopZielordner.addEventListener("keydown", (e) => {
    if (e.key === "Enter") saveDesktopPath();
});

// Beim Oeffnen des Popups: Einstellungen und ID-Zaehler laden
loadSettings();
updateIdCount();
