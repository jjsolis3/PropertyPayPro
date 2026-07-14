// Remember whether the sidebar is collapsed across page loads.
// Mophy toggles the "menu-toggle" class on #main-wrapper (not <body>)
// when the user clicks .nav-control. We keep <html> and #main-wrapper
// in sync so:
//   • Mophy's click handler works correctly (it toggles #main-wrapper),
//   • the head inline script can render the initial collapsed state
//     without a flash (via the class on <html>, whose CSS ancestor
//     selector matches all the same rules).
(function () {
    var KEY = 'pps-sidebar-collapsed';

    function apply() {
        try {
            var wrapper = document.getElementById('main-wrapper');
            if (!wrapper) return;
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
        document.addEventListener('DOMContentLoaded', apply);
    } else {
        apply();
    }

    // After Mophy's own click handler flips the class on #main-wrapper,
    // read the new state and persist it. setTimeout(0) queues us after
    // Mophy's handler.
    document.addEventListener('click', function (e) {
        var target = e.target && e.target.closest && e.target.closest('.nav-control');
        if (!target) return;
        setTimeout(function () {
            try {
                var wrapper = document.getElementById('main-wrapper');
                if (!wrapper) return;
                var collapsed = wrapper.classList.contains('menu-toggle');
                localStorage.setItem(KEY, collapsed ? '1' : '0');
                // Keep <html> in sync so the next page load's head
                // script sets the right initial state.
                if (collapsed) {
                    document.documentElement.classList.add('menu-toggle');
                } else {
                    document.documentElement.classList.remove('menu-toggle');
                }
            } catch (err) { /* ignore */ }
        }, 0);
    });
})();
