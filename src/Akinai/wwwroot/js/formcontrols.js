// Keeps the custom Select's search box from submitting the surrounding <form>
// (implicit submit on Enter) or scrolling the page on arrow keys. Text entry is
// untouched — only navigation keys are prevented. Capture phase runs before the
// browser's implicit form submission.
document.addEventListener('keydown', function (e) {
    var t = e.target;
    if (t && t.classList && t.classList.contains('isel-search-input')) {
        if (e.key === 'Enter' || e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
        }
    }
}, true);

// Floating panel positioner for Select / DatePicker. Their popovers are
// position:absolute, so an ancestor with overflow:hidden (e.g. a card) clips
// them. We pin the panel with position:fixed relative to the trigger instead,
// which escapes any clipping ancestor, and flip above when there's no room below.
// On mobile (<=640px) the CSS bottom-sheet takes over, so we clear inline styles.
window.floatingPanel = (function () {
    // matchWidth: true for Select (options match the field width); false for
    // DatePicker (the calendar keeps its own fixed CSS width, independent of the input).
    function place(trigger, panel, matchWidth) {
        if (!trigger || !panel) return;
        if (window.innerWidth <= 640) {
            panel.style.position = panel.style.top = panel.style.bottom =
                panel.style.left = panel.style.right = panel.style.width = '';
            return;
        }
        var r = trigger.getBoundingClientRect();
        var gap = 6, margin = 8, vw = window.innerWidth, vh = window.innerHeight;
        panel.style.position = 'fixed';
        panel.style.right = 'auto';
        panel.style.bottom = 'auto';
        if (matchWidth) panel.style.width = Math.round(r.width) + 'px';
        // An ancestor with transform / filter / backdrop-filter (e.g. the blurred
        // sticky header) becomes the containing block for position:fixed, so our
        // fixed coords would be measured from that ancestor's box, not the viewport.
        // Probe the real origin by pinning to 0,0 and reading where it lands, then
        // subtract it — so the viewport-space target is honoured either way.
        panel.style.left = '0px';
        panel.style.top = '0px';
        var origin = panel.getBoundingClientRect();
        var offX = origin.left, offY = origin.top;
        // horizontal: align to the trigger's left, clamp so a fixed-width panel
        // (e.g. the calendar) never overflows the viewport edge.
        var pw = panel.offsetWidth;
        var left = r.left;
        if (left + pw > vw - margin) left = vw - margin - pw;
        if (left < margin) left = margin;
        panel.style.left = Math.round(left - offX) + 'px';
        // vertical: prefer opening downward (feels attached to the field). Only flip
        // above when the trigger is genuinely near the viewport bottom; if a downward
        // panel would run past the bottom, clamp it into view rather than flipping.
        var ph = panel.offsetHeight;
        var spaceBelow = vh - r.bottom;
        var top;
        if (spaceBelow >= ph + gap + margin || spaceBelow >= 260) {
            top = r.bottom + gap;
            if (top + ph > vh - margin) top = Math.max(margin, vh - margin - ph);
        } else {
            top = Math.max(margin, r.top - gap - ph);
        }
        panel.style.top = Math.round(top - offY) + 'px';
    }
    return {
        attach: function (trigger, panel, matchWidth) {
            if (!panel) return;
            var fn = function () { place(trigger, panel, matchWidth); };
            fn();
            window.addEventListener('scroll', fn, true);
            window.addEventListener('resize', fn);
            panel.__fp = fn;
        },
        detach: function (panel) {
            if (!panel || !panel.__fp) return;
            window.removeEventListener('scroll', panel.__fp, true);
            window.removeEventListener('resize', panel.__fp);
            panel.__fp = null;
        }
    };
})();
