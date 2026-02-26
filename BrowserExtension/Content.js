/**
 * GyazoDumper - Content Script
 * 
 * Laeuft nur auf gyazo.com Seiten.
 * Liest den Bild-Link aus dem HTML-DOM aus und sendet ihn
 * an den Background Service Worker zum Download.
 *
 * SPA-Navigation:
 *   Gyazo verwendet history.pushState (React) wenn man per Pfeil-Tasten
 *   zwischen Bildern wechselt. Content Scripts laufen in einer isolierten
 *   Welt und koennen pushState der Haupt-Welt nicht abfangen.
 *   Daher wird URL-Polling (alle 500ms) verwendet um Navigationen zu erkennen.
 */

// Regex: Prueft ob die URL eine Gyazo-Bild-Seite ist (32 hex-Zeichen nach gyazo.com/)
const GYAZO_URL_PATTERN = /^https:\/\/gyazo\.com\/([a-f0-9]{32})$/;

// Letzte verarbeitete URL - verhindert doppelte Verarbeitung
let lastProcessedUrl = null;

// Aktiver MutationObserver (wird bei URL-Wechsel gestoppt/neugestartet)
let activeObserver = null;
let activeTimeout = null;

// ============================================================================
//  SPA-Navigation erkennen (URL-Polling)
// ============================================================================

/**
 * Prueft regelmaessig ob sich die URL geaendert hat.
 * Content Scripts laufen in einer isolierten Welt und koennen
 * history.pushState der Haupt-Seite (Gyazo React) nicht abfangen.
 * URL-Polling ist die zuverlaessigste Methode.
 */
setInterval(() => {
    const currentUrl = window.location.href;
    if (currentUrl !== lastProcessedUrl) {
        onUrlChange();
    }
}, 500);

// Browser-Zurueck/Vorwaerts (popstate feuert auch in der Content-Welt)
window.addEventListener("popstate", onUrlChange);

/**
 * Wird bei jeder URL-Aenderung aufgerufen.
 * Prueft ob es eine neue Gyazo-Bild-URL ist und startet den Download.
 */
function onUrlChange() {
    const currentUrl = window.location.href;
    if (currentUrl === lastProcessedUrl) return;

    lastProcessedUrl = currentUrl;

    // Vorherigen Observer/Timeout aufraumen
    if (activeObserver) {
        activeObserver.disconnect();
        activeObserver = null;
    }
    if (activeTimeout) {
        clearTimeout(activeTimeout);
        activeTimeout = null;
    }

    // Kurzer Delay: Bei SPA-Navigation aendert sich die URL sofort,
    // aber das Bild-Element im DOM braucht einen Moment um zu aktualisieren
    setTimeout(findAndSendImageUrl, 100);
}

// ============================================================================
//  Bild-Erkennung und Download
// ============================================================================

/**
 * Versucht das Bild-Element im DOM zu finden und den src-Link auszulesen.
 * Bei SPA-Navigation kann das Bild kurz verzögert geladen werden,
 * daher wird ein MutationObserver als Fallback verwendet.
 */
function findAndSendImageUrl() {
    const currentUrl = window.location.href;
    const match = currentUrl.match(GYAZO_URL_PATTERN);

    if (!match) return;

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

    activeObserver = new MutationObserver((mutations, obs) => {
        const imageUrl = getImageSrcFromDom();
        if (imageUrl) {
            obs.disconnect();
            activeObserver = null;
            sendDownloadRequest(imageUrl, gyazoId);
        }
    });

    activeObserver.observe(document.body, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src"]
    });

    // Timeout nach 15 Sekunden
    activeTimeout = setTimeout(() => {
        if (activeObserver) {
            activeObserver.disconnect();
            activeObserver = null;
        }
        console.warn("[GyazoDumper] Timeout: Bild konnte nicht gefunden werden.");
    }, 15000);
}

/**
 * Sucht im DOM nach dem Bild-Element und gibt die src-URL zurueck.
 * Prueft ob die src eine gueltige Bild-URL ist (nicht die alte bei SPA-Navigation).
 */
function getImageSrcFromDom() {
    const imgElement = document.querySelector(
        "div.image-box-component picture img.image-viewer"
    );

    if (imgElement && imgElement.src && imgElement.src.startsWith("http")) {
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
                if (response.skipped) {
                    console.log(`[GyazoDumper] Bild bereits vorhanden, uebersprungen.`);
                } else {
                    console.log(`[GyazoDumper] Download gestartet!`);
                    showSavedBanner();
                }
            } else {
                console.error("[GyazoDumper] Download fehlgeschlagen:", response?.error);
            }
        }
    );
}

// ============================================================================
//  "Picture Saved!" Banner
// ============================================================================

// CSS wird einmalig in die Seite injiziert
let bannerStyleInjected = false;

function injectBannerStyle() {
    if (bannerStyleInjected) return;
    bannerStyleInjected = true;

    const style = document.createElement("style");
    style.textContent = `
        @keyframes gyazodumper-slide-in {
            0%   { transform: translateX(400px); opacity: 0; }
            11%  { transform: translateX(0);     opacity: 1; }
            56%  { transform: translateX(0);     opacity: 1; }
            100% { transform: translateX(0);     opacity: 0; }
        }
        .gyazodumper-banner {
            position: fixed !important;
            bottom: 10% !important;
            right: 50px !important;
            z-index: 2147483647 !important;
            display: flex !important;
            align-items: center !important;
            gap: 10px !important;
            padding: 10px 18px !important;
            background: rgba(30, 30, 30, 0.92) !important;
            border-radius: 10px !important;
            box-shadow: 0 4px 20px rgba(0,0,0,0.35) !important;
            font-family: "Segoe UI", Arial, sans-serif !important;
            font-size: 14px !important;
            color: #fff !important;
            pointer-events: none !important;
            user-select: none !important;
            animation: gyazodumper-slide-in 4.5s ease-out forwards !important;
        }
        .gyazodumper-banner img {
            width: 22px !important;
            height: 22px !important;
            border-radius: 4px !important;
        }
    `;
    document.head.appendChild(style);
}

/**
 * Zeigt ein kleines "Picture Saved!" Banner an.
 * Fliegt von rechts rein, blendet ein, ruht, blendet aus.
 */
function showSavedBanner() {
    injectBannerStyle();

    const banner = document.createElement("div");
    banner.className = "gyazodumper-banner";

    const icon = document.createElement("img");
    icon.src = chrome.runtime.getURL("GyazoDumper.png");
    icon.alt = "";

    const text = document.createElement("span");
    text.textContent = "Picture Saved!";

    banner.appendChild(icon);
    banner.appendChild(text);
    document.body.appendChild(banner);

    // Banner nach der Animation entfernen
    banner.addEventListener("animationend", () => {
        banner.remove();
    });
}

// Erste Verarbeitung beim Laden der Seite
lastProcessedUrl = window.location.href;
findAndSendImageUrl();
