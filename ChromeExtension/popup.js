/**
 * GyazoDumper - Popup Script
 * 
 * Verwaltet die Einstellungen im Popup-Menue der Extension.
 *   - Zielordner laden/speichern (Browser-Modus)
 *   - Desktop-App Modus aktivieren/deaktivieren
 *   - Desktop-App Status pruefen und Installation anbieten
 *   - Gespeicherte IDs als Textdatei exportieren (saveAs-Dialog)
 *   - Alle gespeicherten IDs loeschen (mit Bestaetigungsdialog)
 */

// Standard-Zielordner (Unterordner im Chrome-Download-Verzeichnis)
const DEFAULT_ZIELORDNER = "GyazooDumper";
const DEFAULT_DESKTOP_ZIELORDNER = "";

// GitHub Release URL fuer automatischen Download
const GITHUB_RELEASE_URL = "https://github.com/Glitzerflocken/GyazoDumper/releases/latest";

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
const btnInstallApp = document.getElementById("btnInstallApp");
const installHint = document.getElementById("installHint");
const inputDesktopZielordner = document.getElementById("inputDesktopZielordner");
const btnSaveDesktopPath = document.getElementById("btnSaveDesktopPath");

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
    btnInstallApp.style.display = "none";
    installHint.style.display = "none";

    try {
        const response = await chrome.runtime.sendMessage({ action: "checkNativeHost" });

        if (response.installed) {
            appStatusIndicator.className = "status-indicator connected";
            appStatusText.textContent = "Desktop-App verbunden";
            btnInstallApp.style.display = "none";
            installHint.style.display = "none";
        } else {
            appStatusIndicator.className = "status-indicator disconnected";
            appStatusText.textContent = "Desktop-App nicht gefunden";
            btnInstallApp.style.display = "block";
            installHint.style.display = "block";
        }
    } catch (error) {
        appStatusIndicator.className = "status-indicator disconnected";
        appStatusText.textContent = "Verbindungsfehler";
        btnInstallApp.style.display = "block";
        installHint.style.display = "block";
    }
}

function openInstallPage() {
    chrome.tabs.create({ url: GITHUB_RELEASE_URL });
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
                saveAs: true  // Benutzer waehlt den Speicherort
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
btnInstallApp.addEventListener("click", openInstallPage);
btnSaveDesktopPath.addEventListener("click", saveDesktopPath);
inputDesktopZielordner.addEventListener("keydown", (e) => {
    if (e.key === "Enter") saveDesktopPath();
});

// Beim Oeffnen des Popups: Einstellungen und ID-Zaehler laden
loadSettings();
updateIdCount();
