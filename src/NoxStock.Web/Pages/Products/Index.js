$(function () {
    var rates = {};
    var displayCurrency = localStorage.getItem('productDisplayCurrency') || 'TRY';
    var editor = $('#ProductEditor');
    var editModal = new abp.ModalManager(abp.appPath + 'Products/EditModal');
    var dataTable;
    var searchTimer;

    $('#DisplayCurrency').val(displayCurrency);

    function formatMoney(value, currency) {
        var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
        return new Intl.NumberFormat(locale, {
            style: 'currency',
            currency: currency || 'TRY',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(Number(value || 0));
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function toTry(value, currency) {
        var rate = currency === 'TRY' ? 1 : Number(rates[currency] && rates[currency].rate);
        return Number(value || 0) * (rate || 1);
    }

    function isDynamicPrice(row) {
        return Boolean(row.useCurrentExchangeRateForSale || row.pricingMode === 1);
    }

    function hasLastPurchaseCost(row) {
        return Number(row.lastPurchaseCostCurrencyAmount || 0) > 0;
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
        var isZero = quantity <= 0;
        var isCritical = !isZero && row.trackStock && quantity <= Number(row.criticalStockLevel || 0);
        var statusClass = isZero ? 'is-zero' : (isCritical ? 'is-critical' : 'is-normal');
        var statusText = isZero ? 'Sıfır stok' : (isCritical ? 'Kritik stok' : (row.trackStock ? 'Normal stok' : 'Takip dışı'));
        return '<div class="product-stock-value ' + statusClass + '">' + quantity.toFixed(2) + ' ' + escapeHtml(row.unit || '') + '</div>' +
            '<div class="product-stock-status ' + statusClass + '">' + statusText + '</div>';
    }

    function renderPricingBehavior(row) {
        var dynamic = isDynamicPrice(row);
        var price = dynamic ? 'Döviz bazlı dinamik' : 'Manuel fiyat';
        var detail = dynamic
            ? (hasLastPurchaseCost(row)
                ? (escapeHtml(row.pricingCurrencyCode || row.lastPurchaseCostCurrencyCode || 'TRY') + ' referansli · %' + Number(row.profitMarkupPercent || 0).toFixed(2) + ' markup')
                : 'Onaylı alış maliyeti bekleniyor')
            : ('Kart fiyatı · ' + escapeHtml(row.saleCurrency || 'TRY'));
        return '<div class="product-pricing-status ' + (dynamic ? 'is-dynamic' : 'is-manual') + '"><span class="material-icons-round" aria-hidden="true">' + (dynamic ? 'currency_exchange' : 'sell') + '</span><span>' + price + '</span></div>' +
            '<div class="product-table-subtitle">' + detail + '</div>';
    }

    function renderCostSource(row) {
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
            '<div class="product-cost-source">' + escapeHtml(netCost) + (rate > 0 ? ' · 1 ' + escapeHtml(row.pricingCurrencyCode || 'TRY') + ' = ' + rate.toFixed(4) + ' TRY' : '') + '</div>' +
            '<div class="product-table-subtitle">' + escapeHtml(date) + '</div>';
    }

    function renderReferenceCost(row) {
        if (!hasLastPurchaseCost(row)) {
            return '<div class="product-cost-empty"><span class="material-icons-round" aria-hidden="true">lock_clock</span><div><strong>Kilitli maliyet yok</strong><span>Alış onayında otomatik oluşur.</span></div></div>';
        }

        var currency = row.lastPurchaseCostCurrencyCode || row.pricingCurrencyCode || 'TRY';
        return '<div class="product-cost-value">' + escapeHtml(formatMoney(row.lastPurchaseCostCurrencyAmount, currency)) + '</div>' +
            '<div class="product-table-subtitle">' + escapeHtml(currency) + ' bazlı maliyet snapshotı</div>' +
            (row.lastPurchaseCostTryAmount ? '<div class="product-table-subtitle">TRY karşılığı: ' + escapeHtml(formatMoney(row.lastPurchaseCostTryAmount, 'TRY')) + '</div>' : '');
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
        return row.suggestedSaleUnitPrice != null
            ? '<div class="product-cost-value">' + escapeHtml(formatMoney(row.suggestedSaleUnitPrice, row.suggestedSaleCurrency || displayCurrency || 'TRY')) + '</div>' +
                '<div class="product-table-subtitle">Seçili para birimindeki satış önerisi</div>'
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

        var currency = row.suggestedSaleCurrency || displayCurrency || 'TRY';
        var suggested = formatMoney(row.suggestedSaleUnitPrice, currency);
        var rateDate = row.suggestedSaleRateDate
            ? luxon.DateTime.fromISO(row.suggestedSaleRateDate).toFormat('dd.MM.yyyy HH:mm')
            : '';
        var pricingRate = Number(row.currentPricingCurrencyExchangeRate || 0);
        var pricingRateText = dynamic && row.pricingCurrencyCode !== 'TRY' && pricingRate > 0
            ? 'Güncel kur: 1 ' + escapeHtml(row.pricingCurrencyCode) + ' = ' + pricingRate.toFixed(4) + ' TRY'
            : (rateDate ? 'Kur zamanı: ' + escapeHtml(rateDate) : 'TRY baz para birimi');
        return '<div class="product-sale-value">' + escapeHtml(suggested) + '</div>' +
            '<div class="product-sale-live"><span></span>' + (dynamic ? 'Güncel kurla dinamik' : 'Manuel fiyatın güncel görünümü') + '</div>' +
            '<div class="product-table-subtitle">' + pricingRateText + '</div>';
    }

    function updateSummary() {
        if (!dataTable) {
            return;
        }

        var rows = dataTable.rows({ page: 'current' }).data().toArray();
        var dynamic = rows.filter(isDynamicPrice).length;
        var missingCost = rows.filter(function (row) { return !hasLastPurchaseCost(row); }).length;
        var stockAttention = rows.filter(function (row) {
            var quantity = Number(row.quantity || 0);
            return quantity <= 0 || (row.trackStock && quantity <= Number(row.criticalStockLevel || 0));
        }).length;

        $('#ProductVisibleCount').text(rows.length);
        $('#ProductDynamicCount').text(dynamic);
        $('#ProductMissingCostCount').text(missingCost);
        $('#ProductAttentionStockCount').text(stockAttention);
    }

    function refreshTablePrices() {
        if (dataTable) {
            $('#DisplayCurrency').prop('disabled', true);
            $('#DisplayCurrencyStatus').text(displayCurrency + ' satış önerileri yükleniyor...');
            dataTable.ajax.reload(function () {
                $('#DisplayCurrency').prop('disabled', false);
                $('#DisplayCurrencyStatus').text(displayCurrency + ' satış önerisi gösteriliyor. Kilitli maliyet kaynak dövizinde korunur.');
            }, false);
        }
    }

    function updateEquivalent(priceInput, currencyInput, target) {
        var value = Number(priceInput.val());
        var currency = currencyInput.val();
        target.text(value && currency !== 'TRY' ? 'Yaklaşık ' + formatMoney(toTry(value, currency), 'TRY') : '');
    }

    function updatePriceEquivalents() {
        updateEquivalent($('#Product_PurchasePrice'), $('#Product_PurchaseCurrency'), $('#PurchaseTryEquivalent'));
        updateEquivalent($('#Product_SalePrice'), $('#Product_SaleCurrency'), $('#SaleTryEquivalent'));
    }

    function openEditor() {
        editor.prop('hidden', false);
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

    function fetchRates() {
        return fetch(abp.appPath + 'api/app/exchange-rate/current', { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (data) {
                    rates = { USD: data.usd || data.USD, EUR: data.eur || data.EUR };
                }
                updatePriceEquivalents();
                refreshTablePrices();
            })
            .catch(function () {
                updatePriceEquivalents();
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
            var saleCurrency = $modal.find('#Product_SaleCurrency').val() || 'TRY';
            var markupInput = $modal.find('#Product_ProfitMarkupPercent');
            var markup = Number(markupInput.val());
            if (!Number.isFinite(markup) || markup <= 0) {
                markup = 30;
                markupInput.val('30');
            }

            var mode = Number($modal.find('#Product_PricingMode').val());
            var currentReferenceRate = editRate(referenceCurrency);
            var currentSaleRate = editRate(saleCurrency);
            var hasCost = referenceCost > 0 || tryCost > 0;
            var $warning = $modal.find('#EditPricingWarning');

            $modal.find('#EditSourcePurchaseCost').text(hasCost && sourceCost > 0 ? editFormatMoney(sourceCost, sourceCurrency) : 'Maliyet bilgisi yok');
            $modal.find('#EditSourcePurchaseMeta').text(hasCost
                ? 'Net birim maliyet · TRY karşılığı: ' + editFormatMoney(tryCost || sourceCost, 'TRY')
                : 'Onaylı alış faturası bekleniyor.');
            $modal.find('#EditSourceDocument').text(dashboard.dataset.documentNumber || '-');
            $modal.find('#EditSourceSupplier').text(dashboard.dataset.supplierName || '-');
            $modal.find('#EditSourceRate').text(hasCost && purchaseReferenceRate > 0
                ? '1 ' + referenceCurrency + ' = ' + purchaseReferenceRate.toFixed(4) + ' TRY'
                : '-');
            $modal.find('#EditSourceDate').text(dashboard.dataset.purchaseDate || '-');
            $modal.find('#EditReferenceCost').text(hasCost && referenceCost > 0 ? editFormatMoney(referenceCost, referenceCurrency) : 'Maliyet bilgisi yok');
            $modal.find('#EditReferenceMeta').text(hasCost
                ? 'Referans: ' + referenceCurrency + ' · Güncel ' + referenceCurrency + '/TRY kuru: ' + (currentReferenceRate ? currentReferenceRate.toFixed(4) : 'bekleniyor')
                : 'TL maliyet, seçili referans dövizinde saklanır.');
            $modal.find('#EditPricingModeStatus').text(mode === 1 ? 'Dinamik fiyat aktif' : 'Manuel fiyat modu');
            $modal.find('#EditPricingModeStatus').toggleClass('is-dynamic', mode === 1).toggleClass('is-manual', mode !== 1);

            if (!hasCost) {
                $modal.find('#EditSuggestedSale').text('Hesaplanamaz');
                $modal.find('#EditSuggestedSaleMeta').text('Önce onaylı bir alış faturasıyla maliyet snapshotı oluşturun.');
                $warning.prop('hidden', true);
                return;
            }

            if (!currentReferenceRate || !currentSaleRate) {
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
            var suggestedSale = suggestedTry / currentSaleRate;
            $modal.find('#EditReferenceSale').text(editFormatMoney(referenceSale, referenceCurrency));
            $modal.find('#EditCurrentRate').text(saleCurrency === 'TRY'
                ? 'TRY baz para birimi'
                : '1 ' + saleCurrency + ' = ' + currentSaleRate.toFixed(4) + ' TRY');
            $modal.find('#EditSuggestedSale').text(editFormatMoney(suggestedSale, saleCurrency));
            $modal.find('#EditSuggestedSaleMeta').text('TRY eşdeğeri: ' + editFormatMoney(suggestedTry, 'TRY') + ' · Güncel ' + saleCurrency + '/TRY kuru: ' + currentSaleRate.toFixed(4) + ' · %' + markup.toFixed(2) + ' markup');
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

        $modal.find('#Product_SaleCurrency, #Product_PricingCurrencyCode, #Product_PricingMode, #Product_ProfitMarkupPercent').on('input change', function () {
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

    function getFilterPayload() {
        return {
            filter: $('#ProductSearch').val(),
            brandId: $('#ProductBrandFilter').val() || null,
            categoryId: $('#ProductCategoryFilter').val() || null,
            stockStatus: $('#ProductStockStatusFilter').val() || null,
            pricingMode: $('#ProductPricingModeFilter').val() || null,
            costStatus: $('#ProductCostStatusFilter').val() || null,
            displayCurrency: displayCurrency
        };
    }

    function reloadProducts() {
        dataTable.ajax.reload();
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
        } else if (filter === 'attention-stock') {
            $('#ProductStockStatusFilter').val('attention');
        }
        $('.products-quick-filter').removeClass('is-active');
        $('.products-quick-filter[data-quick-filter="' + filter + '"]').addClass('is-active');
        reloadProducts();
    }

    function columnWidthKey(columnIndex) {
        return 'productColumnWidth' + columnIndex;
    }

    function clampColumnWidth(width) {
        return Math.max(72, Math.min(420, Math.round(width)));
    }

    function applyColumnWidth(columnIndex, width, persist) {
        if (!dataTable) {
            return;
        }
        width = clampColumnWidth(width);
        $('#ProductsTable_wrapper th[data-column-index="' + columnIndex + '"]').css({ width: width + 'px', minWidth: width + 'px' });
        dataTable.column(columnIndex).nodes().to$().css({ width: width + 'px', minWidth: width + 'px' });
        if (persist) {
            localStorage.setItem(columnWidthKey(columnIndex), String(width));
        }
    }

    function initColumnResizeHandles() {
        var $headers = $('#ProductsTable_wrapper .dataTables_scrollHead th');
        $headers.each(function () {
            var $header = $(this);
            var columnIndex = this.cellIndex;
            $header.attr('data-column-index', columnIndex);
            if (!$header.children('.product-column-resize-handle').length) {
                $header.append('<span class="product-column-resize-handle" title="Kolon genişliğini ayarla" aria-hidden="true"></span>');
            }

            var savedWidth = Number(localStorage.getItem(columnWidthKey(columnIndex)) || 0);
            if (savedWidth > 0) {
                applyColumnWidth(columnIndex, savedWidth, false);
            }
        });
    }

    dataTable = $('#ProductsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, 'asc']],
        searching: false,
        scrollX: true,
        drawCallback: function () {
            updateSummary();
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
            { title: 'Maliyet kaynağı', data: 'lastPurchaseDocumentNumber', orderable: false, render: function (data, type, row) { return renderCostSource(row); } },
            { title: 'Kilitli referans maliyet', data: 'lastPurchaseCostCurrencyAmount', render: function (data, type, row) { return renderReferenceCost(row); } },
            { title: 'Güncel satış önerisi', data: 'suggestedSaleUnitPrice', orderable: false, render: function (data, type, row) { return renderSalePreview(row); } },
            { title: 'Marka / kategori', data: 'brandName', visible: false, orderable: false, render: function (data, type, row) { return renderBrandCategory(row); } },
            { title: 'Barkod', data: 'barcode', visible: false, render: function (data, type, row) { return escapeHtml(row.barcode || '-'); } },
            { title: 'OEM kodları', data: 'oemCodes', visible: false, orderable: false, render: function (data, type, row) { return escapeHtml(row.oemCodes || '-'); } },
            { title: 'Son alış tarihi', data: 'lastPurchaseDate', visible: false, render: function (data, type, row) { return renderOptionalDate(row); } },
            { title: 'Alış tutarı', data: 'lastPurchaseSourceNetUnitCost', visible: false, render: function (data, type, row) { return renderOptionalMoney(row.lastPurchaseSourceNetUnitCost, row.lastPurchaseSourceCurrencyCode); } },
            { title: 'Fatura kuru', data: 'lastPurchaseExchangeRate', visible: false, render: function (data, type, row) { return Number(data || 0) > 0 ? '1 ' + escapeHtml(row.pricingCurrencyCode || 'TRY') + ' = ' + Number(data).toFixed(4) + ' TRY' : '<span class="product-table-muted">-</span>'; } },
            { title: 'TRY maliyeti', data: 'lastPurchaseCostTryAmount', visible: false, render: function (data, type, row) { return renderOptionalMoney(data, 'TRY'); } },
            { title: 'Referans satış (seçili)', data: 'suggestedSaleUnitPrice', visible: false, render: function (data, type, row) { return renderOptionalReferenceSale(row); } },
            { title: 'Raf / konum', data: 'shelfLocation', visible: false, orderable: false, render: function (data, type, row) { return escapeHtml(row.shelfLocation || '-'); } }
        ]
    }));

    initColumnResizeHandles();

    $('#NewProductButton').on('click', openEditor);
    $('#CloseProductEditor').on('click', closeEditor);
    $('#Product_PurchasePrice, #Product_SalePrice, #Product_PurchaseCurrency, #Product_SaleCurrency, #Product_ProfitMarkupPercent').on('input change', updatePriceEquivalents);
    $('#CalculateSalePrice').on('click', calculateSalePrice);

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

    $('#DisplayCurrency').on('change', function () {
        displayCurrency = String(this.value || 'TRY').toUpperCase();
        localStorage.setItem('productDisplayCurrency', displayCurrency);
        refreshTablePrices();
    });

    editModal.onResult(function () { dataTable.ajax.reload(null, false); });

    $('#ProductsTable').on('click', '.product-edit-button', function () {
        closeEditor();
        editModal.open({ id: $(this).data('id') });
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
            });
        });
    });

    // Kolon gizleme dropdown ve toggle yönetimi
    $('.col-toggle').each(function () {
        var columnIdx = Number($(this).data('column'));
        var stored = localStorage.getItem('productVisibleColumn' + columnIdx);
        if (stored !== null) {
            var visible = stored === '1';
            $(this).prop('checked', visible);
            dataTable.column(columnIdx).visible(visible, false);
        }
    });
    dataTable.columns.adjust().draw(false);

    $('#ToggleColumnsBtn').on('click', function (e) {
        e.stopPropagation();
        $('#ColumnToggleMenu').toggleClass('hidden');
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('#ColumnToggleWrapper').length) {
            $('#ColumnToggleMenu').addClass('hidden');
        }
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
            var defaultVisible = columnIdx >= 2 && columnIdx <= 6;
            $(this).prop('checked', defaultVisible);
            localStorage.removeItem('productVisibleColumn' + columnIdx);
            dataTable.column(columnIdx).visible(defaultVisible, false);
        });
        for (var columnIndex = 0; columnIndex < dataTable.columns().count(); columnIndex++) {
            localStorage.removeItem(columnWidthKey(columnIndex));
            $('#ProductsTable_wrapper th[data-column-index="' + columnIndex + '"]').css({ width: '', minWidth: '' });
            dataTable.column(columnIndex).nodes().to$().css({ width: '', minWidth: '' });
        }
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

    fetchRates();
});
