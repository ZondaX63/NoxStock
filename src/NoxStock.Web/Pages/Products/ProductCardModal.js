(function (window, $) {
    'use strict';

    var rates = {};

    function numberValue($element) {
        return Number($element.val()) || 0;
    }

    function formatMoney(value) {
        return new Intl.NumberFormat(document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR', {
            style: 'currency', currency: 'TRY', minimumFractionDigits: 2, maximumFractionDigits: 2
        }).format(Number(value) || 0);
    }

    function formatDecimal(value, digits) {
        return new Intl.NumberFormat(document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR', {
            minimumFractionDigits: 0, maximumFractionDigits: digits
        }).format(Number(value) || 0);
    }

    function compactInputNumber(value, digits) {
        var number = Number(value || 0);
        if (!Number.isFinite(number)) return '0';
        return number.toFixed(digits || 6).replace(/(\.\d*?[1-9])0+$/, '$1').replace(/\.0+$/, '').replace(/\.$/, '');
    }

    function currentRate(currency) {
        return currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate) || 0;
    }

    function chainDiscount($form, amount, prefix) {
        var result = Math.max(0, Number(amount) || 0);
        for (var index = 1; index <= 4; index++) {
            var discount = Math.min(100, Math.max(0, numberValue($form.find('#Product_' + prefix + index))));
            result *= 1 - discount / 100;
        }
        return result;
    }

    function syncRate($form, forceCurrentRate) {
        var currency = String($form.find('#Product_PurchaseCurrency').val() || 'TRY').toUpperCase();
        var $rate = $form.find('[data-purchase-rate]');
        var $override = $form.find('[data-rate-override]');

        if (currency === 'TRY') {
            $rate.val('1').prop('readonly', true);
            $override.prop('checked', false).prop('disabled', true);
            return;
        }

        $override.prop('disabled', false);
        if ($override.prop('checked')) {
            $rate.prop('readonly', false);
            return;
        }

        $rate.prop('readonly', true);
        var rate = currentRate(currency);
        if (forceCurrentRate && rate > 0) {
            $rate.val(compactInputNumber(rate, 6));
        }
    }

    function syncPricingMode($form) {
        var dynamic = String($form.find('#Product_PricingMode').val() || '0') === '1';
        var $sale = $form.find('#Product_SalePrice');
        $sale.prop('disabled', dynamic).attr('aria-disabled', dynamic ? 'true' : 'false');
        $form.find('[data-manual-sale-field]').toggleClass('is-automatic', dynamic);
        $form.find('[data-sale-price-label]').text(dynamic ? 'Otomatik satış fiyatı (KDV\'siz)' : 'Manuel satış fiyatı (KDV\'siz, TL)');
        $form.find('[data-markup-label]').text(dynamic ? 'Hedef kâr ekleme oranı (%)' : 'Hedef / hesaplanan kâr ekleme oranı (%)');
        $form.find('[data-sale-price-hint]').text(dynamic
            ? 'Maliyet ve hedef oran üzerinden otomatik hesaplanır.'
            : 'Fiyatı veya oranı değiştirebilirsiniz; diğeri maliyete göre anında eşitlenir.');
        $form.find('[data-markup-hint]').text(dynamic
            ? 'Dinamik fiyatın ana girdisidir.'
            : 'Fiyat veya oran değiştiğinde karşılığı anında hesaplanır.');
        $form.find('[data-pricing-mode-explainer]').text(dynamic
            ? 'Satış fiyatı güncel kur, net döviz maliyeti ve hedef markup ile hesaplanır.'
            : 'Sabit satış fiyatı TL olarak korunur ve kur değişiminden etkilenmez.');
        return dynamic;
    }

    function refresh($form) {
        if (!$form || !$form.length) return;

        var currency = String($form.find('#Product_PurchaseCurrency').val() || 'TRY').toUpperCase();
        var purchaseList = Math.max(0, numberValue($form.find('#Product_PurchaseListPrice')));
        var netPurchase = chainDiscount($form, purchaseList, 'PurchaseDiscount');
        var transactionRate = currency === 'TRY' ? 1 : numberValue($form.find('[data-purchase-rate]'));
        var netCost = transactionRate > 0 ? netPurchase * transactionRate : null;
        var dynamic = syncPricingMode($form);
        var markup = Math.max(0, numberValue($form.find('#Product_ProfitMarkupPercent')));
        var liveRate = currentRate(currency);
        var dynamicCost = liveRate > 0 ? netPurchase * liveRate : null;
        var source = $form.data('pricingSource');
        var manualSale = Math.max(0, numberValue($form.find('#Product_SalePrice')));
        if (!source) source = manualSale <= 0 && markup > 0 ? 'markup' : 'sale';

        var listSale = dynamic
            ? (dynamicCost == null ? null : dynamicCost * (1 + markup / 100))
            : source === 'markup' && netCost != null
                ? netCost * (1 + markup / 100)
                : manualSale;

        if (dynamic || source === 'markup') {
            if (listSale != null) $form.find('#Product_SalePrice').val(compactInputNumber(listSale, 6));
        } else if (netCost != null && netCost > 0) {
            $form.find('#Product_ProfitMarkupPercent').val(compactInputNumber(Math.max(0, (manualSale / netCost - 1) * 100), 6));
            markup = numberValue($form.find('#Product_ProfitMarkupPercent'));
        }

        var netSale = listSale == null ? null : chainDiscount($form, listSale, 'SalesDiscount');
        var salesDiscount = listSale == null || netSale == null ? null : Math.max(0, listSale - netSale);
        var vatRate = Math.min(100, Math.max(0, numberValue($form.find('#Product_VatRate'))));
        var vatAmount = netSale == null ? null : netSale * vatRate / 100;
        var finalPrice = netSale == null ? null : netSale + vatAmount;
        var displayedCost = dynamic ? dynamicCost : netCost;
        var saleMissing = !dynamic && (!listSale || listSale <= 0);
        var profit = displayedCost == null || netSale == null || saleMissing ? null : netSale - displayedCost;
        var margin = netSale > 0 && profit != null ? profit / netSale * 100 : null;
        var realizedMarkup = displayedCost > 0 && profit != null ? profit / displayedCost * 100 : null;
        var effectivePurchaseDiscount = purchaseList > 0 ? (1 - netPurchase / purchaseList) * 100 : 0;
        var displayedRate = dynamic ? liveRate : transactionRate;
        var text = function (value) { return value == null ? 'Kur bekleniyor' : formatMoney(value); };

        $form.find('[data-dialog-value="net-purchase-currency"]').text(formatDecimal(netPurchase, 4) + ' ' + currency);
        $form.find('[data-dialog-value="cost"]').text(text(displayedCost));
        $form.find('[data-dialog-value="list"]').text(text(listSale));
        $form.find('[data-dialog-value="sales-discount"]').text(salesDiscount == null ? '—' : '-' + formatMoney(salesDiscount));
        $form.find('[data-dialog-value="net-sale"]').text(text(netSale));
        $form.find('[data-dialog-value="vat-label"]').text('KDV (%' + formatDecimal(vatRate, 0) + ')');
        $form.find('[data-dialog-value="vat-amount"]').text(text(vatAmount));
        $form.find('[data-dialog-value="final"]').text(text(finalPrice));
        $form.find('[data-dialog-value="profit"]').text(saleMissing ? '—' : (profit == null ? 'Hesaplanamadı' : formatMoney(profit)));
        $form.find('[data-dialog-value="realized-markup"]').text(saleMissing ? 'Satış fiyatı giriniz' : (realizedMarkup == null ? 'Gerçekleşen markup hesaplanamadı' : 'Gerçekleşen markup: %' + formatDecimal(realizedMarkup, 2)));
        $form.find('[data-dialog-value="margin"]').text(saleMissing ? 'Satış fiyatı giriniz' : (margin == null ? 'Brüt kâr marjı hesaplanamadı' : 'Brüt kâr marjı: %' + formatDecimal(margin, 2)));
        $form.find('[data-dialog-value="meta"]').text(displayedRate > 0
            ? '%' + formatDecimal(effectivePurchaseDiscount, 2) + ' bileşik alış iskontosu · kur ' + formatDecimal(displayedRate, 4)
            : currency + '/TRY işlem kuru gerekli.');
        $form.find('.product-finance-profit').attr('data-profit-state', saleMissing ? 'placeholder' : (profit == null || profit === 0 ? 'neutral' : (profit < 0 ? 'loss' : 'profit')));
    }

    function loadRates($form, forceCurrentRate) {
        return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                rates = data ? { USD: data.usd || data.USD, EUR: data.eur || data.EUR } : {};
                syncRate($form, forceCurrentRate);
                refresh($form);
            })
            .catch(function () {
                syncRate($form, false);
                refresh($form);
            });
    }

    function generateSku($form) {
        var now = new Date();
        var stamp = now.getFullYear() + String(now.getMonth() + 1).padStart(2, '0') + String(now.getDate()).padStart(2, '0') + String(now.getTime()).slice(-6);
        $form.find('#Product_Sku').val('STK-' + stamp).trigger('input').trigger('focus').select();
    }

    function initialize(target) {
        var $form = $(target).is('.product-create-dialog-form') ? $(target) : $(target).find('.product-create-dialog-form').first();
        if (!$form.length || $form.data('invoice-product-card-ready')) return;
        $form.data('invoice-product-card-ready', true);

        $form.on('click.productCardModal', '[data-dialog-tab]', function () {
            var section = String($(this).data('dialog-tab'));
            $form.find('[data-dialog-tab]').removeClass('is-active').attr('aria-selected', 'false');
            $(this).addClass('is-active').attr('aria-selected', 'true');
            $form.find('[data-dialog-section]').attr('hidden', 'hidden');
            $form.find('[data-dialog-section="' + section + '"]').removeAttr('hidden');
        });
        $form.on('click.productCardModal', '[data-dialog-generate-sku]', function () { generateSku($form); });
        $form.on('input.productCardModal change.productCardModal', '#Product_SalePrice, #Product_ProfitMarkupPercent', function () {
            $form.data('pricingSource', this.id === 'Product_ProfitMarkupPercent' ? 'markup' : 'sale');
        });
        $form.on('input.productCardModal change.productCardModal', 'input, select', function () { refresh($form); });
        $form.on('change.productCardModal', '#Product_PurchaseCurrency', function () { loadRates($form, true); });
        $form.on('change.productCardModal', '[data-rate-override]', function () { syncRate($form, !this.checked); refresh($form); });
        $form.on('click.productCardModal', '[data-rate-refresh]', function (event) {
            event.preventDefault();
            var $button = $(this).prop('disabled', true);
            loadRates($form, true).then(function () { $button.prop('disabled', false); });
        });
        $form.on('click.productCardModal', '[data-quick-lookup-create]', function () {
            var type = String($(this).data('quick-lookup-create'));
            $form.find('[data-quick-lookup-panel]').attr('hidden', 'hidden');
            $form.find('[data-quick-lookup-panel="' + type + '"]').removeAttr('hidden').find('[data-quick-lookup-name]').trigger('focus');
        });
        $form.on('click.productCardModal', '[data-quick-lookup-cancel]', function () { $(this).closest('[data-quick-lookup-panel]').attr('hidden', 'hidden'); });
        $form.on('click.productCardModal', '[data-quick-lookup-save]', function () {
            var $button = $(this);
            var $panel = $button.closest('[data-quick-lookup-panel]');
            var type = String($panel.data('quick-lookup-panel'));
            var name = String($panel.find('[data-quick-lookup-name]').val() || '').trim();
            if (!name) return $panel.find('[data-quick-lookup-name]').trigger('focus');
            var service = type === 'brand' ? noxStock.brands.brand : noxStock.categories.category;
            $button.prop('disabled', true);
            service.create({ name: name }).then(function (created) {
                var selector = type === 'brand' ? '#Product_BrandId' : '#Product_CategoryId';
                $form.find(selector).append(new Option(created.name, created.id, true, true)).trigger('change');
                $panel.attr('hidden', 'hidden');
            }, function (error) {
                abp.notify.error(error && error.message ? error.message : 'Kayıt eklenemedi.');
            }).then(function () { $button.prop('disabled', false); });
        });

        syncRate($form, false);
        refresh($form);
        loadRates($form, true);
    }

    window.noxStock = window.noxStock || {};
    window.noxStock.productCardModal = { init: initialize, refresh: refresh };
}(window, jQuery));
