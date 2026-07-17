// Sidebar behavior across all viewport sizes:
//   • Burger toggles Mophy's .menu-toggle on #main-wrapper — same
//     class Mophy already toggles — flipping the sidebar between the
//     6rem icon strip (menu-toggle applied) and the 16.5rem full-text
//     sidebar (menu-toggle removed).
//   • On desktop (>=992px), the two states push content aside like
//     Mophy's default. The collapsed preference is persisted so it
//     survives navigation.
//   • On tablet/phone (<992px), site.css keeps content-body pinned to
//     the icon-strip gutter and turns the expanded state into a fixed
//     drawer with a backdrop. Mobile always LOADS collapsed regardless
//     of the desktop preference, so the drawer never pops open on its
//     own. Tapping the backdrop closes the drawer.
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
            if (isMobile()) {
                // Mobile always starts collapsed — icon strip visible,
                // drawer closed. The persisted desktop preference is
                // irrelevant here.
                setCollapsed(true);
                return;
            }
            setCollapsed(localStorage.getItem(KEY) === '1');
        } catch (e) { /* localStorage unavailable — ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyInitial);
    } else {
        applyInitial();
    }

    // Burger click: let Mophy's own handler flip .menu-toggle, then
    // persist (desktop only) and keep <html> in sync so the head-inline
    // restore script picks the right initial state on the next load.
    document.addEventListener('click', function (e) {
        var trigger = e.target && e.target.closest && e.target.closest('.nav-control');
        if (!trigger) return;
        setTimeout(function () {
            try {
                var wrapper = document.getElementById('main-wrapper');
                if (!wrapper) return;
                var collapsed = wrapper.classList.contains('menu-toggle');
                document.documentElement.classList.toggle('menu-toggle', collapsed);
                if (!isMobile()) {
                    localStorage.setItem(KEY, collapsed ? '1' : '0');
                }
            } catch (err) { /* ignore */ }
        }, 0);
    });

    // Backdrop tap on small screens: close the drawer. A tap inside the
    // sidebar itself, or on the burger, doesn't count.
    document.addEventListener('click', function (e) {
        if (!isMobile()) return;
        var wrapper = document.getElementById('main-wrapper');
        if (!wrapper) return;
        if (wrapper.classList.contains('menu-toggle')) return; // already collapsed
        if (e.target.closest('.deznav') || e.target.closest('.nav-control')) return;
        setCollapsed(true);
    });

    // Viewport crossing the breakpoint (rotate, resize): re-apply the
    // rule so the desktop preference doesn't leak into the mobile state
    // and vice versa.
    window.addEventListener('resize', applyInitial);
})();
