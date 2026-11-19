// Jumps a recipe video to a time and plays it from there.
// YouTube and Vimeo players are controlled through their postMessage APIs, so no extra scripts are loaded.
export function seek(container, seconds) {
    if (!container) {
        return;
    }

    const video = container.querySelector("video");
    const frame = container.querySelector("iframe");

    if (video) {
        video.currentTime = seconds;
        video.play().catch(() => { /* The browser blocked playback; the time is still set. */ });
    } else if (frame && frame.contentWindow) {
        const src = frame.src;
        if (src.startsWith("https://www.youtube.com/")) {
            const send = (func, args) => frame.contentWindow.postMessage(
                JSON.stringify({ event: "command", func, args }), "https://www.youtube.com");
            send("seekTo", [seconds, true]);
            send("playVideo", []);
        } else if (src.startsWith("https://player.vimeo.com/")) {
            const send = (method, value) => frame.contentWindow.postMessage(
                JSON.stringify(value === undefined ? { method } : { method, value }), "https://player.vimeo.com");
            send("setCurrentTime", seconds);
            send("play");
        }
    }

    // On narrow screens the video is far above the steps.
    const rect = container.getBoundingClientRect();
    if (rect.top < 0 || rect.bottom > window.innerHeight) {
        container.scrollIntoView({ behavior: "smooth", block: "center" });
    }
}
