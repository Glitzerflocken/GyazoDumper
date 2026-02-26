/**
 * GyazoDumper - Content Script
 * 
 * Laeuft nur auf gyazo.com Seiten.
 * Liest den Bild-Link aus dem HTML-DOM aus und sendet ihn
 * an den Background Service Worker zum Download.
 * 
 * Der Link befindet sich im DOM unter:
 *   div.image-box-component > picture > img.image-viewer
 *   -> src Attribut
 */

// Regex: Prueft ob die URL eine Gyazo-Bild-Seite ist (32 hex-Zeichen nach gyazo.com/)
const GYAZO_URL_PATTERN = /^https:\/\/gyazo\.com\/([a-f0-9]{32})$/;

/**
 * Versucht das Bild-Element im DOM zu finden und den src-Link auszulesen.
 * Da das Bild moeglicherweise noch nicht geladen ist wenn das Script startet,
 * wird ein MutationObserver verwendet um auf DOM-Aenderungen zu warten.
 */
function findAndSendImageUrl() {
    const currentUrl = window.location.href;
    const match = currentUrl.match(GYAZO_URL_PATTERN);

    if (!match) {
        return; // Keine Gyazo-Bild-Seite
    }

    const gyazoId = match[1];
    console.log(`[GyazoDumper] Gyazo-Seite erkannt! ID: ${gyazoId}`);

    // Versuche sofort das Bild zu finden
    const imageUrl = getImageSrcFromDom();
    if (imageUrl) {
        sendDownloadRequest(imageUrl, gyazoId);
        return;
    }

    // Bild noch nicht im DOM -> MutationObserver starten
    console.log("[GyazoDumper] Bild noch nicht im DOM, warte auf Laden...");

    const observer = new MutationObserver((mutations, obs) => {
        const imageUrl = getImageSrcFromDom();
        if (imageUrl) {
            obs.disconnect(); // Observer stoppen
            sendDownloadRequest(imageUrl, gyazoId);
        }
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src"]
    });

    // Timeout nach 15 Sekunden - falls das Bild nie erscheint
    setTimeout(() => {
        observer.disconnect();
        console.warn("[GyazoDumper] Timeout: Bild konnte nicht gefunden werden.");
    }, 15000);
}

/**
 * Sucht im DOM nach dem Bild-Element und gibt die src-URL zurueck.
 * Pfad: div.image-box-component > picture > img.image-viewer
 */
function getImageSrcFromDom() {
    const imgElement = document.querySelector(
        "div.image-box-component picture img.image-viewer"
    );

    if (imgElement && imgElement.src) {
        console.log(`[GyazoDumper] Bild-URL im DOM gefunden: ${imgElement.src}`);
        return imgElement.src;
    }

    return null;
}

/**
 * Sendet die gefundene Bild-URL an den Background Service Worker.
 */
function sendDownloadRequest(imageUrl, gyazoId) {
    console.log(`[GyazoDumper] Sende Download-Anfrage fuer: ${imageUrl}`);

    chrome.runtime.sendMessage(
        {
            action: "downloadGyazoImage",
            imageUrl: imageUrl,
            gyazoId: gyazoId
        },
        (response) => {
            if (chrome.runtime.lastError) {
                console.error("[GyazoDumper] Fehler beim Senden:", chrome.runtime.lastError.message);
            } else if (response && response.success) {
                console.log(`[GyazoDumper] Download gestartet! ID: ${response.downloadId}`);
            } else {
                console.error("[GyazoDumper] Download fehlgeschlagen:", response?.error);
            }
        }
    );
}

// Starten wenn die Seite geladen ist
findAndSendImageUrl();
