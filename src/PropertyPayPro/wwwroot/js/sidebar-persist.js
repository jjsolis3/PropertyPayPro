// Remember whether the sidebar is collapsed across page loads.
// The Mophy theme toggles a "menu-toggle" class on <body> when the user
// clicks .nav-control. Persist that state to localStorage and re-apply
// it on the next page load so refreshes/navigation don't reset it.
(function () {
    var KEY = 'pps-sidebar-collapsed';

    // Save the current state after Mophy's own click handler has flipped
    // the class. setTimeout(0) queues us after the theme's handler.
    document.addEventListener('click', function (e) {
        var target = e.target && e.target.closest && e.target.closest('.nav-control');
        if (!target) return;
        setTimeout(function () {
            try {
                var collapsed = document.body.classList.contains('menu-toggle');
                localStorage.setItem(KEY, collapsed ? '1' : '0');
            } catch (err) { /* localStorage unavailable — ignore */ }
        }, 0);
    });
})();
