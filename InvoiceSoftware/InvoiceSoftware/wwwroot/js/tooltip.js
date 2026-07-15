// Lightweight, dependency-free tooltip controller.
// One shared floating element + delegated listeners handle every element that
// carries a `data-tooltip` attribute (and auto-upgrades any native `title` too),
// so it works for content Blazor adds/removes at any time.
(function () {
    if (window.__appTooltipInit) return;
    window.__appTooltipInit = true;

    var GAP = 8;      // space between target and tooltip
    var EDGE = 6;     // min distance from viewport edge
    var current = null;
    var tip, showTimer;

    function injectStyles() {
        var css =
            '.app-tooltip{position:fixed;z-index:10000;top:0;left:0;pointer-events:none;' +
            'background:#1c2530;color:#f8fafc;font-family:Inter,system-ui,-apple-system,sans-serif;' +
            'font-size:12px;line-height:1.35;font-weight:500;letter-spacing:.01em;padding:6px 9px;' +
            'border-radius:8px;box-shadow:0 8px 24px rgba(2,6,23,.28),0 2px 6px rgba(2,6,23,.18);' +
            'white-space:nowrap;opacity:0;transform:translateY(3px) scale(.96);' +
            'transition:opacity .14s ease,transform .14s ease}' +
            '.app-tooltip.show{opacity:1;transform:translateY(0) scale(1)}' +
            '.app-tooltip::after{content:"";position:absolute;width:7px;height:7px;background:inherit;' +
            'transform:rotate(45deg);box-shadow:inherit}' +
            '.app-tooltip[data-placement="top"]::after{bottom:-3px;left:var(--ax,50%);margin-left:-3.5px;box-shadow:none}' +
            '.app-tooltip[data-placement="bottom"]::after{top:-3px;left:var(--ax,50%);margin-left:-3.5px;box-shadow:none}' +
            '.app-tooltip[data-placement="left"]::after{right:-3px;top:var(--ay,50%);margin-top:-3.5px;box-shadow:none}' +
            '.app-tooltip[data-placement="right"]::after{left:-3px;top:var(--ay,50%);margin-top:-3.5px;box-shadow:none}' +
            'html.dark .app-tooltip{background:#eef2f6;color:#0b0e12;' +
            'box-shadow:0 8px 24px rgba(0,0,0,.5),0 2px 6px rgba(0,0,0,.35)}';
        var el = document.createElement('style');
        el.textContent = css;
        document.head.appendChild(el);
    }

    function ensureTip() {
        if (tip) return;
        injectStyles();
        tip = document.createElement('div');
        tip.className = 'app-tooltip';
        tip.setAttribute('role', 'tooltip');
        document.body.appendChild(tip);
    }

    // Upgrade a native title -> data-tooltip (once) so the browser's slow default
    // tooltip never appears, and keep the accessible name via aria-label.
    function resolveText(el) {
        if (el.hasAttribute('title')) {
            var t = el.getAttribute('title');
            el.removeAttribute('title');
            if (t) {
                el.setAttribute('data-tooltip', t);
                if (!el.getAttribute('aria-label')) el.setAttribute('aria-label', t);
            }
        }
        return el.getAttribute('data-tooltip');
    }

    function position(el) {
        var placement = el.getAttribute('data-tooltip-placement') || 'top';
        var r = el.getBoundingClientRect();
        var tw = tip.offsetWidth, th = tip.offsetHeight;
        var vw = window.innerWidth, vh = window.innerHeight;

        // Flip vertically if there isn't room.
        if (placement === 'top' && r.top - th - GAP < EDGE) placement = 'bottom';
        else if (placement === 'bottom' && r.bottom + th + GAP > vh - EDGE) placement = 'top';
        else if (placement === 'left' && r.left - tw - GAP < EDGE) placement = 'right';
        else if (placement === 'right' && r.right + tw + GAP > vw - EDGE) placement = 'left';

        var x, y;
        if (placement === 'top') { x = r.left + r.width / 2 - tw / 2; y = r.top - th - GAP; }
        else if (placement === 'bottom') { x = r.left + r.width / 2 - tw / 2; y = r.bottom + GAP; }
        else if (placement === 'left') { x = r.left - tw - GAP; y = r.top + r.height / 2 - th / 2; }
        else { x = r.right + GAP; y = r.top + r.height / 2 - th / 2; }

        var clampedX = Math.max(EDGE, Math.min(x, vw - tw - EDGE));
        var clampedY = Math.max(EDGE, Math.min(y, vh - th - EDGE));

        // Keep the arrow pointing at the target's centre after clamping.
        var centreX = r.left + r.width / 2 - clampedX;
        var centreY = r.top + r.height / 2 - clampedY;
        tip.style.setProperty('--ax', Math.max(8, Math.min(tw - 8, centreX)) + 'px');
        tip.style.setProperty('--ay', Math.max(8, Math.min(th - 8, centreY)) + 'px');

        tip.dataset.placement = placement;
        tip.style.left = clampedX + 'px';
        tip.style.top = clampedY + 'px';
    }

    function show(el, text) {
        ensureTip();
        current = el;
        tip.textContent = text;
        position(el);
        tip.classList.add('show');
    }

    function hide() {
        if (!tip) return;
        tip.classList.remove('show');
        current = null;
    }

    document.addEventListener('mouseover', function (e) {
        var el = e.target.closest && e.target.closest('[data-tooltip],[title]');
        if (!el) return;
        var text = resolveText(el);
        if (text && el !== current) show(el, text);
    });

    document.addEventListener('mouseout', function (e) {
        if (!current) return;
        var to = e.relatedTarget;
        if (to && current.contains && current.contains(to)) return;
        hide();
    });

    document.addEventListener('focusin', function (e) {
        var el = e.target.closest && e.target.closest('[data-tooltip],[title]');
        if (!el) return;
        var text = resolveText(el);
        if (text) show(el, text);
    });

    document.addEventListener('focusout', hide);
    // Dismiss promptly on interaction / layout change so it never lingers or drifts.
    document.addEventListener('click', hide, true);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') hide(); });
    window.addEventListener('scroll', hide, true);
    window.addEventListener('resize', hide);
})();
