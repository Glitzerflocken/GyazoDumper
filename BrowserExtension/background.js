/**
 * GyazoDumper - Background Service Worker
 * 
 * Receives messages from the Content Script with the image URL
 * and downloads the image automatically.
 * 
 * Modes:
 *   - Browser mode: Saves via chrome.downloads API to the download folder
 *   - Desktop App mode: Sends to Native Messaging Host for any save location
 * 
 * Persistence:
 *   - chrome.storage.local stores all downloaded IDs (fast, persistent)
 *   - IDs can be exported as a text file via the popup
 */

// Default target folder (subfolder in the Chrome download directory)
const DEFAULT_ZIELORDNER = "GyazoDumps";
const NATIVE_HOST_NAME = "gyazodumper.nativeapp";

// Current target folder (loaded from chrome.storage.local on startup)
let zielordner = DEFAULT_ZIELORDNER;

// Desktop App mode enabled?
let useDesktopApp = false;

// Native Messaging Port (only when Desktop App is active)
let nativePort = null;

// Set to track already downloaded images (prevents duplicate downloads)
let downloadedIds = new Set();

// Flag: Have the IDs been loaded from storage yet?
let storageLoaded = false;

// ============================================================================
//  Persistence: Load and save IDs and settings
// ============================================================================

/**
 * Loads all saved IDs and the target folder from chrome.storage.local.
 * Called on Service Worker startup.
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
        console.log(`[GyazoDumper] ${downloadedIds.size} saved IDs loaded from storage.`);
        console.log(`[GyazoDumper] Target folder: ${zielordner}`);
        console.log(`[GyazoDumper] Desktop App mode: ${useDesktopApp}`);
    } catch (error) {
        console.error("[GyazoDumper] Error loading:", error);
        storageLoaded = true;
    }
}

/**
 * Listener: Reacts to changes in chrome.storage.local.
 * When the user changes the target folder in the popup, it is applied here immediately.
 */
chrome.storage.onChanged.addListener((changes, areaName) => {
    if (areaName === "local") {
        if (changes.zielordner) {
            zielordner = changes.zielordner.newValue;
            console.log(`[GyazoDumper] Target folder updated: ${zielordner}`);
        }
        if (changes.downloadedIds) {
            downloadedIds = new Set(changes.downloadedIds.newValue || []);
            console.log(`[GyazoDumper] ID list updated: ${downloadedIds.size} IDs`);
        }
        if (changes.useDesktopApp !== undefined) {
            useDesktopApp = changes.useDesktopApp.newValue;
            console.log(`[GyazoDumper] Desktop App mode: ${useDesktopApp}`);
            // Disconnect port when Desktop App is deactivated
            if (!useDesktopApp && nativePort) {
                nativePort.disconnect();
                nativePort = null;
            }
        }
    }
});

/**
 * Saves a new ID to chrome.storage.local.
 */
async function saveIdToStorage(gyazoId) {
    try {
        const result = await chrome.storage.local.get("downloadedIds");
        const savedIds = result.downloadedIds || [];
        savedIds.push(gyazoId);
        await chrome.storage.local.set({ downloadedIds: savedIds });
        console.log(`[GyazoDumper] ID ${gyazoId} saved to storage. Total: ${savedIds.length}`);
    } catch (error) {
        console.error("[GyazoDumper] Error saving ID:", error);
    }
}

// ============================================================================
//  Native Messaging (Desktop App mode)
// ============================================================================

/**
 * Connects to the Native Messaging Host (Desktop App).
 */
function connectToNativeHost() {
    if (nativePort) return nativePort;

    try {
        nativePort = chrome.runtime.connectNative(NATIVE_HOST_NAME);

        nativePort.onMessage.addListener((response) => {
            console.log("[GyazoDumper] Native Host response:", response);
        });

        nativePort.onDisconnect.addListener(() => {
            const error = chrome.runtime.lastError?.message || "Unknown error";
            console.log(`[GyazoDumper] Native Host disconnected: ${error}`);
            nativePort = null;
        });

        console.log("[GyazoDumper] Connected to Native Host.");
        return nativePort;
    } catch (error) {
        console.error("[GyazoDumper] Connection to Native Host failed:", error);
        return null;
    }
}

/**
 * Sends image URL to the Native Host for download.
 */
async function downloadViaNativeHost(imageUrl, gyazoId) {
    const port = connectToNativeHost();
    if (!port) {
        return { success: false, error: "Native Host not available" };
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

        // Timeout after 30 seconds
        setTimeout(() => {
            port.onMessage.removeListener(messageHandler);
            resolve({ success: false, error: "Timeout" });
        }, 30000);
    });
}

// ============================================================================
//  Browser Download (default mode)
// ============================================================================

/**
 * Downloads image via chrome.downloads API.
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
//  Message listener: Download requests from the Content Script
// ============================================================================

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === "downloadGyazoImage") {
        (async () => {
            await loadDownloadedIds();

            const imageUrl = message.imageUrl;
            const gyazoId = message.gyazoId;

            // Prevent duplicate downloads
            if (downloadedIds.has(gyazoId)) {
                console.log(`[GyazoDumper] Image ${gyazoId} already downloaded, skipping.`);
                sendResponse({ success: true, downloadId: null, skipped: true });
                return;
            }

            console.log(`[GyazoDumper] Download requested: ${imageUrl}`);
            console.log(`[GyazoDumper] Mode: ${useDesktopApp ? "Desktop App" : "Browser"}`);

            let result;
            if (useDesktopApp) {
                result = await downloadViaNativeHost(imageUrl, gyazoId);
            } else {
                result = await downloadViaBrowser(imageUrl, gyazoId);
            }

            if (result.success) {
                console.log(`[GyazoDumper] Download successful!`);
                downloadedIds.add(gyazoId);
                await saveIdToStorage(gyazoId);
            } else {
                console.error(`[GyazoDumper] Download error: ${result.error}`);
            }

            sendResponse(result);
        })();

        return true;
    }

    // Message from popup: Check Native Host status
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
                    const error = chrome.runtime.lastError?.message || "Not installed";
                    sendResponse({ installed: false, error: error });
                });

                port.postMessage({ action: "ping" });
            } catch (error) {
                sendResponse({ installed: false, error: error.message });
            }
        })();

        return true;
    }

    // Message from popup: Load configuration from Native Host
    if (message.action === "getNativeConfig") {
        (async () => {
            try {
                const port = connectToNativeHost();
                if (!port) {
                    sendResponse({ success: false, error: "Native Host not connected" });
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

    // Message from popup: Open folder picker dialog via Native Host
    // The popup closes automatically when the dialog gets focus (Chrome limitation).
    // After folder selection, the popup is reopened via chrome.action.openPopup().
    if (message.action === "browseFolder") {
        (async () => {
            try {
                const port = connectToNativeHost();
                if (!port) {
                    sendResponse({ success: false, error: "Native Host not connected" });
                    return;
                }

                const handler = (response) => {
                    port.onMessage.removeListener(handler);
                    try { sendResponse(response); } catch {}

                    // Reopen popup after folder selection (Chrome 127+)
                    setTimeout(() => {
                        try { chrome.action.openPopup(); } catch {}
                    }, 300);
                };
                port.onMessage.addListener(handler);
                port.postMessage({ action: "selectFolder" });

                setTimeout(() => {
                    port.onMessage.removeListener(handler);
                    try { sendResponse({ success: false, error: "Timeout" }); } catch {}
                }, 120000);
            } catch (error) {
                sendResponse({ success: false, error: error.message });
            }
        })();

        return true;
    }

    // Message from popup: Open save folder in Windows Explorer
    if (message.action === "openFolder") {
        (async () => {
            try {
                const port = connectToNativeHost();
                if (!port) {
                    sendResponse({ success: false, error: "Native Host not connected" });
                    return;
                }

                const handler = (response) => {
                    port.onMessage.removeListener(handler);
                    sendResponse(response);
                };
                port.onMessage.addListener(handler);
                port.postMessage({ action: "openFolder" });

                setTimeout(() => {
                    port.onMessage.removeListener(handler);
                    sendResponse({ success: false, error: "Timeout" });
                }, 5000);
            } catch (error) {
                sendResponse({ success: false, error: error.message });
            }
        })();

        return true;
    }
});

// Load IDs on startup
loadDownloadedIds();
console.log("[GyazoDumper] Service Worker started.");
