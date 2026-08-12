(function () {
    'use strict';

    var CACHE_KEY = 'noxstock.exchangeRates.v1';
    var CACHE_TTL_MS = 15 * 60 * 1000;
    var usd = document.getElementById('CurrencyTickerUsd');
    var eur = document.getElementById('CurrencyTickerEur');
    var updated = document.getElementById('CurrencyTickerUpdated');
    var ticker = document.getElementById('CurrencyTicker');

    if (!ticker || !usd || !eur || !updated) return;

    function format(rate) {
        return rate === undefined || rate === null ? '--' : new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(rate) + ' TL';
    }

    function render(data) {
        if (!data) return;
        usd.textContent = format(data.usd && data.usd.rate);
        eur.textContent = format(data.eur && data.eur.rate);
        updated.textContent = data.lastUpdated ? ticker.dataset.tickerUpdated : ticker.dataset.tickerPending;
    }

    function readCache() {
        try {
            var cached = JSON.parse(window.localStorage.getItem(CACHE_KEY));
            return cached && cached.value ? cached : null;
        } catch (_) {
            return null;
        }
    }

    function writeCache(data) {
        try {
            window.localStorage.setItem(CACHE_KEY, JSON.stringify({ savedAt: Date.now(), value: data }));
        } catch (_) {
            // Storage can be disabled without preventing live exchange-rate use.
        }
    }

    function endpoint() {
        var appPath = window.abp && window.abp.appPath ? window.abp.appPath : '/';
        return appPath.replace(/\/?$/, '/') + 'api/app/exchange-rate/current';
    }

    function refresh() {
        return window.fetch(endpoint(), {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (!data) return;
                writeCache(data);
                render(data);
            })
            .catch(function () { updated.textContent = ticker.dataset.tickerError; });
    }

    var cached = readCache();
    var isFresh = cached && cached.savedAt && Date.now() - cached.savedAt < CACHE_TTL_MS;
    if (cached) render(cached.value);

    if (!isFresh) {
        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(refresh, { timeout: 1000 });
        } else {
            window.setTimeout(refresh, cached ? 250 : 0);
        }
    }

    window.setInterval(refresh, 60 * 60 * 1000);
}());
