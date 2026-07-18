// Sidebar behavior — same push-content model at every viewport width:
//   • Burger toggles Mophy's .menu-toggle on #main-wrapper. Same class
//     Mophy already toggles, so no fight with the theme.
//     - With .menu-toggle: sidebar = 6rem icon strip, content margin
//       = 6rem.
//     - Without .menu-toggle: sidebar = 16.5rem full-text, content
//       margin = 16.5rem.
//     Both states push content aside — no overlay, no backdrop.
//   • The collapsed preference is persisted to localStorage so it
//     survives page navigations.
//   • Mobile viewports (<992px) default to COLLAPSED when there is no
//     stored preference — a 16.5rem sidebar on a 375px phone would
//     leave almost no room for content. Once a user explicitly toggles
//     on mobile, their choice sticks.
(function () {
    var KEY = 'pps-sidebar-collapsed';
    var MOBILE_BREAKPOINT = 992;

    function isMobile() { return window.innerWidth < MOBILE_BREAKPOINT; }

    function setCollapsed(collapsed) {
        var wrapper = document.getElementById('main-wrapper');
        if (!wrapper) return;
        wrapper.classList.toggle('menu-toggle', collapsed);
        document.documentElement.classList.toggle('menu-toggle', collapsed);
        document.querySelectorAll('.hamburger').forEach(function (h) {
            h.classList.toggle('is-active', collapsed);
        });
    }

    function applyInitial() {
        try {
            var stored = localStorage.getItem(KEY);
            var collapsed;
            if (stored === '1') collapsed = true;
            else if (stored === '0') collapsed = false;
            else collapsed = isMobile(); // no preference yet
            setCollapsed(collapsed);
        } catch (e) { /* localStorage unavailable — ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyInitial);
    } else {
        applyInitial();
    }

    // Burger click: let Mophy's own handler flip .menu-toggle on
    // #main-wrapper, then persist the new state and keep <html> in
    // sync so the head-inline restore script gets it right next load.
    document.addEventListener('click', function (e) {
        var trigger = e.target && e.target.closest && e.target.closest('.nav-control');
        if (!trigger) return;
        setTimeout(function () {
            try {
                var wrapper = document.getElementById('main-wrapper');
                if (!wrapper) return;
                var collapsed = wrapper.classList.contains('menu-toggle');
                document.documentElement.classList.toggle('menu-toggle', collapsed);
                localStorage.setItem(KEY, collapsed ? '1' : '0');
            } catch (err) { /* ignore */ }
        }, 0);
    });
})();
