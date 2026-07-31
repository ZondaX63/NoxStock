(function () {
    'use strict';

    if (window.localStorage.getItem('noxstock.performance.capture') !== 'true' || !window.PerformanceObserver) {
        return;
    }

    var sent = false;
    var largestContentfulPaint = 0;
    var cumulativeLayoutShift = 0;
    var longTaskCount = 0;

    function observe(type, callback) {
        try {
            var observer = new PerformanceObserver(function (list) {
                list.getEntries().forEach(callback);
            });
            observer.observe({ type: type, buffered: true });
        } catch (_) {
            // The browser may not expose every metric type.
        }
    }

    observe('largest-contentful-paint', function (entry) {
        largestContentfulPaint = Math.max(largestContentfulPaint, entry.startTime || 0);
    });
    observe('layout-shift', function (entry) {
        if (!entry.hadRecentInput) cumulativeLayoutShift += entry.value || 0;
    });
    observe('longtask', function () { longTaskCount += 1; });

    function send() {
        if (sent) return;
        var navigation = performance.getEntriesByType('navigation')[0];
        if (!navigation) return;
        sent = true;

        var paint = performance.getEntriesByName('first-contentful-paint')[0];
        var resources = performance.getEntriesByType('resource');
        var transferBytes = resources.reduce(function (total, resource) {
            return total + Math.max(0, Number(resource.transferSize) || 0);
        }, Math.max(0, Number(navigation.transferSize) || 0));
        var appPath = window.abp && window.abp.appPath ? window.abp.appPath.replace(/\/$/, '') : '';

        window.fetch(appPath + '/api/diagnostics/client-performance', {
            method: 'POST',
            credentials: 'same-origin',
            keepalive: true,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                path: window.location.pathname,
                navigationType: navigation.type,
                ttfbMs: navigation.responseStart,
                fcpMs: paint ? paint.startTime : 0,
                lcpMs: largestContentfulPaint,
                cls: cumulativeLayoutShift,
                loadMs: navigation.loadEventEnd || performance.now(),
                requestCount: resources.length + 1,
                transferBytes: transferBytes,
                longTaskCount: longTaskCount
            })
        }).catch(function () { });
    }

    window.addEventListener('pagehide', send, { once: true });
    window.setTimeout(send, 8000);
})();
