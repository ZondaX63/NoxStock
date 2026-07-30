$(function () {
    var $tbody = $('#ItemsTbody');
    var $template = $('#ItemRowTemplate').html();
    var currency = $('#InvoiceCurrency').val() || $('#InvoiceForm').data('currency') || 'TRY';
    var useProductPricing = String($('#InvoiceForm').data('use-product-pricing')) === 'true';
    var usePurchaseCosting = String($('#InvoiceForm').data('use-purchase-costing')) === 'true';
    var rates = {};
    var l = abp.localization.getResource('NoxStock');
    var quickProductRow = null;
    $('#QuickProductSave').text(l('InvoiceQuickProductSave'));

    function escapeHtml(value) {
        return $('<div>').text(value || '').html();
    }

    function formatAmount(value, currencyCode) {
        return new Intl.NumberFormat(document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR', {
            style: 'currency', currency: currencyCode || 'TRY'
        }).format(Number(value || 0));
    }

    function uuidv4() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function addRow() {
        var id = uuidv4();
        var html = $template.replace(/\{id\}/g, id);
        $tbody.append(html);
        calculateTotals();
    }

    function addProductOption(product) {
        var label = product.name + ' · ' + product.sku +
            (product.barcode ? ' · ' + product.barcode : '') +
            ' (' + Number(product.quantity || 0) + ' ' + (product.unit || 'Adet') + ')';
        $('.product-select').each(function () {
            if (!$(this).find('option[value="' + product.id + '"]').length) {
                $(this).append(new Option(label, product.id));
            }
        });
    }

    function quoteProductPrice($row) {
        var productId = $row.find('.product-select').val();
        if (!productId) {
            return;
        }

        var quoteRequestId = Number($row.data('quoteRequestId') || 0) + 1;
        $row.data('quoteRequestId', quoteRequestId);
        $row.find('.price-overridden-input').val('false');

        var url = abp.appPath + 'api/app/product-pricing/quote?productId=' + encodeURIComponent(productId);
        fetch(url, { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : Promise.reject(); })
            .then(function (quote) {
                var isLatestQuote = Number($row.data('quoteRequestId')) === quoteRequestId;
                var hasManualPrice = $row.find('.price-overridden-input').val() === 'true';
                $row.data('priceQuote', quote);
                if (useProductPricing && quote.isAvailable === false) {
                    $row.find('.price-input').val('');
                    $row.find('.price-source').html(
                        '<span class="invoice-price-badge invoice-price-badge-warning">Maliyet bilgisi yok</span>' +
                        '<span class="invoice-price-detail">Dinamik satış fiyatı için önce bu ürünü içeren bir alış faturasını onaylayın.</span>');
                    calculateTotals();
                    return;
                }
                if (isLatestQuote && !hasManualPrice && useProductPricing) {
                    $row.find('.price-input').val(Number(quote.listSalePriceInTry || quote.unitPrice || 0).toFixed(4));
                    $row.find('.vat-input').val(Number(quote.vatRate || 0).toFixed(2));
                    [
                        quote.salesDiscount1,
                        quote.salesDiscount2,
                        quote.salesDiscount3,
                        quote.salesDiscount4
                    ].forEach(function (value, index) {
                        $row.find('.sales-discount-input').eq(index).val(Number(value || 0).toFixed(4));
                    });
                } else if (isLatestQuote && !hasManualPrice) {
                    $row.find('.price-input').val(Number(quote.purchaseListPrice || quote.purchasePrice || 0).toFixed(4));
                }
                if (usePurchaseCosting) {
                    [
                        quote.purchaseDiscount1,
                        quote.purchaseDiscount2,
                        quote.purchaseDiscount3,
                        quote.purchaseDiscount4
                    ].forEach(function (value, index) {
                        $row.find('.purchase-discount-input').eq(index).val(Number(value || 0).toFixed(4));
                    });
                }

                var sourceCurrency = quote.lastPurchaseSourceCurrencyCode || quote.purchaseCurrency || 'TRY';
                var pricingCurrency = quote.referenceCurrencyCode || quote.lastPurchaseCostCurrencyCode || quote.purchaseCurrency || 'TRY';
                var hasLastCost = Number(quote.lastPurchaseCostCurrencyAmount || 0) > 0;
                var sourceDetails = hasLastCost
                    ? 'Son onaylı alış: ' + Number(quote.lastPurchaseSourceNetUnitCost || 0).toFixed(4) + ' ' + sourceCurrency +
                        ' · fiyatlama maliyeti: ' + Number(quote.lastPurchaseCostCurrencyAmount || 0).toFixed(4) + ' ' + pricingCurrency +
                        (quote.lastPurchaseDocumentNumber ? ' · ' + quote.lastPurchaseDocumentNumber : '') +
                        (quote.lastPurchaseSupplierName ? ' · ' + quote.lastPurchaseSupplierName : '')
                    : 'Bu ürün için onaylı alış maliyeti yok.';
                var priceDetails;
                if (useProductPricing && quote.usesCurrentExchangeRate && hasLastCost) {
                    var pricingRate = Number(quote.referenceCurrencyExchangeRate || 1);
                    priceDetails = '<span class="invoice-price-badge invoice-price-badge-dynamic">Güncel satış fiyatı</span>' +
                        '<span class="invoice-price-detail">' + sourceDetails + '</span>' +
                        '<span class="invoice-price-detail">Referans satış: ' + formatAmount(quote.referenceSaleUnitPrice || 0, pricingCurrency) + '</span>' +
                        '<span class="invoice-price-detail">' + pricingCurrency + '/TRY: ' + pricingRate.toFixed(4) + ' · %' + Number(quote.profitMarkupPercent || 0).toFixed(2) + ' markup</span>' +
                        '<span class="invoice-price-detail">Taslakta önerilir; onayda güncel kurla yeniden hesaplanıp kilitlenir. Fiyatı değiştirirseniz manuel override korunur.</span>';
                } else if (useProductPricing) {
                    priceDetails = '<span class="invoice-price-badge invoice-price-badge-warning">Dinamik maliyet beklemede</span>' +
                        '<span class="invoice-price-detail">' + sourceDetails + '</span>' +
                        '<span class="invoice-price-detail">Satış fiyatı ürün kartındaki değerden başlatıldı.</span>';
                } else {
                    priceDetails = '<span class="invoice-price-badge invoice-price-badge-purchase">Alış maliyeti</span>' +
                        '<span class="invoice-price-detail">Bu belge onaylandığında net alış maliyeti ürünün maliyet profilini günceller.</span>' +
                        (hasLastCost ? '<span class="invoice-price-detail">Önceki maliyet: ' + sourceDetails + '</span>' : '<span class="invoice-price-detail">Önceki onaylı alış maliyeti bulunmuyor.</span>');
                }
                $row.find('.price-source').html(priceDetails);
                renderPurchaseCostImpact();
                calculateTotals();
            })
            .catch(function () {
                $row.find('.price-source').text('Güncel ürün fiyatı alınamadı.');
            });
    }

    function calculateTotals() {
        var subtotal = 0;
        var discountTotal = 0;
        var vatTotal = 0;

        $tbody.find('.item-row').each(function () {
            var $row = $(this);
            var quantity = parseFloat($row.find('.quantity-input').val()) || 0;
            var price = parseFloat($row.find('.price-input').val()) || 0;
            var discountRate = getEffectiveDiscountRate($row);
            var vatRate = parseFloat($row.find('.vat-input').val()) || 0;

            var lineNet = quantity * price;
            var discount = lineNet * (discountRate / 100);
            var taxable = lineNet - discount;
            var vat = taxable * (vatRate / 100);
            var lineTotal = taxable + vat;

            $row.find('.row-total').text(lineTotal.toFixed(2));

            subtotal += lineNet;
            discountTotal += discount;
            vatTotal += vat;
        });

        var grandTotal = subtotal - discountTotal + vatTotal;

        $('#SumSubtotal').text(subtotal.toFixed(2) + ' ' + currency);
        $('#SumDiscount').text(discountTotal.toFixed(2) + ' ' + currency);
        $('#SumVat').text(vatTotal.toFixed(2) + ' ' + currency);
        $('#SumGrandTotal').text(grandTotal.toFixed(2) + ' ' + currency);
    }

    function getEffectiveDiscountRate($row) {
        var $discountInputs = usePurchaseCosting
            ? $row.find('.purchase-discount-input')
            : useProductPricing
                ? $row.find('.sales-discount-input')
                : $();
        if (!$discountInputs.length) {
            return parseFloat($row.find('.discount-input').val()) || 0;
        }

        var remainingFactor = 1;
        $discountInputs.each(function () {
            var discount = Math.min(100, Math.max(0, Number($(this).val()) || 0));
            remainingFactor *= 1 - discount / 100;
        });
        var effectiveRate = (1 - remainingFactor) * 100;
        $row.find('input[name$=".DiscountRate"]').val(effectiveRate.toFixed(4));
        return effectiveRate;
    }

    function renderPurchaseCostImpact() {
        if (!usePurchaseCosting) {
            return;
        }

        var lines = [];
        $tbody.find('.item-row').each(function () {
            var $row = $(this);
            var quote = $row.data('priceQuote');
            var productName = $row.find('.product-select option:selected').text();
            var unitPrice = Number($row.find('.price-input').val() || 0);
            if (!productName || !quote || unitPrice <= 0) {
                return;
            }

            var referenceCurrency = quote.referenceCurrencyCode || quote.purchaseCurrency || currency;
            var referenceRate = referenceCurrency === 'TRY'
                ? 1
                : Number(quote.referenceCurrencyExchangeRate || (rates[referenceCurrency] && rates[referenceCurrency].rate) || 0);
            var documentRate = currency === 'TRY'
                ? 1
                : Number($('#InvoiceExchangeRate').val() || 0) || Number((rates[currency] && rates[currency].rate) || 0);
            var netUnitPrice = unitPrice * (1 - getEffectiveDiscountRate($row) / 100);
            var tryCost = netUnitPrice * documentRate;
            var referenceCost = referenceRate > 0 ? tryCost / referenceRate : null;
            var preview = referenceCost === null
                ? 'Kur bulunamadı; referans maliyet hesaplanamadı.'
                : 'Yeni son alış maliyeti: <strong>' + escapeHtml(formatAmount(referenceCost, referenceCurrency)) + '</strong>';
            lines.push('<li><strong>' + escapeHtml(productName) + '</strong> · net birim alış: ' + escapeHtml(formatAmount(netUnitPrice, currency)) + ' · ' + preview + '</li>');
        });

        var $impact = $('#PurchaseCostImpact');
        if (!lines.length) {
            $impact.addClass('hidden').empty();
            return;
        }
        $impact.removeClass('hidden').html('<strong>Onay öncesi maliyet etkisi</strong><p class="mt-1">Taslakta uygulanmaz; onayda bu satırların maliyet snapshotı güncellenir.</p><ul class="mt-2 list-disc space-y-1 pl-5">' + lines.join('') + '</ul>');
    }

    function renderManualOverride($row) {
        if (!useProductPricing || $row.find('.price-overridden-input').val() !== 'true') {
            return;
        }
        var quote = $row.data('priceQuote');
        var manualPrice = Number($row.find('.price-input').val() || 0);
        if (!quote) {
            return;
        }
        var recommendedPrice = Number(quote.unitPrice || 0);
        var difference = manualPrice - recommendedPrice;
        var differenceText = difference === 0
            ? 'Önerilen fiyatla aynı.'
            : (difference > 0 ? '+' : '') + formatAmount(difference, currency) + ' fark';
        $row.find('.price-source').html(
            '<span class="invoice-price-badge invoice-price-badge-warning">Manuel fiyat uygulandı</span>' +
            '<span class="invoice-price-detail">Önerilen: ' + formatAmount(recommendedPrice, currency) + ' · Manuel: ' + formatAmount(manualPrice, currency) + '</span>' +
            '<span class="invoice-price-detail">' + differenceText + '. Onayda manuel fiyat snapshot olarak kilitlenir.</span>');
    }

    function refreshRates() {
        return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                rates = {
                    USD: data && (data.usd || data.USD),
                    EUR: data && (data.eur || data.EUR)
                };
                var rate = currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate || 0);
                var $documentRate = $('#InvoiceExchangeRate');
                if ($documentRate.length && rate > 0 && !$documentRate.data('manual-rate') && Number($documentRate.val() || 0) <= 1) {
                    $documentRate.val(rate.toFixed(6));
                }
                $('#InvoiceRateInfo').text(useProductPricing
                    ? 'Satış belgesi TRY olarak kaydedilir; ürün referans kuru satır bazında kilitlenir.'
                    : rate > 0
                        ? currency + '/TRY güncel kur: ' + rate.toFixed(4) + '. Onaylanan belgede kullanılan kur sabitlenir.'
                        : 'Güncel kur bulunamadı. Sunucu belge kaydında tekrar deneyecek.');
                renderPurchaseCostImpact();
            })
            .catch(function () {
                $('#InvoiceRateInfo').text('Güncel kur alınamadı. Fiyat hesaplaması için tekrar deneyin.');
            });
    }

    $('#AddRowBtn').click(function () {
        addRow();
    });

    $('#InvoiceDocNumber').on('input', function () {
        $('#IsDocumentNumberAutoGenerated').val('false');
    });

    $tbody.on('click', '.remove-row-btn', function () {
        $(this).closest('.item-row').remove();
        calculateTotals();
    });

    $tbody.on('input', '.calc-input', function () {
        if ($(this).hasClass('price-input')) {
            var $row = $(this).closest('.item-row');
            $row.find('.price-overridden-input').val('true');
            renderManualOverride($row);
        }
        calculateTotals();
        renderPurchaseCostImpact();
    });

    $tbody.on('change', '.product-select', function () {
        quoteProductPrice($(this).closest('.item-row'));
    });

    $tbody.on('input', '.product-search', function () {
        var query = String($(this).val() || '').trim().toLocaleLowerCase('tr-TR');
        var $select = $(this).closest('.invoice-product-picker').find('.product-select');
        $select.find('option').each(function () {
            if (!this.value) {
                this.hidden = false;
                return;
            }
            this.hidden = query.length > 0 && String(this.text || '').toLocaleLowerCase('tr-TR').indexOf(query) === -1;
        });
    });

    $tbody.on('keydown', '.product-search', function (event) {
        if (event.key !== 'Enter') {
            return;
        }
        event.preventDefault();
        var $row = $(this).closest('.item-row');
        var $matches = $row.find('.product-select option').filter(function () { return this.value && !this.hidden; });
        if ($matches.length === 1) {
            $row.find('.product-select').val($matches.first().val()).trigger('change');
            return;
        }
        $row.find('.quick-product-create').trigger('click');
    });

    $tbody.on('click', '.quick-product-create', function () {
        quickProductRow = $(this).closest('.item-row');
        var suggestedName = quickProductRow.find('.product-search').val();
        var now = new Date();
        var sku = 'STK-' + now.getFullYear() + String(now.getMonth() + 1).padStart(2, '0') + String(now.getDate()).padStart(2, '0') + String(now.getTime()).slice(-6);
        $('#QuickProductName').val(suggestedName || '').trigger('focus');
        $('#QuickProductSku').val(sku);
        $('#QuickProductBarcode').val('');
        $('#QuickProductUnit').val('Adet');
        $('#QuickProductPanel').removeAttr('hidden');
    });

    function closeQuickProductPanel() {
        $('#QuickProductPanel').attr('hidden', 'hidden');
        quickProductRow = null;
    }

    $(document).on('click', '.quick-product-cancel', closeQuickProductPanel);
    $(document).on('keydown', function (event) {
        if (event.key === 'Escape' && !$('#QuickProductPanel').is('[hidden]')) {
            closeQuickProductPanel();
        }
    });
    $('#QuickProductSave').on('click', function () {
        var $button = $(this);
        var name = String($('#QuickProductName').val() || '').trim();
        var sku = String($('#QuickProductSku').val() || '').trim();
        if (!name || !sku || !quickProductRow) {
            abp.notify.warn(l('InvoiceQuickProductRequired'));
            return;
        }
        var barcode = String($('#QuickProductBarcode').val() || '').trim();
        $button.prop('disabled', true).text(l('Saving'));
        noxStock.products.product.create({
            name: name,
            sku: sku,
            barcode: barcode,
            unit: $('#QuickProductUnit').val() || 'Adet',
            purchaseCurrency: 'TRY',
            exchangeRateAtPurchase: 1,
            pricingMode: 0,
            profitMarkupPercent: 30,
            vatRate: Number($('#InvoiceForm').data('default-vat-rate') || 20),
            trackStock: true
        }).then(function (product) {
            addProductOption(product);
            quickProductRow.find('.product-search').val('');
            quickProductRow.find('.product-select').val(product.id).trigger('change');
            closeQuickProductPanel();
            abp.notify.success(l('InvoiceQuickProductCreated'));
        }).catch(function (error) {
            abp.notify.error(error && error.message ? error.message : l('InvoiceQuickProductFailed'));
        }).finally(function () {
            $button.prop('disabled', false).text(l('InvoiceQuickProductSave'));
        });
    });

    $('#InvoiceCurrency').on('change', function () {
        currency = $(this).val() || 'TRY';
        $('#InvoiceForm').data('currency', currency);
        $tbody.find('.item-row').each(function () {
            quoteProductPrice($(this));
        });
        refreshRates();
        calculateTotals();
    });

    $('#InvoiceExchangeRate').on('input change', function () {
        $(this).data('manual-rate', true);
        renderPurchaseCostImpact();
        calculateTotals();
    });

    // Add first row by default
    if ($tbody.children().length === 0) {
        addRow();
    }

    refreshRates();
    $tbody.find('.item-row').each(function () {
        if ($(this).find('.product-select').val()) {
            quoteProductPrice($(this));
        }
    });
});
