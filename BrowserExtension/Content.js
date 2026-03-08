/**
 * GyazoDumper - Content Script
 * 
 * Runs only on gyazo.com pages.
 * Reads the image link from the HTML DOM and sends it
 * to the Background Service Worker for download.
 *
 * SPA Navigation:
 *   Gyazo uses history.pushState (React) when navigating between
 *   images via arrow keys. Content Scripts run in an isolated world
 *   and cannot intercept pushState from the main world.
 *   Therefore URL polling (every 500ms) is used to detect navigations.
 */

// Regex: Checks if the URL is a Gyazo image page (32 hex characters after gyazo.com/)
const GYAZO_URL_PATTERN = /^https:\/\/gyazo\.com\/([a-f0-9]{32})$/;

// Last processed URL — prevents duplicate processing
let lastProcessedUrl = null;

// Active MutationObserver (stopped/restarted on URL change)
let activeObserver = null;
let activeTimeout = null;

// ============================================================================
//  Detect SPA navigation (URL polling)
// ============================================================================

/**
 * Periodically checks if the URL has changed.
 * Content Scripts run in an isolated world and cannot intercept
 * history.pushState from the main page (Gyazo React).
 * URL polling is the most reliable method.
 */
setInterval(() => {
    const currentUrl = window.location.href;
    if (currentUrl !== lastProcessedUrl) {
        onUrlChange();
    }
}, 500);

// Browser back/forward (popstate also fires in the content world)
window.addEventListener("popstate", onUrlChange);

/**
 * Called on every URL change.
 * Checks if it's a new Gyazo image URL and starts the download.
 */
function onUrlChange() {
    const currentUrl = window.location.href;
    if (currentUrl === lastProcessedUrl) return;

    lastProcessedUrl = currentUrl;

    // Clean up previous observer/timeout
    if (activeObserver) {
        activeObserver.disconnect();
        activeObserver = null;
    }
    if (activeTimeout) {
        clearTimeout(activeTimeout);
        activeTimeout = null;
    }

    // Short delay: On SPA navigation the URL changes immediately,
    // but the image element in the DOM needs a moment to update
    setTimeout(findAndSendImageUrl, 100);
}

// ============================================================================
//  Image detection and download
// ============================================================================

/**
 * Tries to find the image element in the DOM and read its src URL.
 * On SPA navigation the image may load with a short delay,
 * so a MutationObserver is used as a fallback.
 */
function findAndSendImageUrl() {
    const currentUrl = window.location.href;
    const match = currentUrl.match(GYAZO_URL_PATTERN);

    if (!match) return;

    const gyazoId = match[1];
    console.log(`[GyazoDumper] Gyazo page detected! ID: ${gyazoId}`);

    // Versuche sofort das Bild zu finden
    const imageUrl = getImageSrcFromDom();
    if (imageUrl) {
        sendDownloadRequest(imageUrl, gyazoId);
        return;
    }

    // Image not yet in DOM -> start MutationObserver
    console.log("[GyazoDumper] Image not yet in DOM, waiting for load...");

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
        console.warn("[GyazoDumper] Timeout: Image could not be found.");
    }, 15000);
}

/**
 * Searches the DOM for the image element and returns the src URL.
 * Checks if the src is a valid image URL (not a stale one from SPA navigation).
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
 * Sends the found image URL to the Background Service Worker.
 */
function sendDownloadRequest(imageUrl, gyazoId) {
    console.log(`[GyazoDumper] Sending download request for: ${imageUrl}`);

    chrome.runtime.sendMessage(
        {
            action: "downloadGyazoImage",
            imageUrl: imageUrl,
            gyazoId: gyazoId
        },
        (response) => {
            if (chrome.runtime.lastError) {
                console.error("[GyazoDumper] Error sending message:", chrome.runtime.lastError.message);
            } else if (response && response.success) {
                if (response.skipped) {
                    console.log(`[GyazoDumper] Image already exists, skipped.`);
                } else {
                    console.log(`[GyazoDumper] Download started!`);
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

// CSS is injected into the page once
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
 * Shows a small "Picture Saved!" banner.
 * Slides in from the right, fades in, rests, fades out.
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

    // Remove banner after animation ends
    banner.addEventListener("animationend", () => {
        banner.remove();
    });
}

// Initial processing on page load
lastProcessedUrl = window.location.href;
findAndSendImageUrl();
