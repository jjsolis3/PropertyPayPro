/* PropertyPayPro reusable toast helper.
   Usage:
     PPS.toast.success('Saved.');
     PPS.toast.warn('Allocated more than payment.');
     PPS.toast.error('Could not connect.');
     PPS.toast.info('Remainder held as credit.');
*/
(function () {
    function ensureContainer() {
        let container = document.getElementById('pps-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'pps-toast-container';
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '1090';
            document.body.appendChild(container);
        }
        return container;
    }

    function show(message, variant, delay) {
        const container = ensureContainer();
        const wrap = document.createElement('div');
        wrap.className = `toast align-items-center text-bg-${variant} border-0 shadow-sm`;
        wrap.setAttribute('role', 'alert');
        wrap.setAttribute('aria-live', 'assertive');
        wrap.setAttribute('aria-atomic', 'true');
        wrap.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>`;
        container.appendChild(wrap);
        const toast = new bootstrap.Toast(wrap, { delay: delay ?? 4000 });
        wrap.addEventListener('hidden.bs.toast', () => wrap.remove());
        toast.show();
        return toast;
    }

    window.PPS = window.PPS || {};
    window.PPS.toast = {
        success: (m, d) => show(m, 'success', d),
        info:    (m, d) => show(m, 'info', d),
        warn:    (m, d) => show(m, 'warning', d ?? 6000),
        error:   (m, d) => show(m, 'danger', d ?? 8000),
    };
})();
