(function () {
    'use strict';

    var CACHE_TTL_MS = 30 * 1000;
    var initialized = false;

    function readCache(key) {
        try {
            var cached = JSON.parse(window.sessionStorage.getItem(key));
            if (!cached || !cached.savedAt || Date.now() - cached.savedAt >= CACHE_TTL_MS) {
                return null;
            }

            return cached.value || null;
        } catch (_) {
            return null;
        }
    }

    function writeCache(key, value) {
        try {
            window.sessionStorage.setItem(key, JSON.stringify({ savedAt: Date.now(), value: value }));
        } catch (_) {
            // Storage may be disabled. The live request still provides the data.
        }
    }

    function setState(root, state) {
        root.querySelector('[data-alerts-loading-state]').classList.toggle('hidden', state !== 'loading');
        root.querySelector('[data-alerts-list]').classList.toggle('hidden', state !== 'list');
        root.querySelector('[data-alerts-empty-state]').classList.toggle('hidden', state !== 'empty');
        root.querySelector('[data-alerts-error-state]').classList.toggle('hidden', state !== 'error');
    }

    function resolveLink(link) {
        if (!link || link.charAt(0) !== '/') return link || '#';
        var appPath = window.abp && window.abp.appPath ? window.abp.appPath : '/';
        return appPath.replace(/\/$/, '') + link;
    }

    function render(root, summary) {
        var count = Math.max(0, Number(summary && summary.totalCount) || 0);
        var badge = root.querySelector('[data-alerts-count]');
        badge.textContent = count > 99 ? '99+' : String(count);
        badge.classList.toggle('hidden', count === 0);

        var list = root.querySelector('[data-alerts-list]');
        list.replaceChildren();
        var alerts = summary && Array.isArray(summary.alerts) ? summary.alerts.slice(0, 4) : [];
        if (alerts.length === 0) {
            setState(root, 'empty');
            return;
        }

        alerts.forEach(function (alert) {
            var link = document.createElement('a');
            link.href = resolveLink(alert.link);
            link.className = 'block border-b border-slate-100 px-4 py-3 text-left hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-700/50';

            var title = document.createElement('span');
            title.className = 'block text-xs font-semibold ' + (alert.severity === 'Critical' ? 'text-red-600' : 'text-amber-600');
            title.textContent = alert.title || '';

            var message = document.createElement('span');
            message.className = 'mt-1 block text-xs leading-5 text-slate-600 dark:text-slate-300';
            message.textContent = alert.message || '';

            link.append(title, message);
            list.appendChild(link);
        });
        setState(root, 'list');
    }

    function initialize() {
        if (initialized) return;
        initialized = true;

        var root = document.querySelector('[data-topbar-alerts]');
        if (!root) return;

        var endpoint = root.dataset.alertsEndpoint;
        var cacheKey = 'noxstock.topbarAlerts:' + (root.dataset.alertsCacheContext || endpoint);
        var cached = readCache(cacheKey);
        var request = null;

        if (cached) render(root, cached);

        function load(force) {
            if (!force && cached) return Promise.resolve(cached);
            if (request) return request;
            if (!cached) setState(root, 'loading');

            request = window.fetch(endpoint, {
                credentials: 'same-origin',
                headers: { Accept: 'application/json' }
            })
                .then(function (response) {
                    if (!response.ok) throw new Error('Alert summary request failed.');
                    return response.json();
                })
                .then(function (summary) {
                    cached = summary;
                    writeCache(cacheKey, summary);
                    render(root, summary);
                    return summary;
                })
                .catch(function () {
                    if (!cached) setState(root, 'error');
                    return null;
                })
                .finally(function () { request = null; });
            return request;
        }

        root.querySelector('[data-alerts-trigger]').addEventListener('click', function () {
            load(false);
        });

        if (!cached) {
            if ('requestIdleCallback' in window) {
                window.requestIdleCallback(function () { load(false); }, { timeout: 1200 });
            } else {
                window.setTimeout(function () { load(false); }, 250);
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
    } else {
        initialize();
    }
}());
