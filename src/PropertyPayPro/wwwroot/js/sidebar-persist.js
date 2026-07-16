// Sidebar behavior:
//   • Desktop (>=992px): the burger toggles Mophy's .menu-toggle on
//     #main-wrapper (full sidebar ↔ icons-only). The collapsed preference
//     is persisted to localStorage so it survives page loads.
//   • Tablet / phone (<992px): the burger toggles .mobile-menu-open on
//     #main-wrapper — the sidebar slides in as an overlay drawer with a
//     backdrop. The desktop collapsed preference is ignored here so the
//     drawer always starts closed.
(function () {
    var KEY = 'pps-sidebar-collapsed';
    var MOBILE_BREAKPOINT = 992;

    function isMobile() { return window.innerWidth < MOBILE_BREAKPOINT; }

    function applyInitial() {
        try {
            var wrapper = document.getElementById('main-wrapper');
            if (!wrapper) return;
            if (isMobile()) {
                // Never carry a desktop "collapsed" preference into mobile:
                // that would render the sidebar as an unexpected open drawer.
                wrapper.classList.remove('menu-toggle', 'mobile-menu-open');
                document.documentElement.classList.remove('menu-toggle');
                return;
            }
            if (localStorage.getItem(KEY) === '1') {
                wrapper.classList.add('menu-toggle');
                document.documentElement.classList.add('menu-toggle');
            } else {
                wrapper.classList.remove('menu-toggle');
                document.documentElement.classList.remove('menu-toggle');
            }
        } catch (e) { /* localStorage unavailable — ignore */ }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyInitial);
    } else {
        applyInitial();
    }

    // Intercept the burger click in the capture phase so we can preempt
    // Mophy's default handler at mobile widths without ripping it out.
    document.addEventListener('click', function (e) {
        var trigger = e.target && e.target.closest && e.target.closest('.nav-control');
        if (!trigger) return;

        var wrapper = document.getElementById('main-wrapper');
        if (!wrapper) return;

        if (isMobile()) {
            // Prevent Mophy from toggling .menu-toggle (that would give us
            // the desktop "collapse to icons" behavior instead of a drawer).
            e.stopImmediatePropagation();
            e.preventDefault();
            wrapper.classList.toggle('mobile-menu-open');
            document.querySelectorAll('.hamburger').forEach(function (h) {
                h.classList.toggle('is-active');
            });
            return;
        }

        // Desktop: let Mophy's own handler flip .menu-toggle, then persist.
        setTimeout(function () {
            try {
                var collapsed = wrapper.classList.contains('menu-toggle');
                localStorage.setItem(KEY, collapsed ? '1' : '0');
                if (collapsed) {
                    document.documentElement.classList.add('menu-toggle');
                } else {
                    document.documentElement.classList.remove('menu-toggle');
                }
            } catch (err) { /* ignore */ }
        }, 0);
    }, true);

    // Tap the backdrop to close the drawer.
    document.addEventListener('click', function (e) {
        if (!isMobile()) return;
        var wrapper = document.getElementById('main-wrapper');
        if (!wrapper || !wrapper.classList.contains('mobile-menu-open')) return;
        // A click inside the sidebar or on the burger should NOT close.
        if (e.target.closest('.deznav') || e.target.closest('.nav-control')) return;
        wrapper.classList.remove('mobile-menu-open');
        document.querySelectorAll('.hamburger.is-active').forEach(function (h) {
            h.classList.remove('is-active');
        });
    });

    // Re-evaluate when viewport crosses the breakpoint (rotate, resize).
    window.addEventListener('resize', applyInitial);
})();
