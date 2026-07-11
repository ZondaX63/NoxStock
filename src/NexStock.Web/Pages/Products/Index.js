$(function () {
    var rates = {};
    var displayCurrency = localStorage.getItem('productDisplayCurrency') || 'TRY';
    var editor = $('#ProductEditor');

    $('#DisplayCurrency').val(displayCurrency);

    function formatMoney(value, currency) {
        return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: currency, minimumFractionDigits: 2 }).format(Number(value || 0));
    }

    function toTry(value, currency) {
        var rate = currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate);
        return Number(value || 0) * (rate || 1);
    }

    function convert(value, fromCurrency, toCurrency) {
        if (fromCurrency === toCurrency) return Number(value || 0);
        var targetRate = toCurrency === 'TRY' ? 1 : Number(rates[toCurrency] && rates[toCurrency].rate);
        return toTry(value, fromCurrency) / (targetRate || 1);
    }

    function updateEquivalent(priceInput, currencyInput, target) {
        var value = Number(priceInput.val());
        var currency = currencyInput.val();
        target.text(value && currency !== 'TRY' ? 'Yaklasik ' + formatMoney(toTry(value, currency), 'TRY') : '');
    }

    function updatePriceEquivalents() {
        updateEquivalent($('#Product_PurchasePrice'), $('#Product_PurchaseCurrency'), $('#PurchaseTryEquivalent'));
        updateEquivalent($('#Product_SalePrice'), $('#Product_SaleCurrency'), $('#SaleTryEquivalent'));
    }

    function updateDisplayedPrices() {
        $('.product-purchase-price, .product-sale-price').each(function () {
            var $price = $(this);
            var sourceCurrency = $price.data('currency');
            var sourcePrice = Number($price.data('price'));
            if (sourceCurrency !== displayCurrency &&
                ((!rates[sourceCurrency] && sourceCurrency !== 'TRY') || (!rates[displayCurrency] && displayCurrency !== 'TRY'))) {
                $price.text(formatMoney(sourcePrice, sourceCurrency));
                return;
            }
            $price.text(formatMoney(convert(sourcePrice, sourceCurrency, displayCurrency), displayCurrency));
        });
    }

    function openEditor() {
        editor.prop('hidden', false);
        editor[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
        $('#Product_Name').trigger('focus');
    }

    $('#NewProductButton').on('click', openEditor);
    $('#CloseProductEditor').on('click', function () { editor.prop('hidden', true); });
    $('#Product_PurchasePrice, #Product_SalePrice, #Product_PurchaseCurrency, #Product_SaleCurrency').on('input change', updatePriceEquivalents);
    function calculateSalePrice() {
        var purchasePrice = Number($('#Product_PurchasePrice').val());
        var margin = Number($('#ProfitMargin').val());
        var purchaseCurrency = $('#Product_PurchaseCurrency').val();
        var saleCurrency = $('#Product_SaleCurrency').val();
        var purchaseRate = purchaseCurrency === 'TRY' ? 1 : Number(rates[purchaseCurrency] && rates[purchaseCurrency].rate);
        var saleRate = saleCurrency === 'TRY' ? 1 : Number(rates[saleCurrency] && rates[saleCurrency].rate);

        if (!purchasePrice || margin < 0) {
            $('#SaleCalculationInfo').text('Alış fiyatı ve geçerli bir kâr marjı girin.');
            return;
        }
        if (!purchaseRate || !saleRate) {
            $('#SaleCalculationInfo').text('Kur bilgisi alınamadı. Kur yenileniyor...');
            return fetchRates().then(function () {
                var refreshedPurchaseRate = purchaseCurrency === 'TRY' ? 1 : Number(rates[purchaseCurrency] && rates[purchaseCurrency].rate);
                var refreshedSaleRate = saleCurrency === 'TRY' ? 1 : Number(rates[saleCurrency] && rates[saleCurrency].rate);
                if (!refreshedPurchaseRate || !refreshedSaleRate) {
                    $('#SaleCalculationInfo').text('Kur bilgisi alınamadı. Lütfen tekrar deneyin.');
                    return;
                }
                var refreshedSalePriceInTry = purchasePrice * refreshedPurchaseRate * (1 + margin / 100);
                var refreshedFinalSalePrice = refreshedSalePriceInTry / refreshedSaleRate;
                $('#Product_SalePrice').val(refreshedFinalSalePrice.toFixed(2)).trigger('input');
                $('#SaleCalculationInfo').text(formatMoney(purchasePrice, purchaseCurrency) + ' + %' + margin + ' marj = ' + formatMoney(refreshedFinalSalePrice, saleCurrency) + ' (' + formatMoney(refreshedSalePriceInTry, 'TRY') + ')');
            });
        }

        var salePriceInTry = purchasePrice * purchaseRate * (1 + margin / 100);
        var finalSalePrice = salePriceInTry / saleRate;
        $('#Product_SalePrice').val(finalSalePrice.toFixed(2)).trigger('input');
        $('#SaleCalculationInfo').text(formatMoney(purchasePrice, purchaseCurrency) + ' + %' + margin + ' marj = ' + formatMoney(finalSalePrice, saleCurrency) + ' (' + formatMoney(salePriceInTry, 'TRY') + ')');
    }

    $('#CalculateSalePrice').on('click', function () {
        calculateSalePrice();
    });

    function fetchRates() {
        return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (!data) return;
                rates = {
                    USD: data.usd || data.USD,
                    EUR: data.eur || data.EUR
                };
                updatePriceEquivalents();
                updateDisplayedPrices();
            })
            .catch(function () { updatePriceEquivalents(); updateDisplayedPrices(); });
    }

    // Fetch currency data before initializing the table. A table/proxy error must not prevent price calculation.
    fetchRates();

    $('#ProductSearch').on('input', function () {
        var query = this.value.toLocaleLowerCase('tr-TR');
        $('#ProductsTable tbody tr').each(function () {
            $(this).toggle($(this).text().toLocaleLowerCase('tr-TR').indexOf(query) !== -1);
        });
    });

    $('#DisplayCurrency').on('change', function () {
        displayCurrency = this.value;
        localStorage.setItem('productDisplayCurrency', displayCurrency);
        updateDisplayedPrices();
    });

});
