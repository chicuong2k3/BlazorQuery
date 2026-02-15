export function addFocusListener(dotNetRef) {
    const handler = () => {
        const isFocused = document.visibilityState === "visible";
        dotNetRef.invokeMethodAsync("OnFocusChanged", isFocused);
    };
    document.addEventListener("visibilitychange", handler);
    return {
        dispose: () => document.removeEventListener("visibilitychange", handler)
    };
}

export function addOnlineListener(dotNetRef) {
    const onlineHandler = () => dotNetRef.invokeMethodAsync("OnOnlineStatusChanged", true);
    const offlineHandler = () => dotNetRef.invokeMethodAsync("OnOnlineStatusChanged", false);
    window.addEventListener("online", onlineHandler);
    window.addEventListener("offline", offlineHandler);
    return {
        dispose: () => {
            window.removeEventListener("online", onlineHandler);
            window.removeEventListener("offline", offlineHandler);
        }
    };
}

export function isOnline() {
    return navigator.onLine;
}

export function isFocused() {
    return document.visibilityState === "visible";
}
