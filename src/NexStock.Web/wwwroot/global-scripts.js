/* Your Global Scripts */

// Suppress "Could not find localization source: AbpUi" console warnings.
// AbpUi keys are merged into the NexStock resource on the backend,
// but ABP's JS still looks for them under a separate "AbpUi" key.
// We pre-register the AbpUi namespace so the lookup succeeds silently.
(function () {
    var originalWarn = abp.log ? abp.log.warn : null;
    if (originalWarn) {
        abp.log.warn = function (msg) {
            if (msg && msg.indexOf && msg.indexOf('Could not find localization source: AbpUi') !== -1) {
                return; // swallow this specific warning
            }
            originalWarn.apply(abp.log, arguments);
        };
    }

    // Also pre-seed AbpUi values so actual lookups don't fail
    if (abp && abp.localization && abp.localization.values) {
        abp.localization.values['AbpUi'] = abp.localization.values['AbpUi'] || abp.localization.values['NexStock'] || {};
    }
})();

// Datatable localization overrides (Turkish)
$(function () {
    if (abp && abp.localization && abp.localization.values) {
        abp.localization.values['AbpUi'] = abp.localization.values['AbpUi'] || {};
        var ui = abp.localization.values['AbpUi'];
        ui['DatatableActionDropdownDefaultText'] = ui['DatatableActionDropdownDefaultText'] || 'İşlemler';
        ui['PagerShowMenuEntries'] = ui['PagerShowMenuEntries'] || '_MENU_ kayıt göster';
        ui['PagerInfo'] = ui['PagerInfo'] || 'Toplam _TOTAL_ kayıttan _START_ - _END_ arası gösteriliyor';
        ui['PagerInfoEmpty'] = ui['PagerInfoEmpty'] || 'Kayıt yok';
        ui['PagerFirst'] = ui['PagerFirst'] || 'İlk';
        ui['PagerPrevious'] = ui['PagerPrevious'] || 'Önceki';
        ui['PagerNext'] = ui['PagerNext'] || 'Sonraki';
        ui['PagerLast'] = ui['PagerLast'] || 'Son';
        ui['NoDataAvailableInDatatable'] = ui['NoDataAvailableInDatatable'] || 'Tabloda veri bulunamadı';
    }
});

// Capture browser and AJAX failures in the server logs and show the user a useful, traceable notice.
(function () {
    var reportedErrorCount = 0;
    var maximumReportedErrors = 5;

    function traceId() {
        var element = document.querySelector('meta[name="nexstock-correlation-id"]');
        return element ? element.getAttribute('content') : null;
    }

    function showError(message, requestTraceId) {
        var container = document.getElementById('NexStockErrorNotifications');
        if (!container) {
            container = document.createElement('div');
            container.id = 'NexStockErrorNotifications';
            container.className = 'nexstock-error-notifications';
            container.setAttribute('aria-live', 'assertive');
            document.body.appendChild(container);
        }

        var notification = document.createElement('div');
        notification.className = 'nexstock-error-notification';
        var title = document.createElement('strong');
        title.textContent = 'İşlem tamamlanamadı';
        var detail = document.createElement('span');
        detail.textContent = requestTraceId
            ? message + ' İzleme kodu: ' + requestTraceId
            : message;
        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'nexstock-error-close';
        close.setAttribute('aria-label', 'Bildirimi kapat');
        close.textContent = '×';
        close.addEventListener('click', function () { notification.remove(); });
        notification.append(title, detail, close);
        container.appendChild(notification);
        window.setTimeout(function () { notification.remove(); }, 10000);
    }

    function reportClientError(error, source) {
        if (reportedErrorCount >= maximumReportedErrors) {
            return;
        }

        reportedErrorCount += 1;
        var payload = {
            message: String(error && error.message ? error.message : error || 'Unknown client error'),
            source: source,
            url: window.location.href,
            stack: error && error.stack ? String(error.stack) : null
        };

        window.fetch('/api/diagnostics/client-errors', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            keepalive: true,
            body: JSON.stringify(payload)
        }).catch(function () { });
    }

    window.addEventListener('error', function (event) {
        reportClientError(event.error || event.message, event.filename || 'window.error');
        showError('Beklenmeyen bir ekran hatası oluştu.');
    });

    window.addEventListener('unhandledrejection', function (event) {
        reportClientError(event.reason, 'window.unhandledrejection');
        showError('Beklenmeyen bir işlem hatası oluştu.');
    });

    $(document).ajaxError(function (_event, xhr, settings) {
        if (settings.url && settings.url.indexOf('/api/diagnostics/client-errors') !== -1) {
            return;
        }

        var requestTraceId = xhr.getResponseHeader('X-Correlation-Id') || traceId();
        reportClientError(xhr.status + ' ' + xhr.statusText, settings.url || 'jquery.ajax');
        showError(xhr.status === 403 ? 'Bu işlem için yetkiniz yok.' : 'Sunucu isteği işleyemedi.', requestTraceId);
    });
})();
