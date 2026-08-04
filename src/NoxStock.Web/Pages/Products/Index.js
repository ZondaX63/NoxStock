$(function () {
    var rates = {};
    var showReferenceCurrency = localStorage.getItem('productShowReferenceCurrency') === 'true';
    var editor = $('#ProductEditor');
    var editModal = new abp.ModalManager(abp.appPath + 'Products/EditModal');
    var createModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Products/CreateModal',
        modalClass: 'ProductCreateModal'
    });
    var dataTable;
    var searchTimer;
    var referenceCurrencyColumns = [11, 12, 14];
    var defaultVisibleColumns = [2, 3, 5, 6, 15];

    syncCurrencyViewControl();

    // Modal forms are loaded after this page. Use a capturing invalid handler
    // so a required/min/max error is visible inside the product card before
    // ABP submits its AJAX request.
    document.addEventListener('invalid', function (event) {
        var field = event.target;
        if (!field || !field.matches || !field.matches('.product-create-dialog-form input, .product-create-dialog-form select, .product-create-dialog-form textarea')) {
            return;
        }
        var $form = $(field).closest('.product-create-dialog-form');
        var $summary = $form.find('.product-save-errors');
        var fieldLabel = $form.find('label[for="' + field.id + '"]').first().text().trim() || 'Bu alan';
        $summary.removeClass('validation-summary-valid').html('<strong>Ürün kaydedilemedi.</strong> ' + escapeHtml(fieldLabel) + ': ' + escapeHtml(field.validationMessage));

        var $section = $(field).closest('[data-dialog-section]');
        if ($section.length && $section.is('[hidden]')) {
            var sectionName = $section.data('dialog-section');
            $form.find('[data-dialog-tab="' + sectionName + '"]').trigger('click');
        }
    }, true);

    // The creation modal is loaded dynamically by ABP. Delegate these actions
    // from the static page root so they work even if a modal lifecycle hook is
    // skipped by a cached or already-open Bootstrap dialog.
    $(document).on('click', '.product-create-dialog-form [data-dialog-tab]', function (event) {
        event.preventDefault();
        var $form = $(this).closest('.product-create-dialog-form');
        var tab = String($(this).data('dialog-tab'));
        $form.find('[data-dialog-tab]')
            .removeClass('is-active')
            .attr('aria-selected', 'false');
        $(this)
            .addClass('is-active')
            .attr('aria-selected', 'true');
        $form.find('[data-dialog-section]')
            .attr('hidden', 'hidden')
            .filter('[data-dialog-section="' + tab + '"]')
            .removeAttr('hidden');
        refreshCreateDialogHero($form);
    });

    $(document).on('click', '.product-create-dialog-form [data-dialog-generate-sku]', function (event) {
        event.preventDefault();
        var now = new Date();
        var stamp = now.getFullYear().toString() +
            String(now.getMonth() + 1).padStart(2, '0') +
            String(now.getDate()).padStart(2, '0') +
            String(now.getTime()).slice(-6);
        $(this).closest('.product-create-dialog-form').find('#Product_Sku')
            .val('STK-' + stamp)
            .trigger('input')
            .trigger('focus')
            .select();
    });

    $(document).on('change', '.product-create-dialog-form #Product_PurchaseCurrency', function () {
        var $form = $(this).closest('.product-create-dialog-form');
        syncPurchaseRate($form, true);
        refreshCreateDialogHero($form);
    });

    $(document).on('change', '.product-create-dialog-form [data-rate-override]', function () {
        var $form = $(this).closest('.product-create-dialog-form');
        syncPurchaseRate($form, !this.checked);
        refreshCreateDialogHero($form);
    });

    $(document).on('click', '.product-create-dialog-form [data-rate-refresh]', function (event) {
        event.preventDefault();
        var $button = $(this);
        var $form = $button.closest('.product-create-dialog-form');
        $button.addClass('is-loading').prop('disabled', true);
        fetchRates().then(function () {
            syncPurchaseRate($form, true);
            refreshCreateDialogHero($form);
        }).finally(function () {
            $button.removeClass('is-loading').prop('disabled', false);
        });
    });

    $(document).on('input change', '.product-create-dialog-form #Product_SalePrice, .product-create-dialog-form #Product_ProfitMarkupPercent', function () {
        $(this).closest('.product-create-dialog-form').data('pricingSource', this.id === 'Product_ProfitMarkupPercent' ? 'markup' : 'sale');
    });

    $(document).on('input change', '.product-create-dialog-form input, .product-create-dialog-form select', function () {
        refreshCreateDialogHero($(this).closest('.product-create-dialog-form'));
    });

    $(document).on('click', '.product-create-dialog-form [data-quick-lookup-create]', function (event) {
        event.preventDefault();
        var $button = $(this);
        var $form = $button.closest('.product-create-dialog-form');
        var lookupType = String($button.data('quick-lookup-create'));
        var $panel = $form.find('[data-quick-lookup-panel="' + lookupType + '"]');
        $form.find('[data-quick-lookup-panel]').not($panel).attr('hidden', 'hidden');
        $panel.removeAttr('hidden').find('[data-quick-lookup-name]').val('').trigger('focus');
    });

    $(document).on('click', '.product-create-dialog-form [data-quick-lookup-cancel]', function (event) {
        event.preventDefault();
        $(this).closest('[data-quick-lookup-panel]').attr('hidden', 'hidden');
    });

    $(document).on('click', '.product-create-dialog-form [data-quick-lookup-save]', function (event) {
        event.preventDefault();
        var $button = $(this);
        var $panel = $button.closest('[data-quick-lookup-panel]');
        var $form = $button.closest('.product-create-dialog-form');
        var lookupType = String($panel.data('quick-lookup-panel'));
        var label = lookupType === 'brand' ? 'marka' : 'kategori';
        var name = String($panel.find('[data-quick-lookup-name]').val() || '').trim();
        if (!name) {
            $panel.find('[data-quick-lookup-name]').trigger('focus');
            return;
        }

        $button.prop('disabled', true).text('Kaydediliyor…');
        var service = lookupType === 'brand'
            ? noxStock.brands.brand
            : noxStock.categories.category;
        service.create({ name: name })
            .then(function (created) {
                var selectId = lookupType === 'brand' ? '#Product_BrandId' : '#Product_CategoryId';
                var $select = $form.find(selectId);
                var option = new Option(created.name, created.id, true, true);
                $select.append(option).trigger('change');
                $panel.attr('hidden', 'hidden');
                abp.notify.success('Yeni ' + label + ' eklendi.');
            })
            .catch(function (error) {
                var message = error && error.message ? error.message : 'Yeni ' + label + ' eklenemedi.';
                abp.notify.error(message);
            })
            .finally(function () { $button.prop('disabled', false).text('Kaydet'); });
    });

    function formatMoney(value, currency) {
        var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
        return new Intl.NumberFormat(locale, {
            style: 'currency',
            currency: currency || 'TRY',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(Number(value || 0));
    }

    function formatDecimal(value, digits) {
        var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
        return new Intl.NumberFormat(locale, {
            minimumFractionDigits: 0,
            maximumFractionDigits: digits
        }).format(Number(value || 0));
    }

    function compactInputNumber(value, digits) {
        var number = Number(value || 0);
        if (!Number.isFinite(number)) return '0';
        return number.toFixed(digits || 6).replace(/(\.\d*?[1-9])0+$/, '$1').replace(/\.0+$/, '').replace(/\.$/, '');
    }

    function syncCurrencyViewControl() {
        $('[data-currency-view]').each(function () {
            var active = ($(this).data('currency-view') === 'reference') === showReferenceCurrency;
            $(this).toggleClass('is-active', active).attr('aria-pressed', active ? 'true' : 'false');
        });
        $('#ProductCurrencyViewHint').text(showReferenceCurrency
            ? 'TL tutarlarının yanında kaynak döviz, fatura kuru ve referans satış gösteriliyor.'
            : 'Satış ve maliyetler yalnızca TL gösteriliyor.');
    }

    function applyCurrencyView(showReference, persist) {
        showReferenceCurrency = Boolean(showReference);
        if (persist) {
            localStorage.setItem('productShowReferenceCurrency', String(showReferenceCurrency));
        }
        syncCurrencyViewControl();
        if (!dataTable) {
            return;
        }
        referenceCurrencyColumns.forEach(function (columnIndex) {
            dataTable.column(columnIndex).visible(showReferenceCurrency, false);
        });
        dataTable.rows().invalidate();
        dataTable.columns.adjust().draw(false);
    }

    function currentRate(currency) {
        return currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate);
    }

    function chainDiscount($form, value, fieldPrefix) {
        var result = Number(value || 0);
        for (var index = 1; index <= 4; index++) {
            var discount = Math.min(100, Math.max(0, Number($form.find('#Product_' + fieldPrefix + index).val()) || 0));
            result *= 1 - discount / 100;
        }
        return result;
    }

    function syncPurchaseRate($form, forceCurrentRate) {
        if (!$form || !$form.length) return;
        var currency = $form.find('#Product_PurchaseCurrency').val() || 'TRY';
        var $rate = $form.find('[data-purchase-rate]');
        var $override = $form.find('[data-rate-override]');
        var isOverride = $override.prop('checked');

        if (currency === 'TRY') {
            $rate.val('1').prop('readonly', true);
            $override.prop('checked', false).prop('disabled', true);
            return;
        }

        $override.prop('disabled', false);
        if (isOverride) {
            $rate.prop('readonly', false);
            return;
        }

        $rate.prop('readonly', true);
        var rate = currentRate(currency);
        if (forceCurrentRate && rate > 0) {
            $rate.val(compactInputNumber(rate, 6)).trigger('input');
        }
    }

    function syncDialogPricingMode($form) {
        var dynamic = String($form.find('#Product_PricingMode').val() || '0') === '1';
        var $salePrice = $form.find('#Product_SalePrice');
        var $markup = $form.find('#Product_ProfitMarkupPercent');
        $form.find('[data-manual-sale-field]').toggleClass('is-automatic', dynamic);
        $salePrice.prop('disabled', dynamic).attr('aria-disabled', dynamic ? 'true' : 'false');
        $form.find('[data-markup-field]').removeClass('is-automatic');
        $markup.prop('readonly', false).attr('aria-readonly', 'false');
        $form.find('[data-sale-price-label]').text(dynamic
            ? 'Otomatik satış fiyatı (KDV\'siz)'
            : 'Manuel satış fiyatı (KDV\'siz)');
        $form.find('[data-markup-label]').text(dynamic
            ? 'Hedef kâr ekleme oranı (%)'
            : 'Hedef / hesaplanan kâr ekleme oranı (%)');
        $form.find('[data-sale-price-hint]').text(dynamic
            ? 'Maliyet ve hedef oran üzerinden otomatik hesaplanır.'
            : 'Satışta kullanılacak liste fiyatını girin.');
        $form.find('[data-markup-hint]').text(dynamic
            ? 'Dinamik fiyatın ana girdisidir.'
            : 'Fiyatı veya oranı değiştirebilirsiniz; diğeri maliyete göre anında eşitlenir.');
        $form.find('[data-pricing-mode-explainer]').text(dynamic
            ? 'Satış fiyatı, maliyetin döviz tutarı güncel kura taşınarak ve hedef kâr oranı eklenerek hesaplanır.'
            : 'Sabit satış fiyatı TL olarak korunur ve kur değişiminden etkilenmez.');
        return dynamic;
    }

    function refreshCreateDialogHero($form) {
        if (!$form || !$form.length) return;
        var purchaseCurrency = $form.find('#Product_PurchaseCurrency').val() || 'TRY';
        var purchaseList = Math.max(0, Number($form.find('#Product_PurchaseListPrice').val()) || 0);
        var netPurchase = chainDiscount($form, purchaseList, 'PurchaseDiscount');
        var approvedReferenceCost = Number($form.attr('data-approved-reference-cost')) || 0;
        var approvedTryCost = Number($form.attr('data-approved-try-cost')) || 0;
        var purchaseRate = Number($form.find('[data-purchase-rate]').val());
        var hasPurchaseRate = purchaseCurrency === 'TRY' || purchaseRate > 0;
        purchaseRate = purchaseCurrency === 'TRY' ? 1 : purchaseRate;
        var netCost = hasPurchaseRate ? netPurchase * purchaseRate : null;
        var dynamic = syncDialogPricingMode($form);
        var markup = Math.max(0, Number($form.find('#Product_ProfitMarkupPercent').val()) || 0);
        var livePurchaseRate = currentRate(purchaseCurrency);
        var dynamicReferenceCost = approvedReferenceCost > 0 ? approvedReferenceCost : netPurchase;
        var dynamicCost = livePurchaseRate > 0 ? dynamicReferenceCost * livePurchaseRate : null;
        var pricingSource = $form.data('pricingSource') || 'sale';
        var listSale = dynamic
            ? (dynamicCost == null ? null : dynamicCost * (1 + markup / 100))
            : pricingSource === 'markup' && (approvedTryCost > 0 || netCost != null)
                ? (approvedTryCost > 0 ? approvedTryCost : netCost) * (1 + markup / 100)
                : Math.max(0, Number($form.find('#Product_SalePrice').val()) || 0);
        var netSale = listSale == null ? null : chainDiscount($form, listSale, 'SalesDiscount');
        var salesDiscount = listSale == null || netSale == null ? null : Math.max(0, listSale - netSale);
        var vatRate = Math.min(100, Math.max(0, Number($form.find('#Product_VatRate').val()) || 0));
        var vatAmount = netSale == null ? null : netSale * vatRate / 100;
        var finalPrice = netSale == null ? null : netSale + vatAmount;
        var displayedCost = dynamic ? dynamicCost : (approvedTryCost > 0 ? approvedTryCost : netCost);
        var salePriceMissing = !dynamic && (!listSale || listSale <= 0);
        var profit = displayedCost == null || netSale == null || salePriceMissing ? null : netSale - displayedCost;
        var margin = netSale > 0 && profit != null ? profit / netSale * 100 : null;
        var realizedMarkup = displayedCost > 0 && profit != null ? profit / displayedCost * 100 : null;
        var effectivePurchaseDiscount = purchaseList > 0 ? (1 - netPurchase / purchaseList) * 100 : 0;
        var displayedRate = dynamic ? livePurchaseRate : purchaseRate;
        var hasDisplayedRate = purchaseCurrency === 'TRY' || displayedRate > 0;
        var text = function (amount) { return amount == null ? 'Kur bekleniyor' : formatMoney(amount, 'TRY'); };

        if (dynamic && listSale != null) {
            $form.find('#Product_SalePrice').val(compactInputNumber(listSale, 6));
        }
        if (!dynamic) {
            if (pricingSource === 'markup' && listSale != null) {
                $form.find('#Product_SalePrice').val(compactInputNumber(listSale, 6));
            }
            var reverseMarkup = displayedCost != null && displayedCost > 0 && listSale != null
                ? Math.max(0, (listSale / displayedCost - 1) * 100)
                : 0;
            if (pricingSource !== 'markup') {
                $form.find('#Product_ProfitMarkupPercent').val(compactInputNumber(reverseMarkup, 6));
            }
        }

        $form.find('[data-dialog-value="net-purchase-currency"]').text(
            formatDecimal(dynamic && approvedReferenceCost > 0 ? approvedReferenceCost : netPurchase, 4) + ' ' + purchaseCurrency);
        $form.find('[data-dialog-value="cost"]').text(text(displayedCost));
        $form.find('[data-dialog-value="list"]').text(text(listSale));
        $form.find('[data-dialog-value="sales-discount"]').text(salesDiscount == null ? '—' : '-' + formatMoney(salesDiscount, 'TRY'));
        $form.find('[data-dialog-value="net-sale"]').text(text(netSale));
        $form.find('[data-dialog-value="vat-label"]').text('KDV (%' + formatDecimal(vatRate, 0) + ')');
        $form.find('[data-dialog-value="vat-amount"]').text(text(vatAmount));
        $form.find('[data-dialog-value="final"]').text(text(finalPrice));
        $form.find('[data-dialog-value="profit"]').text(salePriceMissing ? '—' : (profit == null ? 'Hesaplanamadı' : formatMoney(profit, 'TRY')));
        $form.find('[data-dialog-value="margin"]').text(salePriceMissing
            ? 'Satış fiyatı giriniz'
            : (margin == null ? 'Brüt kâr marjı hesaplanamadı' : 'Brüt kâr marjı: %' + formatDecimal(margin, 2)));
        $form.find('[data-dialog-value="realized-markup"]').text(salePriceMissing
            ? 'Satış fiyatı giriniz'
            : (realizedMarkup == null ? 'Gerçekleşen markup hesaplanamadı' : 'Gerçekleşen markup: %' + formatDecimal(realizedMarkup, 2)));
        $form.find('[data-dialog-value="meta"]').text(
            !hasDisplayedRate
                ? purchaseCurrency + '/TRY işlem kuru gerekli.'
                : dynamic && approvedReferenceCost > 0
                    ? 'Onaylı maliyet profili · güncel kur ' + formatDecimal(displayedRate, 4)
                : '%' + formatDecimal(effectivePurchaseDiscount, 2) + ' bileşik alış iskontosu · kur ' + formatDecimal(displayedRate, 4)
        );
        $form.find('.product-finance-profit').attr('data-profit-state', salePriceMissing
            ? 'placeholder'
            : (profit == null || profit === 0 ? 'neutral' : (profit < 0 ? 'loss' : 'profit')));
    }

    function refreshDialogHeroAfterOpen(selector) {
        [80, 260, 600].forEach(function (delay) {
            window.setTimeout(function () {
                $(selector).each(function () {
                    var $form = $(this);
                    normalizeProductDecimalDisplay($form);
                    refreshCreateDialogHero($form);
                });
            }, delay);
        });
    }

    // Database precision is deliberately higher than the precision a user
    // should have to read.  Keep the numeric value intact, but avoid opening a
    // card with distracting padded decimal placeholders.
    function normalizeProductDecimalDisplay($form) {
        if (!$form || !$form.length || $form.data('product-decimals-normalized')) return;

        $form.find('input[type="number"]').each(function () {
            var raw = String(this.value || '').trim();
            if (!raw) return;

            var value = Number(raw);
            if (!Number.isFinite(value)) return;

            var display = compactInputNumber(value, 6);
            this.value = display === '-0' ? '0' : display;
        });

        $form.data('product-decimals-normalized', true);
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function toTry(value, currency) {
        var rate = currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate);
        return rate > 0 ? Number(value || 0) * rate : null;
    }

    function isDynamicPrice(row) {
        return Boolean(row.useCurrentExchangeRateForSale || row.pricingMode === 1);
    }

    function hasLastPurchaseCost(row) {
        return Number(row.lastPurchaseCostCurrencyAmount || 0) > 0 ||
            Number(row.lastPurchaseCostTryAmount || 0) > 0;
    }

    function renderProductIdentity(row) {
        var catalog = [row.brandName, row.categoryName].filter(Boolean).join(' · ');
        return '<div class="product-table-name">' + escapeHtml(row.name || '') + '</div>' +
            '<div class="product-code-main">' + escapeHtml(row.sku || '-') + '</div>' +
            '<div class="product-code-list">' +
                (row.barcode ? '<span>Barkod: <strong>' + escapeHtml(row.barcode) + '</strong></span>' : '') +
                (row.oemCodes ? '<span title="OEM: ' + escapeHtml(row.oemCodes) + '">OEM: <strong>' + escapeHtml(row.oemCodes) + '</strong></span>' : '') +
            '</div>' +
            (catalog ? '<div class="product-table-subtitle">' + escapeHtml(catalog) + '</div>' : '');
    }

    function renderStock(row) {
        var quantity = Number(row.quantity || 0);
        var tracksStock = row.trackStock !== false;
        var isZero = tracksStock && quantity <= 0;
        var isCritical = tracksStock && !isZero && quantity <= Number(row.criticalStockLevel || 0);
        var statusClass = !tracksStock ? 'is-untracked' : (isZero ? 'is-zero' : (isCritical ? 'is-critical' : 'is-normal'));
        var statusText = !tracksStock ? 'Stok takibi kapalı' : (isZero ? 'Sıfır stok' : (isCritical ? 'Kritik stok' : 'Normal stok'));
        var stockValue = tracksStock ? formatDecimal(quantity, 4) + ' ' + escapeHtml(row.unit || '') : 'Takip edilmiyor';
        return '<div class="product-stock-value ' + statusClass + '">' + stockValue + '</div>' +
            '<div class="product-stock-status ' + statusClass + '">' + statusText + '</div>';
    }

    function renderPricingBehavior(row) {
        var dynamic = isDynamicPrice(row);
        var price = dynamic ? 'Döviz bazlı dinamik' : 'Manuel fiyat';
        var detail = dynamic
            ? (hasLastPurchaseCost(row)
                ? (escapeHtml(row.purchaseCurrency || row.lastPurchaseCostCurrencyCode || 'TRY') + ' referanslı · %' + Number(row.profitMarkupPercent || 0).toFixed(2) + ' markup')
                : 'Onaylı alış maliyeti bekleniyor')
            : 'Sabit TL kart fiyatı';
        return '<div class="product-pricing-status ' + (dynamic ? 'is-dynamic' : 'is-manual') + '"><span class="material-icons-round" aria-hidden="true">' + (dynamic ? 'currency_exchange' : 'sell') + '</span><span>' + price + '</span></div>' +
            '<div class="product-table-subtitle">' + detail + '</div>';
    }

    function renderCostSource(row) {
        if (row.isIncompleteProfile) {
            return '<div class="product-cost-empty"><span class="material-icons-round" aria-hidden="true">edit_note</span><div><strong>Eksik bilgili ürün</strong><span>Sayım sırasında hızlı oluşturuldu; ürün kartını tamamlayın.</span></div></div>';
        }
        if (!hasLastPurchaseCost(row)) {
            return '<div class="product-cost-empty"><span class="material-icons-round" aria-hidden="true">receipt_long</span><div><strong>Maliyet bilgisi yok</strong><span>İlk onaylı alış faturasıyla oluşur.</span></div></div>';
        }

        var sourceCurrency = row.lastPurchaseSourceCurrencyCode || 'TRY';
        var sourceValue = row.lastPurchaseSourceNetUnitCost;
        var date = row.lastPurchaseDate ? luxon.DateTime.fromISO(row.lastPurchaseDate).toFormat('dd.MM.yyyy') : '';
        var source = [row.lastPurchaseDocumentNumber, row.lastPurchaseSupplierName].filter(Boolean).join(' · ');
        var sourceLine = source || 'Onaylı alış faturası';
        var netCost = sourceValue != null
            ? formatMoney(sourceValue, sourceCurrency)
            : formatMoney(row.lastPurchaseCostTryAmount, 'TRY');
        var rate = Number(row.lastPurchaseExchangeRate || row.lastPurchaseSourceExchangeRate || 0);
        return '<div class="product-cost-value product-cost-source-title" title="' + escapeHtml(sourceLine) + '">' + escapeHtml(sourceLine) + '</div>' +
            '<div class="product-cost-source">' + escapeHtml(netCost) + (rate > 0 ? ' · 1 ' + escapeHtml(row.purchaseCurrency || 'TRY') + ' = ' + formatDecimal(rate, 4) + ' TRY' : '') + '</div>' +
            '<div class="product-table-subtitle">' + escapeHtml(date) + '</div>';
    }

    function renderReferenceCost(row) {
        if (!hasLastPurchaseCost(row)) {
            return '<div class="product-table-empty"><strong>—</strong><span>Onaylı maliyet oluşmadı</span></div>';
        }

        var currency = row.purchaseCurrency || row.lastPurchaseCostCurrencyCode || 'TRY';
        var currentTryCost = row.currentNetCostInTry != null ? row.currentNetCostInTry : row.lastPurchaseCostTryAmount;
        var referenceDetail = showReferenceCurrency && Number(row.lastPurchaseCostCurrencyAmount || 0) > 0
            ? '<div class="product-table-subtitle">' + escapeHtml(formatMoney(row.lastPurchaseCostCurrencyAmount, currency)) + ' referans maliyet</div>'
            : '';
        return '<div class="product-cost-value">' + escapeHtml(formatMoney(currentTryCost, 'TRY')) + '</div>' +
            '<div class="product-table-subtitle">Güncel net TL maliyet</div>' + referenceDetail;
    }

    function renderBrandCategory(row) {
        return '<div class="product-table-compact">' + escapeHtml(row.brandName || 'Marka yok') + '</div>' +
            '<div class="product-table-subtitle">' + escapeHtml(row.categoryName || 'Kategori yok') + '</div>';
    }

    function renderOptionalDate(row) {
        return row.lastPurchaseDate
            ? escapeHtml(luxon.DateTime.fromISO(row.lastPurchaseDate).toFormat('dd.MM.yyyy'))
            : '<span class="product-table-muted">-</span>';
    }

    function renderOptionalMoney(value, currency) {
        return value == null || Number(value) <= 0
            ? '<span class="product-table-muted">-</span>'
            : escapeHtml(formatMoney(value, currency || 'TRY'));
    }

    function renderOptionalReferenceSale(row) {
        var currency = row.purchaseCurrency || row.lastPurchaseCostCurrencyCode || 'TRY';
        return row.referenceSaleUnitPrice != null && Number(row.referenceSaleUnitPrice) > 0
            ? '<div class="product-cost-value">' + escapeHtml(formatMoney(row.referenceSaleUnitPrice, currency)) + '</div>' +
                '<div class="product-table-subtitle">KDV hariç referans liste fiyatı</div>'
            : '<span class="product-table-muted">-</span>';
    }

    function renderSalePreview(row) {
        var dynamic = isDynamicPrice(row);
        if (!row.isSuggestedSaleAvailable && dynamic && !hasLastPurchaseCost(row)) {
            return '<div class="product-sale-unavailable"><span class="material-icons-round" aria-hidden="true">schedule</span><div><strong>Hesaplanmayı bekliyor</strong><span>Onaylı alış maliyeti yok.</span></div></div>';
        }

        if (!row.isSuggestedSaleAvailable) {
            return '<div class="product-sale-unavailable"><span class="material-icons-round" aria-hidden="true">cloud_off</span><div><strong>Kur bekleniyor</strong><span>Fiyat satışta backend tarafından hesaplanır.</span></div></div>';
        }

        if (!dynamic && Number(row.suggestedSaleUnitPrice || 0) <= 0) {
            return '<div class="product-sale-unavailable is-action-needed"><span class="material-icons-round" aria-hidden="true">sell</span><div><strong>Satış fiyatı girilmedi</strong><span>Ürün kartından manuel TL fiyatı belirleyin.</span></div></div>';
        }

        var suggested = formatMoney(row.suggestedSaleUnitPrice, 'TRY');
        var rateDate = row.suggestedSaleRateDate
            ? luxon.DateTime.fromISO(row.suggestedSaleRateDate).toFormat('dd.MM.yyyy HH:mm')
            : '';
        var pricingRate = Number(row.currentReferenceCurrencyExchangeRate || 0);
        var pricingRateText = dynamic && row.purchaseCurrency !== 'TRY' && pricingRate > 0
            ? 'Güncel kur: 1 ' + escapeHtml(row.purchaseCurrency) + ' = ' + formatDecimal(pricingRate, 4) + ' TRY'
            : (rateDate ? 'Kur zamanı: ' + escapeHtml(rateDate) : 'TRY baz para birimi');
        return '<div class="product-sale-value">' + escapeHtml(suggested) + '</div>' +
            '<div class="product-sale-live"><span></span>' + (dynamic ? 'Güncel kurla dinamik' : 'Manuel fiyatın güncel görünümü') + '</div>' +
            '<div class="product-table-subtitle">' + pricingRateText + '</div>';
    }

    function updateSummary() {
        noxStock.products.product.getInventorySummary().then(function (summary) {
            $('#ProductVisibleCount').text(Number(summary.totalCount || 0));
            $('#ProductDynamicCount').text(Number(summary.dynamicPricingCount || 0));
            $('#ProductMissingCostCount').text(Number(summary.missingCostCount || 0));
            $('#ProductAttentionStockCount').text(Number(summary.attentionStockCount || 0));
        }).catch(function () {
            $('#ProductVisibleCount, #ProductDynamicCount, #ProductMissingCostCount, #ProductAttentionStockCount').text('—');
        });
    }

    var lookupManagerType = 'brand';

    function lookupManagerConfig() {
        return lookupManagerType === 'brand'
            ? { service: noxStock.brands.brand, label: 'marka', selector: '#Product_BrandId', filterSelector: '#ProductBrandFilter' }
            : { service: noxStock.categories.category, label: 'kategori', selector: '#Product_CategoryId', filterSelector: '#ProductCategoryFilter' };
    }

    function renderLookupManager(items) {
        var config = lookupManagerConfig();
        var $list = $('#ProductLookupManagerList').empty();
        if (!items.length) {
            $list.html('<p class="px-4 py-8 text-center text-sm text-slate-500">Henüz kayıt yok.</p>');
            return;
        }
        items.forEach(function (item) {
            $list.append('<div class="flex items-center justify-between gap-3 px-4 py-3">' +
                '<span class="min-w-0 truncate text-sm font-medium text-slate-800 dark:text-slate-100">' + escapeHtml(item.name) + '</span>' +
                '<button type="button" class="product-lookup-delete shrink-0 text-rose-600 hover:text-rose-700" data-id="' + item.id + '" aria-label="' + escapeHtml(item.name) + ' ' + config.label + 'sını sil" title="Sil"><span class="material-icons-round text-base">delete</span></button>' +
                '</div>');
        });
    }

    function loadLookupManager() {
        var config = lookupManagerConfig();
        var $list = $('#ProductLookupManagerList');
        $list.html('<p class="px-4 py-8 text-center text-sm text-slate-500">Yükleniyor…</p>');
        config.service.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' }).then(function (result) {
            renderLookupManager(result.items || []);
        }).catch(function () {
            $list.html('<p class="px-4 py-8 text-center text-sm text-rose-600">Kayıtlar yüklenemedi.</p>');
        });
    }

    function addLookupManagerOption(item) {
        var config = lookupManagerConfig();
        $(config.selector + ', ' + config.filterSelector).each(function () {
            if (!$(this).find('option[value="' + item.id + '"]').length) {
                $(this).append(new Option(item.name, item.id));
            }
        });
    }

    function updateEquivalent(priceInput, currencyInput, target) {
        var value = Number(priceInput.val());
        var currency = currencyInput.val();
        var tryValue = toTry(value, currency);
        target.text(value && currency !== 'TRY'
            ? (tryValue == null ? 'Güncel kur bekleniyor' : 'Yaklaşık ' + formatMoney(tryValue, 'TRY'))
            : '');
    }

    function updatePriceEquivalents() {
        updateEquivalent($('#Product_PurchasePrice'), $('#Product_PurchaseCurrency'), $('#PurchaseTryEquivalent'));
    }

    function clampPercent(value) {
        return Math.min(100, Math.max(0, Number(value) || 0));
    }

    function applyDiscountChain(value, matrixName) {
        var result = Number(value || 0);
        $('[data-discount-matrix="' + matrixName + '"] input').each(function () {
            result *= 1 - (clampPercent($(this).val()) / 100);
        });
        return result;
    }

    function updateCreateFinanceHero() {
        var purchaseCurrency = $('#Product_PurchaseCurrency').val() || 'TRY';
        var purchaseListTryRaw = toTry($('#Product_PurchasePrice').val(), purchaseCurrency);
        var purchaseRateAvailable = purchaseListTryRaw != null;
        var purchaseListTry = purchaseListTryRaw || 0;
        var netCostTry = applyDiscountChain(purchaseListTry, 'purchase');
        var modeValue = String($('#Product_PricingMode').val() || '0');
        var dynamicMode = modeValue === '1' || modeValue.indexOf('Dynamic') > -1 || modeValue.indexOf('Döviz') > -1;
        var markup = Math.max(0, Number($('#Product_ProfitMarkupPercent').val()) || 0);
        var manualSaleTryRaw = Number($('#Product_SalePrice').val());
        var saleRateAvailable = dynamicMode ? purchaseRateAvailable : manualSaleTryRaw != null;
        var manualSaleTry = manualSaleTryRaw || 0;
        var listSaleTry = dynamicMode ? netCostTry * (1 + markup / 100) : manualSaleTry;
        var netSaleTry = applyDiscountChain(listSaleTry, 'sale');
        var saleDiscountTry = Math.max(0, listSaleTry - netSaleTry);
        var vatRate = clampPercent($('#ProductPreviewVatRate').val());
        var vatSaleTry = netSaleTry * (1 + vatRate / 100);
        var profitTry = netSaleTry - netCostTry;
        var margin = netSaleTry > 0 ? (profitTry / netSaleTry) * 100 : 0;
        var effectivePurchaseDiscount = purchaseListTry > 0 ? (1 - netCostTry / purchaseListTry) * 100 : 0;

        $('#ProductHeroNetCost').text(purchaseRateAvailable ? formatMoney(netCostTry, 'TRY') : 'Kur bekleniyor');
        $('#ProductHeroCostMeta').text(
            !purchaseRateAvailable
                ? purchaseCurrency + '/TRY kuru alınmadan maliyet gösterilmez.'
                : purchaseCurrency === 'TRY'
                ? 'Zincirleme alış iskontosu: %' + effectivePurchaseDiscount.toFixed(2)
                : formatMoney(Number($('#Product_PurchasePrice').val()) || 0, purchaseCurrency) + ' · güncel kur · %' + effectivePurchaseDiscount.toFixed(2) + ' iskonto'
        );
        $('#ProductHeroListSale').text(saleRateAvailable ? formatMoney(listSaleTry, 'TRY') : 'Kur bekleniyor');
        $('#ProductHeroSaleDiscount').text(saleRateAvailable ? '-' + formatMoney(saleDiscountTry, 'TRY') : '—');
        $('#ProductHeroNetSale').text(saleRateAvailable ? formatMoney(netSaleTry, 'TRY') : '—');
        $('#ProductHeroVatSale').text(saleRateAvailable ? formatMoney(vatSaleTry, 'TRY') : '—');
        $('#ProductHeroProfit').text(purchaseRateAvailable && saleRateAvailable ? formatMoney(profitTry, 'TRY') : 'Hesaplanamadı');
        $('#ProductHeroMargin').text(purchaseRateAvailable && saleRateAvailable ? '%' + margin.toFixed(2) + ' satış marjı' : 'Güncel kur gerekli');
        $('.product-finance-profit').attr('data-profit-state', profitTry < 0 ? 'loss' : (profitTry > 0 ? 'profit' : 'neutral'));
    }

    function activateCreateSection(sectionName) {
        var $section = $('[data-product-section="' + sectionName + '"]');
        if (!$section.length) {
            return;
        }
        $('.product-editor-tab').toggleClass('is-active', false).filter('[data-product-tab="' + sectionName + '"]').addClass('is-active');
        $section[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
        window.setTimeout(function () {
            $section.find('input:not([type="hidden"]), select, textarea').first().trigger('focus');
        }, 250);
    }

    function generateSku() {
        var now = new Date();
        var stamp = [
            now.getFullYear(),
            String(now.getMonth() + 1).padStart(2, '0'),
            String(now.getDate()).padStart(2, '0'),
            String(now.getHours()).padStart(2, '0'),
            String(now.getMinutes()).padStart(2, '0'),
            String(now.getSeconds()).padStart(2, '0')
        ].join('');
        $('#Product_Sku').val('STK-' + stamp).trigger('input').trigger('focus').select();
    }

    function openEditor() {
        editor.prop('hidden', false);
        updateCreateFinanceHero();
        editor[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
        window.setTimeout(function () { $('#Product_Name').trigger('focus'); }, 250);
    }

    function closeEditor() {
        editor.prop('hidden', true);
        $('#NewProductButton').trigger('focus');
    }

    function calculateSalePrice() {
        $('#Product_PricingMode').val('1').trigger('change');
        $('#SaleCalculationInfo').text('Dinamik fiyat için ürün kartını kaydedin ve alış faturasını onaylayın. Referans maliyet elle girilmez; onaylı faturadan otomatik oluşur.');
    }

    function initCreateDialog($modal) {
        var $form = $modal.is('.product-create-dialog-form')
            ? $modal
            : $modal.find('.product-create-dialog-form').first();
        if (!$form.length) {
            $form = $modal.closest('.product-create-dialog-form');
        }
        if (!$form.length || $form.data('noxstock-create-ready')) return;
        $form.data('noxstock-create-ready', true);
        $form.find('[data-dialog-tab]').first().attr('aria-selected', 'true');
        normalizeProductDecimalDisplay($form);
        $form.on('keydown', function (event) {
            if (event.key === 'Escape') { event.preventDefault(); $modal.modal('hide'); }
            else if (event.key === 'Enter' && !event.isComposing && !$(event.target).is('textarea, button, select')) {
                event.preventDefault();
                if (this.requestSubmit) this.requestSubmit();
                else $modal.find('.modal-footer button[type="submit"]').trigger('click');
            }
        });
        syncPurchaseRate($form, false);
        refreshCreateDialogHero($form);
        fetchRates().then(function () {
            var shouldUseCurrentRate = !$form.hasClass('product-edit-dialog-form');
            syncPurchaseRate($form, shouldUseCurrentRate);
            refreshCreateDialogHero($form);
        });
    }

    // ABP invokes initModal for every open, before the Bootstrap animation. This
    // avoids relying on a document-level event that may fire before the modal's
    // dynamically loaded content is available.
    abp.modals.ProductCreateModal = function () {
        return {
            initModal: function (publicApi) {
                initCreateDialog(publicApi.getModal());
            }
        };
    };

    function fetchRates() {
        return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (data) {
                    rates = { USD: data.usd || data.USD, EUR: data.eur || data.EUR };
                }
                updatePriceEquivalents();
                updateCreateFinanceHero();
                $('.product-create-dialog-form').each(function () {
                    var $form = $(this);
                    syncPurchaseRate($form, false);
                    refreshCreateDialogHero($form);
                });
                refreshTablePrices();
            })
            .catch(function () {
                updatePriceEquivalents();
                updateCreateFinanceHero();
                $('.product-create-dialog-form').each(function () {
                    var $form = $(this);
                    syncPurchaseRate($form, false);
                    refreshCreateDialogHero($form);
                });
                refreshTablePrices();
            });
    }

    function initEditPricing($modal) {
        var $dashboard = $modal.find('#ProductPricingDashboard');
        if (!$dashboard.length || $dashboard.data('pricing-ready')) {
            return;
        }

        $dashboard.data('pricing-ready', true);
        var dashboard = $dashboard[0];
        var editRates = {};

        function editFormatMoney(value, currency) {
            var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
            return new Intl.NumberFormat(locale, {
                style: 'currency',
                currency: currency || 'TRY',
                minimumFractionDigits: 2,
                maximumFractionDigits: 4
            }).format(Number(value || 0));
        }

        function editRate(currency) {
            return currency === 'TRY' ? 1 : Number(editRates[currency] && editRates[currency].rate);
        }

        function updateEditPricing() {
            var sourceCost = Number(dashboard.dataset.sourceCost || 0);
            var purchaseReferenceRate = Number(dashboard.dataset.referenceRate || 0);
            var tryCost = Number(dashboard.dataset.tryCost || 0);
            var referenceCost = Number(dashboard.dataset.referenceCost || 0);
            var referenceCurrency = dashboard.dataset.referenceCurrency || 'TRY';
            var sourceCurrency = dashboard.dataset.sourceCurrency || 'TRY';
            var markupInput = $modal.find('#Product_ProfitMarkupPercent');
            var markup = Number(markupInput.val());
            if (!Number.isFinite(markup) || markup <= 0) {
                markup = 30;
                markupInput.val('30');
            }

            var mode = Number($modal.find('#Product_PricingMode').val());
            var currentReferenceRate = editRate(referenceCurrency);
            var hasCost = referenceCost > 0 || tryCost > 0;
            var $warning = $modal.find('#EditPricingWarning');

            $modal.find('#EditSourcePurchaseCost').text(hasCost && sourceCost > 0 ? editFormatMoney(sourceCost, sourceCurrency) : 'Maliyet bilgisi yok');
            $modal.find('#EditSourcePurchaseMeta').text(hasCost
                ? 'Net birim maliyet · TRY karşılığı: ' + editFormatMoney(tryCost || sourceCost, 'TRY')
                : 'Onaylı alış faturası bekleniyor.');
            $modal.find('#EditSourceDocument').text(dashboard.dataset.documentNumber || '-');
            $modal.find('#EditSourceSupplier').text(dashboard.dataset.supplierName || '-');
            $modal.find('#EditSourceRate').text(hasCost && purchaseReferenceRate > 0
                ? '1 ' + referenceCurrency + ' = ' + formatDecimal(purchaseReferenceRate, 4) + ' TRY'
                : '-');
            $modal.find('#EditSourceDate').text(dashboard.dataset.purchaseDate || '-');
            $modal.find('#EditReferenceCost').text(hasCost && referenceCost > 0 ? editFormatMoney(referenceCost, referenceCurrency) : 'Maliyet bilgisi yok');
            $modal.find('#EditReferenceMeta').text(hasCost
                ? 'Referans: ' + referenceCurrency + ' · Güncel ' + referenceCurrency + '/TRY kuru: ' + (currentReferenceRate ? formatDecimal(currentReferenceRate, 4) : 'bekleniyor')
                : 'TL maliyet, seçili referans dövizinde saklanır.');
            $modal.find('#EditPricingModeStatus').text(mode === 1 ? 'Dinamik fiyat aktif' : 'Manuel fiyat modu');
            $modal.find('#EditPricingModeStatus').toggleClass('is-dynamic', mode === 1).toggleClass('is-manual', mode !== 1);

            if (!hasCost) {
                $modal.find('#EditSuggestedSale').text('Hesaplanamaz');
                $modal.find('#EditSuggestedSaleMeta').text('Önce onaylı bir alış faturasıyla maliyet snapshotı oluşturun.');
                $warning.prop('hidden', true);
                return;
            }

            if (!currentReferenceRate) {
                $modal.find('#EditSuggestedSale').text('Kur bekleniyor');
                $modal.find('#EditSuggestedSaleMeta').text('Güncel kur alınamadığı için satış önerisi hesaplanamadı.');
                $warning.text('Güncel kur bulunamadı. Fiyat 0 gösterilmez; kur geldiğinde öneri otomatik yenilenir.').prop('hidden', false);
                return;
            }

            var referenceCostInTry = referenceCost > 0
                ? referenceCost * currentReferenceRate
                : (tryCost || sourceCost);
            var referenceSale = referenceCost * (1 + markup / 100);
            var suggestedTry = referenceCostInTry * (1 + markup / 100);
            var suggestedSale = suggestedTry;
            $modal.find('#EditReferenceSale').text(editFormatMoney(referenceSale, referenceCurrency));
            $modal.find('#EditCurrentRate').text('Satış para birimi: TRY');
            $modal.find('#EditSuggestedSale').text(editFormatMoney(suggestedSale, 'TRY'));
            $modal.find('#EditSuggestedSaleMeta').text('Güncel ' + referenceCurrency + '/TRY kuru ve %' + markup.toFixed(2) + ' markup ile hesaplandı.');
            $warning.text(mode === 1
                ? 'Dinamik satış fiyatı aktif. POS ve satış faturası bu maliyet, güncel kur ve markup üzerinden hesaplar.'
                : 'Bu ürün manuel fiyat modunda. Bu öneriyi satışta kullanmak için fiyatlandırma modunu “Döviz bazlı dinamik” yapıp kaydedin.').prop('hidden', false);
        }

        function loadEditRates() {
            return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (data) {
                    editRates = data ? { USD: data.usd || data.USD, EUR: data.eur || data.EUR } : {};
                    updateEditPricing();
                })
                .catch(updateEditPricing);
        }

        function syncEditModePicker() {
            var modeStr = String($modal.find('#Product_PricingMode').val() || '0');
            var dynamic = modeStr === '1' || modeStr.indexOf('Döviz') > -1 || modeStr === 'Dynamic' || modeStr.indexOf('Dynamic') > -1;
            var compareMode = dynamic ? '1' : '0';
            $modal.find('[data-pricing-mode-picker] .product-pricing-mode-option').each(function () {
                $(this).toggleClass('is-selected', String($(this).data('pricing-mode')) === compareMode);
            });
            $modal.find('#EditEnableDynamicPricing').prop('hidden', dynamic);
            $modal.find('#EditPricingModeHint').text(dynamic
                ? 'Satış ekranları güncel kurla öneri üretir.'
                : 'Elle girilen satış fiyatı kullanılır.');
            $modal.find('[data-pricing-manual-field]').prop('hidden', dynamic);
        }

        $modal.find('#Product_PurchaseCurrency, #Product_PricingMode, #Product_ProfitMarkupPercent').on('input change', function () {
            syncEditModePicker();
            updateEditPricing();
        });
        $modal.find('[data-pricing-mode-picker] .product-pricing-mode-option').on('click', function () {
            var targetMode = String($(this).data('pricing-mode'));
            var targetText = targetMode === '1' ? 'Döviz' : 'Manuel';
            var exactVal = targetMode;
            $modal.find('#Product_PricingMode option').each(function() {
                if (this.value === targetMode || this.text.indexOf(targetText) > -1 || this.value.indexOf(targetText) > -1) {
                    exactVal = this.value;
                }
            });
            $modal.find('#Product_PricingMode').val(exactVal).trigger('change');
        });
        $modal.find('#EditEnableDynamicPricing').on('click', function () {
            var exactVal = '1';
            $modal.find('#Product_PricingMode option').each(function() {
                if (this.value === '1' || this.text.indexOf('Döviz') > -1 || this.value.indexOf('Dynamic') > -1) {
                    exactVal = this.value;
                }
            });
            $modal.find('#Product_PricingMode').val(exactVal).trigger('change');
        });
        $modal.find('#EditRefreshPricing').on('click', function () {
            var $button = $(this);
            $button.addClass('is-loading').prop('disabled', true);
            loadEditRates().finally(function () {
                $button.removeClass('is-loading').prop('disabled', false);
            });
        });

        // The modal footer is intentionally a regular form submit button. Keep the
        // selector independent from tenant-prefixed action URLs.
        var $editForm = $modal.find('form').first();
        if ($editForm.length) {
            function submitEditForm(event) {
                event.preventDefault();
                var $form = $(this);
                var $saveButton = $modal.find('#ProductEditSaveButton');
                if (!$saveButton.length) {
                    $saveButton = $modal.find('.modal-footer button[type="submit"]').first();
                }
                var originalHtml = $saveButton.html();
                $saveButton.prop('disabled', true).addClass('is-loading').text('Kaydediliyor...');

                $.ajax({
                    url: $form.attr('action') || (abp.appPath + 'Products/EditModal'),
                    type: 'POST',
                    data: $form.serialize()
                }).done(function () {
                    $modal.modal('hide');
                    abp.notify.success('Ürün başarıyla güncellendi.');
                    if (dataTable) {
                        dataTable.ajax.reload(null, false);
                    }
                }).fail(function (xhr) {
                    var message = 'Ürün kaydedilemedi.';
                    if (xhr.responseJSON && xhr.responseJSON.error && xhr.responseJSON.error.message) {
                        message += ' ' + xhr.responseJSON.error.message;
                    } else if (xhr.status === 400) {
                        message += ' Form alanlarını kontrol edin.';
                    }
                    abp.notify.error(message);
                }).always(function () {
                    $saveButton.prop('disabled', false).removeClass('is-loading').html(originalHtml);
                });
            }

            $editForm.off('submit.noxstockProductEdit').on('submit.noxstockProductEdit', submitEditForm);
            $modal.find('#ProductEditSaveButton').off('click.noxstockProductEdit');
        }
        syncEditModePicker();
        updateEditPricing();
        loadEditRates();
    }

    $(document).on('shown.bs.modal.productsEditPricing', '.modal', function () {
        initEditPricing($(this));
    });
    $(document).on('shown.bs.modal.productsCreatePricing', '.modal', function () {
        initCreateDialog($(this));
    });

    function getFilterPayload() {
        return {
            filter: $('#ProductSearch').val(),
            brandId: $('#ProductBrandFilter').val() || null,
            categoryId: $('#ProductCategoryFilter').val() || null,
            stockStatus: $('#ProductStockStatusFilter').val() || null,
            pricingMode: $('#ProductPricingModeFilter').val() || null,
            costStatus: $('#ProductCostStatusFilter').val() || null,
        };
    }

    function reloadProducts() {
        dataTable.ajax.reload();
    }

    // Exchange-rate changes affect dynamic product prices returned by the
    // server. Keep the table in sync when the rate cache is refreshed, while
    // safely no-oping during page/modal initialization before DataTables is
    // ready.
    function refreshTablePrices() {
        if (!dataTable || !dataTable.ajax) {
            return;
        }
        dataTable.ajax.reload(null, false);
    }

    function resetFilters() {
        $('#ProductSearch, #ProductBrandFilter, #ProductCategoryFilter, #ProductStockStatusFilter, #ProductPricingModeFilter, #ProductCostStatusFilter').val('');
        $('.products-quick-filter').removeClass('is-active');
        $('.products-quick-filter[data-quick-filter="all"]').addClass('is-active');
        reloadProducts();
    }

    function applyQuickFilter(filter) {
        $('#ProductStockStatusFilter, #ProductPricingModeFilter, #ProductCostStatusFilter').val('');
        if (filter === 'dynamic' || filter === 'manual') {
            $('#ProductPricingModeFilter').val(filter);
        } else if (filter === 'missing-cost') {
            $('#ProductCostStatusFilter').val('missing');
        } else if (filter === 'incomplete') {
            $('#ProductCostStatusFilter').val('incomplete');
        } else if (filter === 'attention-stock') {
            $('#ProductStockStatusFilter').val('attention');
        }
        $('.products-quick-filter').removeClass('is-active');
        $('.products-quick-filter[data-quick-filter="' + filter + '"]').addClass('is-active');
        reloadProducts();
    }

    var productColumnWidths = [76, 240, 112, 160, 168, 150, 168, 145, 150, 190, 132, 138, 148, 138, 168, 130];

    function columnWidthKey(columnIndex) {
        // V2 deliberately ignores the former unrestricted 72px preferences.
        // Those values made detailed column titles overlap each other.
        return 'productCatalogColumnWidthV2-' + columnIndex;
    }

    function clampColumnWidth(columnIndex, width) {
        var minimum = productColumnWidths[columnIndex] || 120;
        return Math.max(minimum, Math.min(420, Math.round(width)));
    }

    function applyColumnWidth(columnIndex, width, persist) {
        if (!dataTable) {
            return;
        }
        width = clampColumnWidth(columnIndex, width);
        $('#ProductsTable_wrapper th[data-column-index="' + columnIndex + '"]').css({ width: width + 'px', minWidth: width + 'px' });
        dataTable.column(columnIndex).nodes().to$().css({ width: width + 'px', minWidth: width + 'px' });
        $(dataTable.column(columnIndex).header()).css({ width: width + 'px', minWidth: width + 'px' });
        if (persist) {
            localStorage.setItem(columnWidthKey(columnIndex), String(width));
        }
    }

    function initColumnResizeHandles() {
        var $headers = $('#ProductsTable_wrapper .dataTables_scrollHead th');
        $headers.each(function () {
            var $header = $(this);
            var headerTitle = $header.clone().children().remove().end().text().trim();
            var columnIndex = -1;
            for (var index = 0; index < dataTable.columns().count(); index++) {
                var configuredTitle = $(dataTable.column(index).header()).clone().children().remove().end().text().trim();
                if (configuredTitle === headerTitle) {
                    columnIndex = index;
                    break;
                }
            }
            if (columnIndex < 0) {
                return;
            }
            $header.attr('data-column-index', columnIndex);
            if (!$header.children('.product-column-resize-handle').length) {
                $header.append('<span class="product-column-resize-handle" title="Kolon genişliğini ayarla" aria-hidden="true"></span>');
            }

            var savedWidth = Number(localStorage.getItem(columnWidthKey(columnIndex)) || 0);
            applyColumnWidth(
                columnIndex,
                savedWidth > 0 ? savedWidth : productColumnWidths[columnIndex],
                false);
        });
    }

    function setProductsTableLoading(isLoading) {
        $('#ProductsTableLoading').toggleClass('is-visible', Boolean(isLoading));
    }

    $('#ProductsTable').on('processing.dt', function (event, settings, processing) {
        setProductsTableLoading(processing);
    }).on('xhr.dt', function () {
        setProductsTableLoading(false);
    });

    dataTable = $('#ProductsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        stateSave: true,
        stateDuration: -1,
        searchDelay: 300,
        paging: true,
        processing: true,
        order: [[1, 'asc']],
        searching: false,
        autoWidth: false,
        scrollX: true,
        drawCallback: function () {
            setProductsTableLoading(false);
            initColumnResizeHandles();
        },
        ajax: abp.libs.datatables.createAjax(noxStock.products.product.getList, getFilterPayload),
        columnDefs: [
            {
                title: 'İşlem',
                orderable: false,
                render: function (data, type, row) {
                    return '<div class="product-row-actions">' +
                        '<button type="button" class="product-edit-button" data-id="' + row.id + '" title="Ürünü düzenle" aria-label="' + escapeHtml(row.name || 'Ürünü') + ' düzenle"><span class="material-icons-round" aria-hidden="true">edit</span></button>' +
                        '<button type="button" class="product-delete-button" data-id="' + row.id + '" data-name="' + escapeHtml(row.name || '') + '" title="Ürünü sil" aria-label="' + escapeHtml(row.name || 'Ürünü') + ' sil"><span class="material-icons-round" aria-hidden="true">delete</span></button>' +
                        '</div>';
                }
            },
            { title: 'Ürün ve kodlar', data: 'name', render: function (data, type, row) { return renderProductIdentity(row); } },
            { title: 'Stok', data: 'quantity', render: function (data, type, row) { return renderStock(row); } },
            { title: 'Fiyat politikası', orderable: false, render: function (data, type, row) { return renderPricingBehavior(row); } },
            { title: 'Maliyet kaynağı / fatura', data: 'lastPurchaseDocumentNumber', visible: false, orderable: false, render: function (data, type, row) { return renderCostSource(row); } },
            { title: 'Güncel net TL maliyet', data: 'currentNetCostInTry', render: function (data, type, row) { return renderReferenceCost(row); } },
            { title: 'Güncel TL satış fiyatı', data: 'suggestedSaleUnitPrice', orderable: false, render: function (data, type, row) { return renderSalePreview(row); } },
            { title: 'Marka / kategori', data: 'brandName', visible: false, orderable: false, render: function (data, type, row) { return renderBrandCategory(row); } },
            { title: 'Barkod', data: 'barcode', visible: false, render: function (data, type, row) { return escapeHtml(row.barcode || '-'); } },
            { title: 'OEM kodları', data: 'oemCodes', visible: false, orderable: false, render: function (data, type, row) { return escapeHtml(row.oemCodes || '-'); } },
            { title: 'Son alış tarihi', data: 'lastPurchaseDate', visible: false, render: function (data, type, row) { return renderOptionalDate(row); } },
            { title: 'Net alış (kaynak döviz)', data: 'lastPurchaseSourceNetUnitCost', visible: false, render: function (data, type, row) { return renderOptionalMoney(row.lastPurchaseSourceNetUnitCost, row.lastPurchaseSourceCurrencyCode); } },
            { title: 'Alış fatura kuru', data: 'lastPurchaseExchangeRate', visible: false, render: function (data, type, row) { return Number(data || 0) > 0 ? '1 ' + escapeHtml(row.purchaseCurrency || 'TRY') + ' = ' + formatDecimal(data, 4) + ' TRY' : '<span class="product-table-muted">-</span>'; } },
            { title: 'Alış günü TL maliyeti', data: 'lastPurchaseCostTryAmount', visible: false, render: function (data, type, row) { return renderOptionalMoney(data, 'TRY'); } },
            { title: 'Referans satış fiyatı', data: 'referenceSaleUnitPrice', visible: false, render: function (data, type, row) { return renderOptionalReferenceSale(row); } },
            { title: 'Raf / konum', data: 'shelfLocation', visible: true, orderable: false, render: function (data, type, row) { return escapeHtml(row.shelfLocation || '-'); } }
        ]
    }));

    initColumnResizeHandles();

    $('#NewProductButton').on('click', function () {
        createModal.open();
        refreshDialogHeroAfterOpen('.product-create-dialog-form');
    });
    $('#ProductLookupsModal').on('shown.bs.modal', function () {
        loadLookupManager();
        $('#ProductLookupManagerName').trigger('focus');
    });
    $('[data-lookup-manager-tab]').on('click', function () {
        lookupManagerType = String($(this).data('lookup-manager-tab'));
        $('[data-lookup-manager-tab]').removeClass('is-active');
        $(this).addClass('is-active');
        $('#ProductLookupManagerName').val('').attr('placeholder', lookupManagerType === 'brand' ? 'Yeni marka adı' : 'Yeni kategori adı');
        $('#ProductLookupManagerHint').text(lookupManagerType === 'brand'
            ? 'Markalar ürün kartlarında seçim için kullanılır.'
            : 'Kategoriler ürün kartlarında seçim için kullanılır.');
        loadLookupManager();
    });
    $('#ProductLookupManagerAdd').on('click', function () {
        var config = lookupManagerConfig();
        var $name = $('#ProductLookupManagerName');
        var name = String($name.val() || '').trim();
        if (!name) {
            $name.trigger('focus');
            return;
        }
        var $button = $(this).prop('disabled', true);
        config.service.create({ name: name }).then(function (created) {
            addLookupManagerOption(created);
            $name.val('');
            abp.notify.success('Yeni ' + config.label + ' eklendi.');
            loadLookupManager();
        }, function (error) {
            abp.notify.error(error && error.message ? error.message : 'Kayıt eklenemedi.');
        }).then(function () { $button.prop('disabled', false); });
    });
    $('#ProductLookupManagerName').on('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            $('#ProductLookupManagerAdd').trigger('click');
        }
    });
    $('#ProductLookupManagerList').on('click', '.product-lookup-delete', function () {
        var config = lookupManagerConfig();
        var id = $(this).data('id');
        abp.message.confirm('Bu ' + config.label + ' kaydını silmek istediğinize emin misiniz?').then(function (confirmed) {
            if (!confirmed) return;
            config.service.delete(id).then(function () {
                $(config.selector + ', ' + config.filterSelector).find('option[value="' + id + '"]').remove();
                abp.notify.success(config.label + ' silindi.');
                loadLookupManager();
            }, function (error) {
                abp.notify.error(error && error.message ? error.message : 'Kayıt silinemedi. Ürüne bağlı kayıtlar silinemez.');
            });
        });
    });
    $('#CloseProductEditor').on('click', closeEditor);
    $('#Product_PurchasePrice, #Product_SalePrice, #Product_PurchaseCurrency, #Product_ProfitMarkupPercent').on('input change', function () {
        updatePriceEquivalents();
        updateCreateFinanceHero();
    });
    $('#ProductPreviewVatRate, [data-discount-matrix] input').on('input change', updateCreateFinanceHero);
    $('#CalculateSalePrice').on('click', calculateSalePrice);
    $('#GenerateProductSku').on('click', generateSku);
    $('.product-editor-tab').on('click', function () {
        activateCreateSection(String($(this).data('product-tab')));
    });
    $('#ProductCreateForm').on('keydown', function (event) {
        if (event.key === 'Escape') {
            event.preventDefault();
            closeEditor();
            return;
        }
        if (event.key === 'Enter' && !event.isComposing && !$(event.target).is('textarea, button')) {
            event.preventDefault();
            this.requestSubmit();
        }
    });
    $(document).on('keydown.productsCreate', function (event) {
        if (editor.prop('hidden')) {
            return;
        }
        if (event.key === 'Escape') {
            event.preventDefault();
            closeEditor();
        } else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            document.getElementById('ProductCreateForm').requestSubmit();
        }
    });

    function initCreatePricingModePicker() {
        var $scope = $('#ProductEditor');
        var $select = $scope.find('#Product_PricingMode');
        if (!$select.length || $scope.data('pricing-mode-ready')) {
            return;
        }
        $scope.data('pricing-mode-ready', true);
        function sync() {
            var modeStr = String($select.val() || '0');
            var dynamic = modeStr === '1' || modeStr.indexOf('Döviz') > -1 || modeStr === 'Dynamic' || modeStr.indexOf('Dynamic') > -1;
            var compareMode = dynamic ? '1' : '0';
            $scope.find('[data-pricing-mode-picker] .product-pricing-mode-option').each(function () {
                $(this).toggleClass('is-selected', String($(this).data('pricing-mode')) === compareMode);
            });
            $scope.find('[data-pricing-manual-field]').prop('hidden', dynamic);
            updateCreateFinanceHero();
        }
        $scope.find('[data-pricing-mode-picker] .product-pricing-mode-option').on('click', function () {
            var targetMode = String($(this).data('pricing-mode'));
            var targetText = targetMode === '1' ? 'Döviz' : 'Manuel';
            var exactVal = targetMode;
            $select.find('option').each(function() {
                if (this.value === targetMode || this.text.indexOf(targetText) > -1 || this.value.indexOf(targetText) > -1) {
                    exactVal = this.value;
                }
            });
            $select.val(exactVal).trigger('change');
        });
        $select.on('change', sync);
        sync();
    }
    initCreatePricingModePicker();

    $('#ProductSearch').on('input', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(reloadProducts, 300);
    });

    $('#ProductBrandFilter, #ProductCategoryFilter, #ProductStockStatusFilter, #ProductPricingModeFilter, #ProductCostStatusFilter').on('change', function () {
        $('.products-quick-filter').removeClass('is-active');
        reloadProducts();
    });

    $('#ResetProductFilters').on('click', resetFilters);
    $('.products-quick-filter').on('click', function () { applyQuickFilter($(this).data('quick-filter')); });

    $('[data-currency-view]').on('click', function () {
        applyCurrencyView($(this).data('currency-view') === 'reference', true);
    });

    editModal.onResult(function () { dataTable.ajax.reload(null, false); updateSummary(); });
    createModal.onResult(function () { dataTable.ajax.reload(null, false); updateSummary(); });

    $('#ProductsTable').on('click', '.product-edit-button', function () {
        closeEditor();
        editModal.open({ id: $(this).data('id') });
        refreshDialogHeroAfterOpen('.product-edit-dialog-form');
    });

    $('#ProductsTable').on('click', '.product-delete-button', function () {
        var productId = $(this).data('id');
        var productName = $(this).data('name') || 'bu ürünü';
        abp.message.confirm('"' + productName + '" ürününü silmek istediğinize emin misiniz?').then(function (confirmed) {
            if (!confirmed) {
                return;
            }

            noxStock.products.product.delete(productId).then(function () {
                abp.notify.info('Ürün silindi.');
                dataTable.ajax.reload(null, false);
                updateSummary();
            });
        });
    });

    // Kolon görünümü: ilk iki kimlik kolonu sabittir; diğerleri kullanıcı tercihidir.
    dataTable.column(0).visible(true, false);
    dataTable.column(1).visible(true, false);
    $('.col-toggle').each(function () {
        var columnIdx = Number($(this).data('column'));
        var stored = localStorage.getItem('productVisibleColumn' + columnIdx);
        var visible = stored !== null ? stored === '1' : $(this).prop('checked');
        $(this).prop('checked', visible);
        dataTable.column(columnIdx).visible(visible, false);
    });
    applyCurrencyView(showReferenceCurrency, false);
    dataTable.columns.adjust().draw(false);
    updateSummary();

    $('#ToggleColumnsBtn').on('click', function (e) {
        e.stopPropagation();
        $('#ColumnToggleMenu').toggleClass('hidden');
        $(this).attr('aria-expanded', $('#ColumnToggleMenu').hasClass('hidden') ? 'false' : 'true');
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('#ColumnToggleWrapper').length) {
            $('#ColumnToggleMenu').addClass('hidden');
            $('#ToggleColumnsBtn').attr('aria-expanded', 'false');
        }
    });

    $(document).on('keydown.productsColumns', function (e) {
        if (e.key !== 'Escape' || $('#ColumnToggleMenu').hasClass('hidden')) {
            return;
        }

        $('#ColumnToggleMenu').addClass('hidden');
        $('#ToggleColumnsBtn').attr('aria-expanded', 'false').trigger('focus');
    });

    $('.col-toggle').on('change', function () {
        var columnIdx = Number($(this).data('column'));
        var checked = $(this).is(':checked');
        if (dataTable) {
            dataTable.column(columnIdx).visible(checked);
        }
        localStorage.setItem('productVisibleColumn' + columnIdx, checked ? '1' : '0');
    });

    $('#ResetProductColumns').on('click', function () {
        $('.col-toggle').each(function () {
            var columnIdx = Number($(this).data('column'));
            var defaultVisible = defaultVisibleColumns.indexOf(columnIdx) >= 0;
            $(this).prop('checked', defaultVisible);
            localStorage.removeItem('productVisibleColumn' + columnIdx);
            dataTable.column(columnIdx).visible(defaultVisible, false);
        });
        for (var columnIndex = 0; columnIndex < dataTable.columns().count(); columnIndex++) {
            localStorage.removeItem(columnWidthKey(columnIndex));
            localStorage.removeItem('productColumnWidth' + columnIndex);
            $('#ProductsTable_wrapper th[data-column-index="' + columnIndex + '"]').css({ width: '', minWidth: '' });
            dataTable.column(columnIndex).nodes().to$().css({ width: '', minWidth: '' });
        }
        applyCurrencyView(false, true);
        dataTable.columns.adjust().draw(false);
    });

    $(document).on('mousedown.productsColumnResize', '#ProductsTable_wrapper .product-column-resize-handle', function (event) {
        event.preventDefault();
        event.stopPropagation();

        var $header = $(this).closest('th');
        var columnIndex = Number($header.attr('data-column-index'));
        var startX = event.pageX;
        var startWidth = $header.outerWidth();

        $('body').addClass('is-resizing-product-columns');
        $(document).on('mousemove.productsColumnResize', function (moveEvent) {
            applyColumnWidth(columnIndex, startWidth + (moveEvent.pageX - startX), true);
        });
        $(document).one('mouseup.productsColumnResize', function () {
            $(document).off('mousemove.productsColumnResize');
            $('body').removeClass('is-resizing-product-columns');
            dataTable.columns.adjust();
        });
    });

    $('#ImportFile').on('change', function () {
        var file = this.files && this.files.length ? this.files[0] : null;
        $('#ProductImportFileName').text(file ? file.name : 'En fazla 10 MB');
    });

    $('#ProductImportForm').on('submit', function () {
        var $button = $('#SubmitProductImport');
        $button.prop('disabled', true).html(
            '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>Aktarılıyor…'
        );
    });

    fetchRates();
});
