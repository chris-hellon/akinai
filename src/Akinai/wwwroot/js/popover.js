// Closes popovers when you click anywhere outside them.
//
// This replaces the transparent full-screen backdrops the popovers used to
// rely on. A backdrop has to sit above everything to catch the click, which
// means it also *swallows* it — so clicking a nav link would close the menu
// without following the link, and you'd have to click twice. (In practice the
// backdrops sat below the sidebar, so nav clicks missed them entirely and the
// popover just stayed open.)
//
// A document listener has neither problem: it sees every click, and because it
// never calls preventDefault the click still reaches whatever you actually
// clicked on.

const handlers = new Map();
let nextId = 1;

export function register(element, dotNetRef) {
    const id = nextId++;

    const handler = (event) => {
        // composedPath handles clicks that originate inside a shadow root;
        // contains() alone would miss those.
        const path = event.composedPath ? event.composedPath() : [event.target];
        if (element && (path.includes(element) || element.contains(event.target))) return;
        dotNetRef.invokeMethodAsync('OnOutsideClick');
    };

    // Registered on the next tick. The click that opened the popover is still
    // propagating right now — without this it would immediately close again.
    const timer = setTimeout(() => {
        document.addEventListener('pointerdown', handler, true);
    }, 0);

    handlers.set(id, { handler, timer });
    return id;
}

export function unregister(id) {
    const entry = handlers.get(id);
    if (!entry) return;
    clearTimeout(entry.timer);
    document.removeEventListener('pointerdown', entry.handler, true);
    handlers.delete(id);
}
