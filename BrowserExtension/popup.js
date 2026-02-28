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
 *   - Sprachumschaltung DE/EN
 */

// Standard-Zielordner (Unterordner im Chrome-Download-Verzeichnis)
const DEFAULT_ZIELORDNER = "GyazoDumps";

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
const installDetails = document.getElementById("installDetails");
const btnInstallApp = document.getElementById("btnInstallApp");
const btnCopyId = document.getElementById("btnCopyId");
const extensionIdEl = document.getElementById("extensionId");
const inputDesktopZielordner = document.getElementById("inputDesktopZielordner");
const btnBrowseFolder = document.getElementById("btnBrowseFolder");
const btnOpenFolder = document.getElementById("btnOpenFolder");

// Sprach-Buttons
const btnLangDe = document.getElementById("btnLangDe");
const btnLangEn = document.getElementById("btnLangEn");

// ============================================================================
//  Sprache
// ============================================================================

function updateLangButtons() {
    btnLangDe.classList.toggle("active", currentLang === "de");
    btnLangEn.classList.toggle("active", currentLang === "en");
}

btnLangDe.addEventListener("click", () => {
    setLanguage("de");
    updateLangButtons();
    updateIdCount();
});

btnLangEn.addEventListener("click", () => {
    setLanguage("en");
    updateLangButtons();
    updateIdCount();
});

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
            "useDesktopApp"
        ]);

        inputZielordner.value = result.zielordner || DEFAULT_ZIELORDNER;

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
        showStatus(t("statusEmptyPath"), true);
        return;
    }

    try {
        await chrome.storage.local.set({ zielordner: pfad });
        showStatus(t("statusSaved"));
    } catch (error) {
        showStatus(t("statusSaveError"), true);
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

        showStatus(enabled ? t("statusDesktopOn") : t("statusDesktopOff"));
    } catch (error) {
        showStatus(t("statusToggleError"), true);
        toggleDesktopApp.checked = !enabled;
    }
}

async function checkNativeHostStatus() {
    appStatusIndicator.className = "status-indicator checking";
    appStatusText.textContent = t("statusChecking");
    installDetails.removeAttribute("open");

    try {
        const response = await chrome.runtime.sendMessage({ action: "checkNativeHost" });

        if (response.installed) {
            appStatusIndicator.className = "status-indicator connected";
            appStatusText.textContent = t("statusConnected");
            installDetails.removeAttribute("open");
            loadNativeConfig();
        } else {
            appStatusIndicator.className = "status-indicator disconnected";
            appStatusText.textContent = t("statusNotFound");
            installDetails.setAttribute("open", "");
        }
    } catch (error) {
        appStatusIndicator.className = "status-indicator disconnected";
        appStatusText.textContent = t("statusError");
        installDetails.setAttribute("open", "");
    }
}

function downloadSetup() {
    chrome.downloads.download({
        url: SETUP_DOWNLOAD_URL,
        filename: "GyazoDumper-Setup.exe",
        saveAs: false
    }, (downloadId) => {
        if (chrome.runtime.lastError) {
            showStatus(t("statusDownloadFail"), true);
        } else {
            showStatus(t("statusDownloading"));
        }
    });
}

function copyExtensionId() {
    navigator.clipboard.writeText(chrome.runtime.id).then(() => {
        btnCopyId.textContent = t("btnCopied");
        btnCopyId.classList.add("copied");
        setTimeout(() => {
            btnCopyId.textContent = t("btnCopy");
            btnCopyId.classList.remove("copied");
        }, 2000);
    });
}

async function browseFolder() {
    btnBrowseFolder.disabled = true;
    btnBrowseFolder.textContent = t("browsingFolder");

    try {
        const response = await chrome.runtime.sendMessage({ action: "browseFolder" });

        if (response.success) {
            inputDesktopZielordner.value = response.message;
            showStatus(t("statusNewPath") + response.message);
        } else if (response.error !== "Abgebrochen") {
            showStatus(t("statusBrowseFail"), true);
        }
    } catch (error) {
        showStatus(t("statusBrowseError"), true);
    }

    btnBrowseFolder.disabled = false;
    btnBrowseFolder.textContent = t("btnBrowseFolder");
}

async function openFolder() {
    try {
        const response = await chrome.runtime.sendMessage({ action: "openFolder" });

        if (!response.success) {
            showStatus(t("statusOpenFail"), true);
        }
    } catch (error) {
        showStatus(t("statusOpenError"), true);
    }
}

async function loadNativeConfig() {
    try {
        const response = await chrome.runtime.sendMessage({ action: "getNativeConfig" });

        if (response.success && response.message) {
            inputDesktopZielordner.value = response.message;
        }
    } catch (error) {
        console.error("[GyazoDumper Popup] Config laden fehlgeschlagen:", error);
    }
}

// ============================================================================
//  ID-Zaehler
// ============================================================================

async function updateIdCount() {
    try {
        const result = await chrome.storage.local.get("downloadedIds");
        const count = (result.downloadedIds || []).length;
        idCountEl.textContent = t("idCount", { count });
    } catch (error) {
        idCountEl.textContent = t("statusIdLoadError");
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
            showStatus(t("statusNoIds"), true);
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
                    showStatus(t("statusExportFail"), true);
                } else {
                    showStatus(t("statusExported", { count: ids.length }));
                }
            }
        );
    } catch (error) {
        showStatus(t("statusExportFail"), true);
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
        showStatus(t("statusResetDone"));
        updateIdCount();
    } catch (error) {
        showStatus(t("statusResetError"), true);
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
btnBrowseFolder.addEventListener("click", browseFolder);
btnOpenFolder.addEventListener("click", openFolder);

// Beim Oeffnen des Popups: Sprache, Einstellungen und ID-Zaehler laden
loadLanguage().then(() => {
    updateLangButtons();
    loadSettings();
    updateIdCount();
});
