$(function () {
    var showReferenceCurrency = localStorage.getItem('productShowReferenceCurrency') === 'true';
    var editModal = new abp.ModalManager(abp.appPath + 'Products/EditModal');
    var createModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Products/CreateModal',
        modalClass: 'ProductCreateModal'
    });
    var dataTable;
    var searchTimer;
    var referenceCurrencyColumns = [];
    var defaultVisibleColumns = [2, 3, 4, 5, 6];

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
        return number.toFixed(digits == null ? 2 : digits).replace(/(\.\d*?[1-9])0+$/, '$1').replace(/\.0+$/, '').replace(/\.$/, '');
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

    function refreshDialogHeroAfterOpen(selector) {
        [80, 260, 600].forEach(function (delay) {
            window.setTimeout(function () {
                $(selector).each(function () {
                    var $form = $(this);
                    window.noxStock.productCardModal.init($form);
                    window.noxStock.productCardModal.refresh($form);
                });
            }, delay);
        });
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function isDynamicPrice(row) {
        return Number(row.pricingMode) === 1;
    }

    function hasVerifiedCost(row) {
        return Number(row.averageInventoryCostTry || 0) > 0 ||
            Number(row.lastPurchaseCostCurrencyAmount || 0) > 0 ||
            Number(row.lastPurchaseCostTryAmount || 0) > 0;
    }

    function hasCostInfo(row) {
        return hasVerifiedCost(row) || Number(row.currentNetCostInTry || 0) > 0;
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
        var stockValue = tracksStock ? formatDecimal(quantity, 2) + ' ' + escapeHtml(row.unit || '') : 'Takip edilmiyor';
        return '<div class="product-stock-value ' + statusClass + '">' + stockValue + '</div>' +
            '<div class="product-stock-status ' + statusClass + '">' + statusText + '</div>';
    }

    function renderPricingBehavior(row) {
        var dynamic = isDynamicPrice(row);
        var price = dynamic ? 'Otomatik fiyat' : 'Manuel fiyat';
        var detail = dynamic
            ? '%'+ formatDecimal(row.profitMarkupPercent || 0, 2) + ' maliyet üzerine kâr'
            : 'Aktif TL fiyatı kullanıcı tarafından korunur';
        return '<div class="product-pricing-status ' + (dynamic ? 'is-dynamic' : 'is-manual') + '"><span class="material-icons-round" aria-hidden="true">' + (dynamic ? 'currency_exchange' : 'sell') + '</span><span>' + price + '</span></div>' +
            '<div class="product-table-subtitle">' + detail + '</div>';
    }

    function renderCostSource(row) {
        if (row.isIncompleteProfile) {
            return '<div class="product-cost-empty"><span class="material-icons-round" aria-hidden="true">edit_note</span><div><strong>Eksik bilgili ürün</strong><span>Sayım sırasında hızlı oluşturuldu; ürün kartını tamamlayın.</span></div></div>';
        }
        if (!hasCostInfo(row)) {
            return '<div class="product-cost-empty"><span class="material-icons-round" aria-hidden="true">receipt_long</span><div><strong>Maliyet bilgisi yok</strong><span>Referans maliyet veya alış faturası ekleyin.</span></div></div>';
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
            '<div class="product-cost-source">' + escapeHtml(netCost) + (rate > 0 ? ' · 1 ' + escapeHtml(row.purchaseCurrency || 'TRY') + ' = ' + formatDecimal(rate, 2) + ' TRY' : '') + '</div>' +
            '<div class="product-table-subtitle">' + escapeHtml(date) + '</div>';
    }

    function renderReferenceCost(row) {
        if (!hasCostInfo(row)) {
            return '<div class="product-table-empty"><strong>—</strong><span>Maliyet bilgisi yok</span></div>';
        }

        var isVerified = hasVerifiedCost(row);
        var currency = row.purchaseCurrency || row.lastPurchaseCostCurrencyCode || 'TRY';
        var currentTryCost = row.averageInventoryCostTry != null
            ? row.averageInventoryCostTry
            : row.currentNetCostInTry != null ? row.currentNetCostInTry : row.lastPurchaseCostTryAmount;
        var sourceCurrency = row.lastPurchaseSourceCurrencyCode || currency;
        var sourceAmount = Number(row.lastPurchaseSourceNetUnitCost || row.lastPurchaseCostCurrencyAmount || 0);
        var sourceRate = Number(row.lastPurchaseSourceExchangeRate || row.lastPurchaseExchangeRate || 0);
        var lastPurchaseTitle = sourceAmount > 0
            ? 'Son alış: ' + formatMoney(sourceAmount, sourceCurrency) + (sourceRate > 0 ? ' · Kur ' + formatDecimal(sourceRate, 2) : '') + (row.lastPurchaseCostTryAmount ? ' · ' + formatMoney(row.lastPurchaseCostTryAmount, 'TRY') : '')
            : 'Referans maliyet: ' + formatMoney(currentTryCost, 'TRY') + (row.purchaseCurrency && row.purchaseCurrency !== 'TRY' ? ' · Kur ' + formatDecimal(row.exchangeRateAtPurchase || 0, 2) : '');
        return '<div class="product-cost-value">' + escapeHtml(formatMoney(currentTryCost, 'TRY')) + '</div>' +
            '<div class="product-table-subtitle" title="' + escapeHtml(lastPurchaseTitle) + '">' + (isVerified ? 'Onaylı alış maliyeti · detay için üzerine gelin' : 'Referans maliyet · alış faturasıyla doğrulanır') + '</div>';
    }

    function renderProfit(row) {
        var profit = Number(row.estimatedUnitProfitInTry || 0);
        var rate = row.actualMarkupPercent;
        if (row.currentNetCostInTry == null && row.averageInventoryCostTry == null) {
            return '<div class="product-table-empty"><strong>—</strong><span>Maliyet bilgisi yok</span></div>';
        }
        return '<div class="product-profit-value' + (profit < 0 ? ' is-loss' : '') + '">' + escapeHtml(formatMoney(profit, 'TRY')) + '</div>' +
            '<div class="product-table-subtitle">%' + formatDecimal(rate || 0, 2) + ' birim kâr oranı</div>';
    }

    function renderProductStatus(row) {
        var dynamic = isDynamicPrice(row);
        var states = [];
        states.push('<span class="product-status-pill ' + (dynamic ? 'is-auto' : 'is-manual') + '">' + (dynamic ? 'Otomatik fiyat' : 'Manuel fiyat') + '</span>');
        if (row.isIncompleteProfile) states.push('<span class="product-status-pill is-warning">Bilgileri eksik</span>');
        if (!hasCostInfo(row)) states.push('<span class="product-status-pill is-muted">Maliyet bekliyor</span>');
        else if (!hasVerifiedCost(row)) states.push('<span class="product-status-pill is-warning">Referans maliyet</span>');
        if (row.isBelowTargetMarkup) states.push('<span class="product-status-pill is-warning">Hedef kârın altında</span>');
        return '<div class="product-status-list">' + states.join('') + '</div>';
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
        if (!row.isSuggestedSaleAvailable && dynamic && !hasCostInfo(row)) {
            return '<div class="product-sale-unavailable"><span class="material-icons-round" aria-hidden="true">schedule</span><div><strong>Hesaplanmayı bekliyor</strong><span>Onaylı alış maliyeti yok.</span></div></div>';
        }

        if (!row.isSuggestedSaleAvailable) {
            return '<div class="product-sale-unavailable"><span class="material-icons-round" aria-hidden="true">cloud_off</span><div><strong>Kur bekleniyor</strong><span>Güncel kur geldiğinde fiyat otomatik hesaplanır.</span></div></div>';
        }

        if (!dynamic && Number(row.suggestedSaleUnitPrice || 0) <= 0) {
            return '<div class="product-sale-unavailable is-action-needed"><span class="material-icons-round" aria-hidden="true">sell</span><div><strong>Satış fiyatı girilmedi</strong><span>Ürün kartından manuel TL fiyatı belirleyin.</span></div></div>';
        }

        var suggested = formatMoney(row.suggestedSaleUnitPrice, 'TRY');
        var rateDate = row.suggestedSaleRateDate
            ? luxon.DateTime.fromISO(row.suggestedSaleRateDate).toFormat('dd.MM.yyyy HH:mm')
            : '';
        var pricingRate = Number(row.currentReferenceCurrencyExchangeRate || 0);
        var pricingRateText = dynamic
            ? 'Stok maliyeti × hedef kâr oranı'
            : 'Kullanıcının aktif TL satış fiyatı';
        return '<div class="product-sale-value">' + escapeHtml(suggested) + '</div>' +
            '<div class="product-sale-live"><span></span>' + (dynamic ? 'Otomatik aktif fiyat' : 'Manuel aktif fiyat') + '</div>' +
            '<div class="product-table-subtitle">' + pricingRateText + '</div>';
    }

    function updateSummary() {
        noxStock.products.product.getInventorySummary().then(function (summary) {
            $('#ProductVisibleCount').text(Number(summary.totalCount || 0));
            $('#ProductDynamicCount').text(Number(summary.dynamicPricingCount || 0));
            $('#ProductMissingCostCount').text(Number(summary.missingCostCount || 0));
            $('#ProductAttentionStockCount').text(Number(summary.attentionStockCount || 0));
            $('#ProductTotalInventoryValue').text(formatMoney(summary.totalInventoryCost || 0, 'TRY'));
            $('#ProductTotalPotentialRevenue').text(formatMoney(summary.totalPotentialRevenue || 0, 'TRY'));
        }).catch(function () {
            $('#ProductVisibleCount, #ProductDynamicCount, #ProductMissingCostCount, #ProductAttentionStockCount, #ProductTotalInventoryValue, #ProductTotalPotentialRevenue').text('—');
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

    function closeEditor() {
        // The former page-inline editor was removed. Product cards now open in
        // the shared ABP modal, so there is no inline editor state to close.
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
        window.noxStock.productCardModal.init($form);
        $form.on('keydown', function (event) {
            if (event.key === 'Escape') { event.preventDefault(); $modal.modal('hide'); }
            else if (event.key === 'Enter' && !event.isComposing && !$(event.target).is('textarea, button, select')) {
                event.preventDefault();
                if (this.requestSubmit) this.requestSubmit();
                else $modal.find('.modal-footer button[type="submit"]').trigger('click');
            }
        });
        window.noxStock.productCardModal.refresh($form);
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
        // Modal rate loading belongs to ProductCardModal.js. The list itself is
        // priced server-side; a reload is sufficient when this legacy hook runs.
        refreshTablePrices();
        return Promise.resolve();
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
                maximumFractionDigits: 2
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
                ? '1 ' + referenceCurrency + ' = ' + formatDecimal(purchaseReferenceRate, 2) + ' TRY'
                : '-');
            $modal.find('#EditSourceDate').text(dashboard.dataset.purchaseDate || '-');
            $modal.find('#EditReferenceCost').text(hasCost && referenceCost > 0 ? editFormatMoney(referenceCost, referenceCurrency) : 'Maliyet bilgisi yok');
            $modal.find('#EditReferenceMeta').text(hasCost
                ? 'Referans: ' + referenceCurrency + ' · Güncel ' + referenceCurrency + '/TRY kuru: ' + (currentReferenceRate ? formatDecimal(currentReferenceRate, 2) : 'bekleniyor')
                : 'TL maliyet, seçili referans dövizinde saklanır.');
            $modal.find('#EditPricingModeStatus').text(mode === 1 ? 'Dinamik fiyat aktif' : 'Manuel fiyat modu');
            $modal.find('#EditPricingModeStatus').toggleClass('is-dynamic', mode === 1).toggleClass('is-manual', mode !== 1);

            if (!hasCost) {
                $modal.find('#EditSuggestedSale').text('Hesaplanamaz');
                $modal.find('#EditSuggestedSaleMeta').text('Önce onaylı bir alış faturasıyla ürün maliyetini oluşturun.');
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
            $modal.find('#EditSuggestedSaleMeta').text('Güncel ' + referenceCurrency + '/TRY kuru ve %' + markup.toFixed(2) + ' hedef kâr oranıyla hesaplandı.');
            $warning.text(mode === 1
                ? 'Otomatik satış fiyatı aktif. POS ve satış faturası güncel maliyet, kur ve hedef kâr oranını kullanır.'
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

    var productColumnWidths = [72, 270, 125, 165, 165, 155, 170];

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
        stateSave: false,
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
            { title: 'Ürün', data: 'name', render: function (data, type, row) { return renderProductIdentity(row); } },
            { title: 'Stok', data: 'quantity', render: function (data, type, row) { return renderStock(row); } },
            { title: 'Satış fiyatı', data: 'suggestedSaleUnitPrice', orderable: false, render: function (data, type, row) { return renderSalePreview(row); } },
            { title: 'Maliyet', data: 'currentNetCostInTry', render: function (data, type, row) { return renderReferenceCost(row); } },
            { title: 'Kâr', data: 'estimatedUnitProfitInTry', orderable: false, render: function (data, type, row) { return renderProfit(row); } },
            { title: 'Durum', orderable: false, render: function (data, type, row) { return renderProductStatus(row); } }
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
