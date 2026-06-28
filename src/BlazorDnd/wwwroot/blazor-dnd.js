// BlazorDnd HTML5 backend interop.
// Blazor handles the actual drag events natively (ondragstart/ondrag/ondrop/etc).
// This module covers the things the managed event args cannot do:
//   1) read an element's bounding rectangle (for source-relative offsets),
//   2) customize/hide the native drag image (for custom drag layers),
//   3) configure dataTransfer effectAllowed (for copy vs move drop effects), and
//   4) read native drop payloads (files/text/url/html) via dataTransfer.getData.

const EMPTY_IMG_ID = "__blazor_dnd_empty_img__";

function getEmptyImage() {
    let img = document.getElementById(EMPTY_IMG_ID);
    if (!img) {
        img = new Image();
        img.id = EMPTY_IMG_ID;
        img.src =
            "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
        img.style.position = "absolute";
        img.style.top = "-9999px";
        img.style.left = "-9999px";
        document.body.appendChild(img);
    }
    return img;
}

export function getBoundingClientRect(element) {
    if (!element) return null;
    const r = element.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
}

// Replaces the native drag image with a transparent pixel (custom drag layers).
export function useEmptyDragPreview(element) {
    if (!element) return null;
    const handler = (e) => {
        if (e.dataTransfer) {
            try {
                e.dataTransfer.setDragImage(getEmptyImage(), 0, 0);
            } catch {
                /* ignore */
            }
        }
    };
    element.addEventListener("dragstart", handler);
    return { dispose: () => element.removeEventListener("dragstart", handler) };
}

// Seeds dataTransfer (required by Firefox to start a drag) and sets the
// effectAllowed, which drives the copy/move cursor.
export function configureDragSource(element, effectAllowed) {
    if (!element) return null;
    const handler = (e) => {
        if (!e.dataTransfer) return;
        try {
            if (e.dataTransfer.types.length === 0) {
                e.dataTransfer.setData("application/blazor-dnd", "1");
            }
            e.dataTransfer.effectAllowed = effectAllowed || "move";
        } catch {
            /* ignore */
        }
    };
    element.addEventListener("dragstart", handler);
    return { dispose: () => element.removeEventListener("dragstart", handler) };
}

// Reads native drop payloads (files/text/url/html) and forwards them to .NET.
// dataTransfer.getData is only valid during the drop event, hence this listener.
export function registerNativeDrop(element, dotNetRef) {
    if (!element) return null;
    const onDragOver = (e) => {
        // allow native drops (e.g. files) so the browser doesn't open them
        if (e.dataTransfer && (e.dataTransfer.types || []).length) {
            e.preventDefault();
        }
    };
    const onDrop = async (e) => {
        if (!e.dataTransfer) return;
        const types = Array.from(e.dataTransfer.types || []);
        if (!types.length) return;
        e.preventDefault();
        const data = {};
        for (const t of types) {
            if (t === "Files") continue;
            try { data[t] = e.dataTransfer.getData(t); } catch { /* ignore */ }
        }
        const files = e.dataTransfer.files
            ? Array.from(e.dataTransfer.files).map((f) => ({
                  name: f.name,
                  size: f.size,
                  type: f.type,
              }))
            : [];
        await dotNetRef.invokeMethodAsync("HandleNativeDrop", { types, data, files });
    };
    element.addEventListener("dragover", onDragOver);
    element.addEventListener("drop", onDrop);
    return {
        dispose: () => {
            element.removeEventListener("dragover", onDragOver);
            element.removeEventListener("drop", onDrop);
        },
    };
}
