/* Your Global Scripts */

// Safely patch HTMLInputElement.prototype.select to prevent Firefox / browser InvalidStateError
// when ABP ModalManager or jQuery calls .select() on <input type="number">, <input type="date">, etc.
(function () {
    if (typeof HTMLInputElement !== 'undefined' && HTMLInputElement.prototype) {
        var originalSelect = HTMLInputElement.prototype.select;
        HTMLInputElement.prototype.select = function () {
            try {
                if (!this.isConnected) return; // Prevent InvalidStateError on detached elements
                var type = (this.type || '').toLowerCase();
                // HTML5 spec: select() is only allowed on text, search, url, tel, password
                if (['number', 'date', 'datetime-local', 'month', 'time', 'week', 'color', 'checkbox', 'radio', 'file', 'range'].indexOf(type) !== -1) {
                    if (typeof this.focus === 'function') {
                        try { this.focus(); } catch(e) {}
                    }
                    return;
                }
                originalSelect.apply(this, arguments);
            } catch (e) {
                if (typeof this.focus === 'function' && this.isConnected) {
                    try { this.focus(); } catch(err) {}
                }
            }
        };
    }

    if (typeof HTMLElement !== 'undefined' && HTMLElement.prototype) {
        var originalFocus = HTMLElement.prototype.focus;
        HTMLElement.prototype.focus = function () {
            try {
                if (this.isConnected !== false) {
                    originalFocus.apply(this, arguments);
                }
            } catch (e) {
                // Ignore InvalidStateError / DOMException on focus
            }
        };
    }
})();

// Suppress "Could not find localization source: AbpUi" console warnings.
// AbpUi keys are merged into the NoxStock resource on the backend,
// but ABP's JS still looks for them under a separate "AbpUi" key.
// We pre-register the AbpUi namespace so the lookup succeeds silently.
(function () {
    var originalWarn = abp.log ? abp.log.warn : null;
    if (originalWarn) {
        abp.log.warn = function (msg) {
            if (msg && msg.indexOf && (msg.indexOf('Could not find localization source: AbpUi') !== -1 || msg.indexOf('Could not find localization source: NoxStock') !== -1)) {
                return; // swallow this specific warning
            }
            originalWarn.apply(abp.log, arguments);
        };
    }

    // Also pre-seed AbpUi values so actual lookups don't fail
    if (abp && abp.localization && abp.localization.values) {
        abp.localization.values['AbpUi'] = abp.localization.values['AbpUi'] || abp.localization.values['NoxStock'] || {};
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

// Opt-in guard for full-page commands. The lock is applied only after other form
// handlers have had a chance to reject invalid input, so ABP/client validation stays intact.
(function () {
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-nx-submit-lock')) {
            return;
        }

        if (form.dataset.nxSubmitting === 'true') {
            event.preventDefault();
            return;
        }

        form.dataset.nxSubmitting = 'true';
        window.setTimeout(function () {
            if (event.defaultPrevented) {
                delete form.dataset.nxSubmitting;
                return;
            }

            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (button) {
                button.disabled = true;
                button.classList.add('nx-submit-pending');
            });
        }, 0);
    });
})();

// Capture browser and AJAX failures in the server logs and show the user a useful, traceable notice.
(function () {
    var reportedErrorCount = 0;
    var maximumReportedErrors = 5;
    var lastShownMessage = null;
    var lastShownAt = 0;

    function traceId() {
        var element = document.querySelector('meta[name="noxstock-correlation-id"]');
        return element ? element.getAttribute('content') : null;
    }

    function showError(message, requestTraceId) {
        var now = Date.now();
        if (message === lastShownMessage && now - lastShownAt < 2000) {
            return;
        }
        lastShownMessage = message;
        lastShownAt = now;
        var container = document.getElementById('NoxStockErrorNotifications');
        if (!container) {
            container = document.createElement('div');
            container.id = 'NoxStockErrorNotifications';
            container.className = 'noxstock-error-notifications';
            container.setAttribute('aria-live', 'assertive');
            document.body.appendChild(container);
        }

        // Keep one actionable error visible instead of stacking a notification
        // for every browser event generated by the same failed operation.
        container.replaceChildren();

        var notification = document.createElement('div');
        notification.className = 'noxstock-error-notification';
        var title = document.createElement('strong');
        title.textContent = 'İşlem tamamlanamadı';
        var detail = document.createElement('span');
        detail.textContent = requestTraceId
            ? message + ' İzleme kodu: ' + requestTraceId
            : message;
        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'noxstock-error-close';
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
        var msg = String(event && (event.message || (event.error && event.error.message)) || '');
        if (msg.indexOf('no longer, usable') !== -1 || msg.indexOf('InvalidStateError') !== -1) {
            return;
        }
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
