// Cooking mode: full screen, keep the screen awake, and stop the page behind from scrolling.
// The page is told when either changes (the user can leave full screen with Esc, and the
// browser drops the wake lock whenever the tab is hidden), so its buttons stay accurate.

let session = null;

const fullscreenElement = () => document.fullscreenElement ?? document.webkitFullscreenElement ?? null;

export function isFullscreenSupported() {
    return !!(document.fullscreenEnabled || document.webkitFullscreenEnabled);
}

export async function enter(dotnet) {
    if (session) {
        return capabilities();
    }

    session = { dotnet, lock: null };
    document.documentElement.classList.add("cooking-mode-open");

    session.onFullscreenChange = () => notify("OnFullscreenChanged", fullscreenElement() !== null);
    session.onVisibilityChange = () => {
        if (document.visibilityState === "visible") {
            requestWakeLock();
        }
    };
    session.onKeyDown = e => {
        // Esc while in full screen is used by the browser to leave full screen; otherwise it leaves cooking mode.
        if (e.key === "Escape" && fullscreenElement() === null) {
            notify("OnEscape");
        }
    };

    document.addEventListener("fullscreenchange", session.onFullscreenChange);
    document.addEventListener("webkitfullscreenchange", session.onFullscreenChange);
    document.addEventListener("visibilitychange", session.onVisibilityChange);
    document.addEventListener("keydown", session.onKeyDown);

    // Full screen must be asked for while the click that opened cooking mode still counts.
    await setFullscreen(true);
    await requestWakeLock();
    return capabilities();
}

export async function leave() {
    if (!session) {
        return;
    }

    const current = session;
    session = null;

    document.removeEventListener("fullscreenchange", current.onFullscreenChange);
    document.removeEventListener("webkitfullscreenchange", current.onFullscreenChange);
    document.removeEventListener("visibilitychange", current.onVisibilityChange);
    document.removeEventListener("keydown", current.onKeyDown);
    document.documentElement.classList.remove("cooking-mode-open");

    try {
        await current.lock?.release();
    } catch {
        // Already released.
    }

    await setFullscreen(false);
}

export async function setFullscreen(on) {
    try {
        if (on && fullscreenElement() === null && isFullscreenSupported()) {
            const root = document.documentElement;
            await (root.requestFullscreen
                ? root.requestFullscreen({ navigationUI: "hide" })
                : root.webkitRequestFullscreen());
        } else if (!on && fullscreenElement() !== null) {
            await (document.exitFullscreen ? document.exitFullscreen() : document.webkitExitFullscreen());
        }
    } catch {
        // The browser refused (for example, no recent click). Cooking mode still works without it.
    }

    return fullscreenElement() !== null;
}

function capabilities() {
    return {
        fullscreenSupported: isFullscreenSupported(),
        fullscreen: fullscreenElement() !== null,
        wakeLockSupported: "wakeLock" in navigator,
        screenAwake: !!session?.lock && !session.lock.released,
    };
}

async function requestWakeLock() {
    if (!session || !("wakeLock" in navigator) || document.visibilityState !== "visible") {
        return;
    }

    if (session.lock && !session.lock.released) {
        return;
    }

    try {
        const lock = await navigator.wakeLock.request("screen");
        if (!session) {
            await lock.release();
            return;
        }

        session.lock = lock;
        lock.addEventListener("release", () => notify("OnScreenAwakeChanged", false));
        notify("OnScreenAwakeChanged", true);
    } catch {
        // Refused, for example on battery saver or outside a secure (https) page.
        notify("OnScreenAwakeChanged", false);
    }
}

function notify(method, ...args) {
    session?.dotnet.invokeMethodAsync(method, ...args).catch(() => { /* The page has gone. */ });
}
