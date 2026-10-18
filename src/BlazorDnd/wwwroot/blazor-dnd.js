// BlazorDnd HTML5 backend interop.
// Blazor raises the managed drag events (ondragstart/ondragover/ondrop). This module covers what
// managed handlers cannot do, because they cannot touch the live DataTransfer and (in Blazor
// Server) do not run while the native event is being dispatched:
//   1) configure a drag at dragstart: drag data, effectAllowed, drag image and drag handles,
//   2) track the pointer during a drag (Firefox reports 0,0 in 'drag' events),
//   3) report the end of a drag, even when the source element was removed from the DOM,
//   4) track hover per drop target in a way that survives re-rendered/removed child nodes,
//   5) give drop-effect feedback and read native drop payloads (files/text/url/html),
//   6) read an element's bounding rectangle.

// Marks drags started by a registered source; the handler id follows the prefix.
const SOURCE_TYPE_PREFIX = "application/x-blazor-dnd-";
const HANDLE_ATTRIBUTE = "data-blazor-dnd-handle";
const EMPTY_IMAGE_SRC =
    "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

// Firefox can raise a mousemove right as a drag starts, so mouse movement is only taken as
// "the drag is over" after this delay (the same approach as React DnD's HTML5 backend).
const SOURCE_REMOVED_CHECK_DELAY = 1000;

const claimedEvents = new WeakSet(); // dragstart events already handled by an inner source
const dropEffectStates = new WeakMap(); // dragenter/dragover event -> nested targets' decision
const targets = new Set();
let emptyImage = null;
let session = null; // the in-app drag in progress, started by one of this document's sources
let cancelRequested = false;

// Runs after Blazor's document-level listener, i.e. after the managed dragstart handler.
window.addEventListener("dragstart", (e) => {
    // .NET calls cancelDrag() synchronously while dragstart is being dispatched (WebAssembly)
    if (cancelRequested) {
        cancelRequested = false;
        e.preventDefault();
        endSession(false);
        return;
    }
    // other code cancelled the drag: no dragend will follow
    if (e.defaultPrevented && session && session.startEvent === e) endSession(true);
});

function getEmptyImage() {
    if (!emptyImage) {
        emptyImage = new Image();
        emptyImage.src = EMPTY_IMAGE_SRC;
    }
    return emptyImage;
}

function typesOf(dataTransfer) {
    return dataTransfer && dataTransfer.types ? Array.from(dataTransfer.types) : [];
}

function isInAppDrag(dataTransfer) {
    return !!session || typesOf(dataTransfer).some((t) => t.startsWith(SOURCE_TYPE_PREFIX));
}

// .NET callbacks are fire-and-forget; a disposed component or a dropped circuit must not
// surface as an unhandled promise rejection.
function invoke(dotNetRef, method, ...args) {
    try {
        dotNetRef.invokeMethodAsync(method, ...args).catch(() => { });
    } catch {
        /* ignore */
    }
}

export function getBoundingClientRect(element) {
    if (!element) return null;
    const r = element.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
}

// ---- drag sources -------------------------------------------------------------------------

function findHandle(node, root) {
    for (; node && node !== root; node = node.parentNode) {
        if (node.nodeType === Node.ELEMENT_NODE && node.hasAttribute(HANDLE_ATTRIBUTE)) {
            return node;
        }
    }
    return null;
}

// options: { id, canDrag, useHandle, useEmptyPreview, effectAllowed }
export function registerDragSource(element, dotNetRef, options) {
    if (!element) return null;
    const source = { element, dotNetRef, options: {}, rect: null };

    const configure = (o) => {
        source.options = o || {};
        // decode the transparent image before the first drag needs it
        if (source.options.useEmptyPreview) getEmptyImage();
    };

    const onDragStart = (e) => {
        // dragstart bubbles: the innermost registered source decides, its ancestors stay out
        // of it (the managed handler stops propagation the same way).
        if (claimedEvents.has(e)) return;
        claimedEvents.add(e);

        const o = source.options;
        const dataTransfer = e.dataTransfer;
        if (!dataTransfer || !o.canDrag) return;
        const handle = o.useHandle ? findHandle(e.target, element) : null;
        if (o.useHandle && !handle) return;

        // measured before anything re-renders; read by .NET as the source's initial offset
        const r = element.getBoundingClientRect();
        source.rect = { left: r.left, top: r.top, width: r.width, height: r.height };
        try {
            // marks the drag as ours (Firefox also requires some data to start a drag)
            dataTransfer.setData(SOURCE_TYPE_PREFIX + o.id, o.id);
            dataTransfer.effectAllowed = o.effectAllowed || "move";
            if (o.useEmptyPreview) {
                dataTransfer.setDragImage(getEmptyImage(), 0, 0);
            } else if (handle) {
                // the browser would picture just the handle; show the whole source instead
                dataTransfer.setDragImage(element, e.clientX - r.left, e.clientY - r.top);
            }
        } catch {
            /* ignore */
        }
        beginSession(source, e);
    };

    const onDragEnd = () => {
        if (session && session.source === source) endSession(true);
    };

    configure(options);
    // Element-level listeners still fire after the element was removed from the DOM, whereas
    // Blazor's document-level listener never sees events of detached elements.
    element.addEventListener("dragstart", onDragStart);
    element.addEventListener("dragend", onDragEnd);

    return {
        update: configure,
        getDragStartRect: () => source.rect,
        cancelDrag: () => {
            if (!session || session.source !== source) return;
            cancelRequested = true;
            setTimeout(() => (cancelRequested = false), 0);
        },
        dispose: () => {
            element.removeEventListener("dragstart", onDragStart);
            element.removeEventListener("dragend", onDragEnd);
            if (session && session.source === source) endSession(false);
        },
    };
}

function beginSession(source, startEvent) {
    if (session) endSession(true); // the previous drag never reported its end

    const s = { source, startEvent, x: 0, y: 0, sentX: NaN, sentY: NaN, frame: 0, timer: 0 };
    s.onDragOver = (e) => {
        s.x = e.clientX;
        s.y = e.clientY;
        if (!s.frame) {
            s.frame = requestAnimationFrame(() => {
                s.frame = 0;
                if (session !== s || (s.x === s.sentX && s.y === s.sentY)) return;
                s.sentX = s.x;
                s.sentY = s.y;
                invoke(source.dotNetRef, "HandleDragMove", s.x, s.y);
            });
        }
    };
    // Browsers may never raise 'dragend' for a source that was removed from the DOM (e.g. by
    // the re-render a drop causes), so also end the drag after a drop or once the mouse moves.
    s.onDrop = () => setTimeout(() => endIfSourceRemoved(s), 0);
    s.onMouseMove = () => endIfSourceRemoved(s);

    document.addEventListener("dragover", s.onDragOver, true);
    document.addEventListener("drop", s.onDrop, true);
    s.timer = setTimeout(
        () => window.addEventListener("mousemove", s.onMouseMove, true),
        SOURCE_REMOVED_CHECK_DELAY);
    session = s;
}

function endIfSourceRemoved(s) {
    if (session === s && !s.source.element.isConnected) endSession(true);
}

function endSession(notify) {
    const s = session;
    if (!s) return;
    session = null;
    document.removeEventListener("dragover", s.onDragOver, true);
    document.removeEventListener("drop", s.onDrop, true);
    window.removeEventListener("mousemove", s.onMouseMove, true);
    clearTimeout(s.timer);
    if (s.frame) cancelAnimationFrame(s.frame);
    for (const target of targets) target.reset();
    if (notify) invoke(s.source.dotNetRef, "HandleDragEnd");
}

// ---- drop targets -------------------------------------------------------------------------

// Native drags over a text field or editable content are left to the browser, so text can still
// be dropped into inputs placed inside a drop target.
function isNativeDragOverEditable(e) {
    if (isInAppDrag(e.dataTransfer)) return false;
    let el = e.target;
    if (el && el.nodeType !== Node.ELEMENT_NODE) el = el.parentElement;
    return !!el && (el.isContentEditable || el.tagName === "INPUT" || el.tagName === "TEXTAREA");
}

// dragenter/dragover bubble through nested drop targets. The innermost target that can drop
// accepts the event; outer targets must not undo that decision.
function applyDropEffect(e, canDrop) {
    const dataTransfer = e.dataTransfer;
    if (!dataTransfer) return;
    let state = dropEffectStates.get(e);
    if (!state) dropEffectStates.set(e, (state = { accepted: false, rejected: false }));
    if (state.accepted) return;
    if (isNativeDragOverEditable(e)) {
        state.accepted = true; // the editable element handles it, untouched
        return;
    }
    e.preventDefault();
    if (canDrop) {
        state.accepted = true;
        // an inner target rejected the event first: restore an effect the source allows
        if (state.rejected) dataTransfer.dropEffect = defaultDropEffect(dataTransfer.effectAllowed);
    } else {
        state.rejected = true;
        dataTransfer.dropEffect = "none"; // no-drop cursor; the drop event will not fire
    }
}

function defaultDropEffect(effectAllowed) {
    switch (effectAllowed) {
        case "move":
        case "linkMove":
            return "move";
        case "link":
            return "link";
        default:
            return "copy";
    }
}

// options: { canDrop (in-app drag), nativeTypes (DataTransfer types of accepted native drags) }
export function registerDropTarget(element, dotNetRef, options) {
    if (!element) return null;
    const target = { options: options || {}, entered: new Set(), over: false };

    // Hover is tracked per node that raised dragenter. Nodes removed or moved out of the target
    // since (e.g. re-rendered by Blazor) never raise dragleave, so they are pruned here.
    const refresh = (dataTransfer) => {
        for (const node of target.entered) {
            if (!node.isConnected || !element.contains(node)) target.entered.delete(node);
        }
        const over = target.entered.size > 0;
        if (over !== target.over) {
            target.over = over;
            invoke(dotNetRef, "HandleHoverChanged", over, typesOf(dataTransfer));
        }
    };

    const canDrop = (dataTransfer) => {
        if (isInAppDrag(dataTransfer)) return !!session && !!target.options.canDrop;
        const types = typesOf(dataTransfer);
        return (target.options.nativeTypes || []).some((t) => types.includes(t));
    };

    const onDragEnter = (e) => {
        target.entered.add(e.target);
        refresh(e.dataTransfer);
        applyDropEffect(e, canDrop(e.dataTransfer));
    };

    const onDragOver = (e) => {
        // the hover may have started before this target was registered
        if (target.entered.size === 0) target.entered.add(e.target);
        refresh(e.dataTransfer);
        applyDropEffect(e, canDrop(e.dataTransfer));
    };

    const onDragLeave = (e) => {
        target.entered.delete(e.target);
        refresh(e.dataTransfer);
    };

    const onDrop = (e) => {
        const dataTransfer = e.dataTransfer;
        // No dragleave follows a drop. Reset silently: managed OnDrop handlers may still query
        // IsOver, and the managed state is reset when the drag ends.
        target.entered.clear();
        target.over = false;
        if (isNativeDragOverEditable(e)) return;
        e.preventDefault(); // never let the browser open or navigate to dropped content
        // in-app drops are handled by the managed ondrop handler
        if (!dataTransfer || isInAppDrag(dataTransfer) || !canDrop(dataTransfer)) return;

        // getData only works during the drop event, hence it is read here
        const types = typesOf(dataTransfer);
        const data = {};
        for (const t of types) {
            if (t === "Files") continue;
            try {
                data[t] = dataTransfer.getData(t);
            } catch {
                /* ignore */
            }
        }
        const files = Array.from(dataTransfer.files || [], (f) => ({
            name: f.name,
            size: f.size,
            type: f.type,
        }));
        invoke(dotNetRef, "HandleNativeDrop", { types, data, files });
    };

    target.reset = () => {
        target.entered.clear();
        target.over = false;
    };

    element.addEventListener("dragenter", onDragEnter);
    element.addEventListener("dragover", onDragOver);
    element.addEventListener("dragleave", onDragLeave);
    element.addEventListener("drop", onDrop);
    targets.add(target);

    return {
        update: (o) => {
            target.options = o || {};
        },
        dispose: () => {
            element.removeEventListener("dragenter", onDragEnter);
            element.removeEventListener("dragover", onDragOver);
            element.removeEventListener("dragleave", onDragLeave);
            element.removeEventListener("drop", onDrop);
            targets.delete(target);
        },
    };
}
