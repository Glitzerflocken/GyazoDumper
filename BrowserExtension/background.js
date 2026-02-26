/**
 * GyazoDumper - Background Service Worker
 * 
 * Empfaengt Nachrichten vom Content Script mit der Bild-URL
 * und laedt das Bild automatisch herunter.
 * 
 * Modi:
 *   - Browser-Modus: Speichert ueber chrome.downloads API im Download-Ordner
 *   - Desktop-App-Modus: Sendet an Native Messaging Host fuer beliebigen Speicherort
 * 
 * Persistenz:
 *   - chrome.storage.local speichert alle heruntergeladenen IDs (schnell, persistent)
 *   - IDs koennen ueber das Popup als Textdatei exportiert werden
 */

// Standard-Zielordner (Unterordner im Chrome-Download-Verzeichnis)
const DEFAULT_ZIELORDNER = "GyazooDumper";
const NATIVE_HOST_NAME = "gyazodumper.nativeapp";

// Aktueller Zielordner (wird beim Start aus chrome.storage.local geladen)
let zielordner = DEFAULT_ZIELORDNER;

// Desktop-App Modus aktiviert?
let useDesktopApp = false;

// Native Messaging Port (nur wenn Desktop-App aktiv)
let nativePort = null;

// Set um bereits heruntergeladene Bilder zu tracken (verhindert Doppel-Downloads)
let downloadedIds = new Set();

// Flag: Wurden die IDs bereits aus dem Storage geladen?
let storageLoaded = false;

// ============================================================================
//  Persistenz: IDs und Einstellungen laden und speichern
// ============================================================================

/**
 * Laedt alle gespeicherten IDs und den Zielordner aus chrome.storage.local.
 * Wird beim Start des Service Workers aufgerufen.
 */
async function loadDownloadedIds() {
    if (storageLoaded) return;

    try {
        const result = await chrome.storage.local.get(["downloadedIds", "zielordner", "useDesktopApp"]);
        const savedIds = result.downloadedIds || [];
        downloadedIds = new Set(savedIds);

        if (result.zielordner) {
            zielordner = result.zielordner;
        }

        useDesktopApp = result.useDesktopApp || false;

        storageLoaded = true;
        console.log(`[GyazoDumper] ${downloadedIds.size} gespeicherte IDs aus Storage geladen.`);
        console.log(`[GyazoDumper] Zielordner: ${zielordner}`);
        console.log(`[GyazoDumper] Desktop-App Modus: ${useDesktopApp}`);
    } catch (error) {
        console.error("[GyazoDumper] Fehler beim Laden:", error);
        storageLoaded = true;
    }
}

/**
 * Listener: Reagiert auf Aenderungen in chrome.storage.local.
 * Wenn der Benutzer den Zielordner im Popup aendert, wird er hier sofort uebernommen.
 */
chrome.storage.onChanged.addListener((changes, areaName) => {
    if (areaName === "local") {
        if (changes.zielordner) {
            zielordner = changes.zielordner.newValue;
            console.log(`[GyazoDumper] Zielordner aktualisiert: ${zielordner}`);
        }
        if (changes.downloadedIds) {
            downloadedIds = new Set(changes.downloadedIds.newValue || []);
            console.log(`[GyazoDumper] ID-Liste aktualisiert: ${downloadedIds.size} IDs`);
        }
        if (changes.useDesktopApp !== undefined) {
            useDesktopApp = changes.useDesktopApp.newValue;
            console.log(`[GyazoDumper] Desktop-App Modus: ${useDesktopApp}`);
            // Port trennen wenn Desktop-App deaktiviert wird
            if (!useDesktopApp && nativePort) {
                nativePort.disconnect();
                nativePort = null;
            }
        }
    }
});

/**
 * Speichert eine neue ID in chrome.storage.local.
 */
async function saveIdToStorage(gyazoId) {
    try {
        const result = await chrome.storage.local.get("downloadedIds");
        const savedIds = result.downloadedIds || [];
        savedIds.push(gyazoId);
        await chrome.storage.local.set({ downloadedIds: savedIds });
        console.log(`[GyazoDumper] ID ${gyazoId} in Storage gespeichert. Gesamt: ${savedIds.length}`);
    } catch (error) {
        console.error("[GyazoDumper] Fehler beim Speichern der ID:", error);
    }
}

// ============================================================================
//  Native Messaging (Desktop-App Modus)
// ============================================================================

/**
 * Verbindet zum Native Messaging Host (Desktop-App)
 */
function connectToNativeHost() {
    if (nativePort) return nativePort;

    try {
        nativePort = chrome.runtime.connectNative(NATIVE_HOST_NAME);

        nativePort.onMessage.addListener((response) => {
            console.log("[GyazoDumper] Native Host Antwort:", response);
        });

        nativePort.onDisconnect.addListener(() => {
            const error = chrome.runtime.lastError?.message || "Unbekannter Fehler";
            console.log(`[GyazoDumper] Native Host getrennt: ${error}`);
            nativePort = null;
        });

        console.log("[GyazoDumper] Mit Native Host verbunden.");
        return nativePort;
    } catch (error) {
        console.error("[GyazoDumper] Verbindung zu Native Host fehlgeschlagen:", error);
        return null;
    }
}

/**
 * Sendet Bild-URL an den Native Host zum Download
 */
async function downloadViaNativeHost(imageUrl, gyazoId) {
    const port = connectToNativeHost();
    if (!port) {
        return { success: false, error: "Native Host nicht verfuegbar" };
    }

    return new Promise((resolve) => {
        const messageHandler = (response) => {
            port.onMessage.removeListener(messageHandler);
            resolve(response);
        };

        port.onMessage.addListener(messageHandler);

        port.postMessage({
            action: "saveImage",
            imageUrl: imageUrl,
            gyazoId: gyazoId,
            timestamp: new Date().toISOString()
        });

        // Timeout nach 30 Sekunden
        setTimeout(() => {
            port.onMessage.removeListener(messageHandler);
            resolve({ success: false, error: "Timeout" });
        }, 30000);
    });
}

// ============================================================================
//  Browser Download (Standard-Modus)
// ============================================================================

/**
 * Laedt Bild ueber chrome.downloads API herunter
 */
function downloadViaBrowser(imageUrl, gyazoId) {
    return new Promise((resolve) => {
        const urlPath = new URL(imageUrl).pathname;
        const extension = urlPath.substring(urlPath.lastIndexOf(".")) || ".png";

        chrome.downloads.download(
            {
                url: imageUrl,
                filename: `${zielordner}/${gyazoId}${extension}`,
                saveAs: false,
                conflictAction: "uniquify"
            },
            (downloadId) => {
                if (chrome.runtime.lastError) {
                    resolve({ success: false, error: chrome.runtime.lastError.message });
                } else {
                    resolve({ success: true, downloadId: downloadId });
                }
            }
        );
    });
}

// ============================================================================
//  Message-Listener: Download-Anfragen vom Content Script
// ============================================================================

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === "downloadGyazoImage") {
        (async () => {
            await loadDownloadedIds();

            const imageUrl = message.imageUrl;
            const gyazoId = message.gyazoId;

            // Doppel-Downloads verhindern
            if (downloadedIds.has(gyazoId)) {
                console.log(`[GyazoDumper] Bild ${gyazoId} bereits heruntergeladen, ueberspringe.`);
                sendResponse({ success: true, downloadId: null, skipped: true });
                return;
            }

            console.log(`[GyazoDumper] Download angefordert: ${imageUrl}`);
            console.log(`[GyazoDumper] Modus: ${useDesktopApp ? "Desktop-App" : "Browser"}`);

            let result;
            if (useDesktopApp) {
                result = await downloadViaNativeHost(imageUrl, gyazoId);
            } else {
                result = await downloadViaBrowser(imageUrl, gyazoId);
            }

            if (result.success) {
                console.log(`[GyazoDumper] Download erfolgreich!`);
                downloadedIds.add(gyazoId);
                await saveIdToStorage(gyazoId);
            } else {
                console.error(`[GyazoDumper] Download-Fehler: ${result.error}`);
            }

            sendResponse(result);
        })();

        return true;
    }

    // Nachricht vom Popup: Native Host Status pruefen
    if (message.action === "checkNativeHost") {
        (async () => {
            try {
                const port = chrome.runtime.connectNative(NATIVE_HOST_NAME);

                const timeout = setTimeout(() => {
                    port.disconnect();
                    sendResponse({ installed: false, error: "Timeout" });
                }, 3000);

                port.onMessage.addListener((response) => {
                    clearTimeout(timeout);
                    port.disconnect();
                    sendResponse({ installed: true, response: response });
                });

                port.onDisconnect.addListener(() => {
                    clearTimeout(timeout);
                    const error = chrome.runtime.lastError?.message || "Nicht installiert";
                    sendResponse({ installed: false, error: error });
                });

                port.postMessage({ action: "ping" });
            } catch (error) {
                sendResponse({ installed: false, error: error.message });
            }
        })();

        return true;
    }

    // Nachricht vom Popup: Konfiguration vom Native Host laden
    if (message.action === "getNativeConfig") {
        (async () => {
            try {
                const port = connectToNativeHost();
                if (!port) {
                    sendResponse({ success: false, error: "Native Host nicht verbunden" });
                    return;
                }

                const handler = (response) => {
                    port.onMessage.removeListener(handler);
                    sendResponse(response);
                };
                port.onMessage.addListener(handler);
                port.postMessage({ action: "getConfig" });

                setTimeout(() => {
                    port.onMessage.removeListener(handler);
                    sendResponse({ success: false, error: "Timeout" });
                }, 3000);
            } catch (error) {
                sendResponse({ success: false, error: error.message });
            }
        })();

        return true;
    }

    // Nachricht vom Popup: Ordnerauswahl-Dialog ueber Native Host oeffnen
    if (message.action === "browseFolder") {
        (async () => {
            try {
                const port = connectToNativeHost();
                if (!port) {
                    sendResponse({ success: false, error: "Native Host nicht verbunden" });
                    return;
                }

                const handler = (response) => {
                    port.onMessage.removeListener(handler);
                    sendResponse(response);
                };
                port.onMessage.addListener(handler);
                port.postMessage({ action: "selectFolder" });

                setTimeout(() => {
                    port.onMessage.removeListener(handler);
                    sendResponse({ success: false, error: "Timeout" });
                }, 120000); // 2 Minuten - Benutzer braucht Zeit im Dialog
            } catch (error) {
                sendResponse({ success: false, error: error.message });
            }
        })();

        return true;
    }
});

// IDs beim Start laden
loadDownloadedIds();
console.log("[GyazoDumper] Service Worker gestartet.");
