// Auto-enhance any <table class="js-datatable"> with search, sort, pagination.
// Opt out of sorting on a column by adding class="no-sort" to its <th>.
// Override the default order by adding data-order='[[colIndex, "asc|desc"]]' on the <table>.
(function () {
    if (typeof jQuery === 'undefined' || typeof jQuery.fn === 'undefined' || typeof jQuery.fn.DataTable === 'undefined') {
        return;
    }
    var $ = jQuery;

    // Currency sort: strip $ and , so "$1,500.00" sorts as 1500.
    var currencyParse = function (s) {
        if (s === null || s === undefined) return 0;
        var t = String(s).replace(/[^0-9.\-]/g, '');
        var n = parseFloat(t);
        return isNaN(n) ? 0 : n;
    };
    $.fn.dataTable.ext.type.order['currency-pre'] = currencyParse;
    // Detect currency cells: starts with $ or -$ optionally followed by digits.
    $.fn.dataTable.ext.type.detect.unshift(function (d) {
        if (typeof d !== 'string') return null;
        return /^-?\$[\d,]+(\.\d+)?$|^\(\$[\d,]+(\.\d+)?\)$/.test(d.trim()) ? 'currency' : null;
    });

    $(function () {
        $('table.js-datatable').each(function () {
            var $t = $(this);
            if ($.fn.dataTable.isDataTable($t)) return;

            var columnDefs = [];
            $t.find('thead th').each(function (i, th) {
                var $th = $(th);
                var isEmpty = $.trim($th.text()) === '' && $th.find('input,button,a').length === 0;
                if ($th.hasClass('no-sort') || isEmpty) {
                    columnDefs.push({ targets: i, orderable: false, searchable: false });
                    $th.addClass('no-sort');
                }
            });

            // Pull optional order override from data-order='[[2,"desc"]]'
            var order = $t.data('order');
            if (typeof order === 'string') {
                try { order = JSON.parse(order); } catch (e) { order = undefined; }
            }

            $t.DataTable({
                pageLength: parseInt($t.data('page-length') || '25', 10),
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                order: order || [],
                columnDefs: columnDefs,
                language: {
                    search: 'Search:',
                    lengthMenu: 'Show _MENU_',
                    info: 'Showing _START_–_END_ of _TOTAL_',
                    infoEmpty: 'No entries',
                    infoFiltered: '(filtered from _MAX_)',
                    zeroRecords: 'No matching records',
                    paginate: { first: '«', previous: '‹', next: '›', last: '»' }
                }
            });
        });
    });
})();
