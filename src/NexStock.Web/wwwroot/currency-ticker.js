(function () {
    var endpoint = (window.abp && abp.appPath ? abp.appPath : '/') + 'api/app/exchange-rate/current';
    var usd = document.getElementById('CurrencyTickerUsd');
    var eur = document.getElementById('CurrencyTickerEur');
    var updated = document.getElementById('CurrencyTickerUpdated');

    function format(rate) {
        return rate === undefined || rate === null ? '--' : new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(rate) + ' TL';
    }

    function refresh() {
        fetch(endpoint, { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (!data) return;
                usd.textContent = format(data.usd && data.usd.rate);
                eur.textContent = format(data.eur && data.eur.rate);
                updated.textContent = data.lastUpdated ? 'Güncellendi' : 'Son kayıt bekleniyor';
            })
            .catch(function () { updated.textContent = 'Kur bilgisi alınamadı'; });
    }

    refresh();
    window.setInterval(refresh, 60 * 60 * 1000);
}());
