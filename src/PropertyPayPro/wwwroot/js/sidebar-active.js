// The theme only highlights a sidebar link whose URL matches the page
// exactly, so sub-pages (/Notices/Edit/3, /Users/Invite, ?filter=...) leave
// the menu collapsed. When nothing matched, highlight the link whose path is
// the longest prefix of the current path and open its group, using the same
// classes the theme applies.
jQuery(function () {
    var $links = $('ul#menu a[href^="/"]');
    if ($links.filter('.mm-active').length) return;

    var path = window.location.pathname.replace(/\/+$/, '').toLowerCase();
    var best = null, bestLen = 0;
    $links.each(function () {
        var linkPath = this.pathname.replace(/\/+$/, '').toLowerCase();
        if (!linkPath) return; // dashboard "/" only matches exactly
        var isPrefix = path === linkPath || path.indexOf(linkPath + '/') === 0;
        if (isPrefix && linkPath.length > bestLen) {
            best = this;
            bestLen = linkPath.length;
        }
    });
    if (!best) return;

    var $el = $(best).addClass('mm-active').parent().addClass('mm-active');
    while ($el.is('li')) {
        $el = $el.parent().addClass('mm-show').parent().addClass('mm-active');
    }
});
