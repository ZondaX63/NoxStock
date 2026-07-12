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
        return new Intl.NumberFormat(locale, { style: 'currency', currency: currency, minimumFractionDigits: 2 }).format(Number(value || 0));
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

    function renderPrice(value, sourceCurrency) {
        if (sourceCurrency !== displayCurrency &&
            ((!rates[sourceCurrency] && sourceCurrency !== 'TRY') || (!rates[displayCurrency] && displayCurrency !== 'TRY'))) {
            return formatMoney(value, sourceCurrency);
        }

        return formatMoney(convert(value, sourceCurrency, displayCurrency), displayCurrency);
    }

    function renderProductSalePrice(row) {
        if (!row.useCurrentExchangeRateForSale) {
            return renderPrice(row.salePrice, row.saleCurrency || 'TRY');
        }

        var purchaseCurrency = row.purchaseCurrency || 'TRY';
        var saleCurrency = row.saleCurrency || 'TRY';
        var purchaseRate = purchaseCurrency === 'TRY' ? 1 : Number(rates[purchaseCurrency] && rates[purchaseCurrency].rate);
        var saleRate = saleCurrency === 'TRY' ? 1 : Number(rates[saleCurrency] && rates[saleCurrency].rate);
        if (!purchaseRate || !saleRate) {
            return formatMoney(row.salePrice, saleCurrency);
        }

        var currentPrice = Number(row.purchasePrice || 0) * purchaseRate * (1 + Number(row.profitMarkupPercent || 0) / 100) / saleRate;
        return renderPrice(currentPrice, saleCurrency);
    }

    function refreshTablePrices() {
        if (dataTable) {
            dataTable.ajax.reload(null, false);
        }
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

    function openEditor() {
        editor.prop('hidden', false);
        editor[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
        $('#Product_Name').trigger('focus');
    }

    function closeEditor() {
        editor.prop('hidden', true);
    }

    function calculateSalePrice() {
        var purchasePrice = Number($('#Product_PurchasePrice').val());
        var margin = Number($('#Product_ProfitMarkupPercent').val());
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
                $('#Product_UseCurrentExchangeRateForSale').prop('checked', true);
                $('#Product_SalePrice').val(refreshedFinalSalePrice.toFixed(2)).trigger('input');
                $('#SaleCalculationInfo').text(formatMoney(purchasePrice, purchaseCurrency) + ' + %' + margin + ' marj = ' + formatMoney(refreshedFinalSalePrice, saleCurrency) + ' (' + formatMoney(refreshedSalePriceInTry, 'TRY') + ')');
                refreshTablePrices();
            });
        }

        var salePriceInTry = purchasePrice * purchaseRate * (1 + margin / 100);
        var finalSalePrice = salePriceInTry / saleRate;
        $('#Product_UseCurrentExchangeRateForSale').prop('checked', true);
        $('#Product_SalePrice').val(finalSalePrice.toFixed(2)).trigger('input');
        $('#SaleCalculationInfo').text(formatMoney(purchasePrice, purchaseCurrency) + ' + %' + margin + ' marj = ' + formatMoney(finalSalePrice, saleCurrency) + ' (' + formatMoney(salePriceInTry, 'TRY') + ')');
    }

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
                refreshTablePrices();
            })
            .catch(function () {
                updatePriceEquivalents();
                refreshTablePrices();
            });
    }

    function getFilterPayload() {
        return {
            filter: $('#ProductSearch').val(),
            brandId: $('#ProductBrandFilter').val() || null,
            categoryId: $('#ProductCategoryFilter').val() || null,
            stockStatus: $('#ProductStockStatusFilter').val() || null
        };
    }

    dataTable = $('#ProductsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, 'asc']],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(nexStock.products.product.getList, getFilterPayload),
        columnDefs: [
            {
                title: 'İşlem',
                orderable: false,
                render: function (data, type, row) {
                    return '' +
                        '<div class="flex items-center gap-2">' +
                        '<button type="button" class="product-edit-button text-indigo-600 hover:text-indigo-900" data-id="' + row.id + '" title="Düzenle"><span class="material-icons-round text-lg">edit</span></button>' +
                        '<button type="button" class="product-delete-button text-red-600 hover:text-red-900" data-id="' + row.id + '" data-name="' + (row.name || '') + '" title="Sil"><span class="material-icons-round text-lg">delete</span></button>' +
                        '</div>';
                }
            },
            {
                title: 'Ürün',
                data: 'name',
                render: function (data, type, row) {
                    return '<div class="font-medium text-slate-800 dark:text-slate-100">' + (data || '') + '</div>' +
                        '<div class="mt-1 max-w-xs truncate text-xs text-slate-500">' + (row.description || '') + '</div>';
                }
            },
            {
                title: 'Kodlar',
                data: 'sku',
                render: function (data, type, row) {
                    var extra = (row.barcode || '-') + (row.oemCodes ? ' · ' + row.oemCodes : '');
                    return '<div>' + (data || '') + '</div><div class="mt-1 text-xs text-slate-500">' + extra + '</div>';
                }
            },
            {
                title: 'Marka / kategori',
                render: function (data, type, row) {
                    return (row.brandName || '-') + '<div class="mt-1 text-xs text-slate-500">' + (row.categoryName || '-') + '</div>';
                }
            },
            {
                title: 'Stok',
                data: 'quantity',
                render: function (data, type, row) {
                    var stockClass = Number(row.quantity) <= Number(row.criticalStockLevel) ? 'text-red-600' : 'text-slate-700';
                    return '<span class="font-semibold ' + stockClass + '">' + Number(data || 0).toFixed(2) + ' ' + (row.unit || '') + '</span>';
                }
            },
            {
                title: 'Alış',
                data: 'purchasePrice',
                render: function (data, type, row) {
                    return '<span class="product-purchase-price font-semibold" data-price="' + Number(data || 0).toFixed(2) + '" data-currency="' + (row.purchaseCurrency || 'TRY') + '">' +
                        renderPrice(data, row.purchaseCurrency || 'TRY') +
                        '</span>';
                }
            },
            {
                title: 'Satış',
                data: 'salePrice',
                render: function (data, type, row) {
                    return '<span class="product-sale-price font-semibold" data-price="' + Number(data || 0).toFixed(2) + '" data-currency="' + (row.saleCurrency || 'TRY') + '">' +
                        renderProductSalePrice(row) +
                        '</span>';
                }
            }
        ]
    }));

    $('#NewProductButton').on('click', openEditor);
    $('#CloseProductEditor').on('click', closeEditor);
    $('#Product_PurchasePrice, #Product_SalePrice, #Product_PurchaseCurrency, #Product_SaleCurrency, #Product_ProfitMarkupPercent').on('input change', updatePriceEquivalents);
    $('#CalculateSalePrice').on('click', function () {
        calculateSalePrice();
    });

    $('#ProductSearch').on('input', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(function () {
            dataTable.ajax.reload();
        }, 300);
    });

    $('#ProductBrandFilter, #ProductCategoryFilter, #ProductStockStatusFilter').on('change', function () {
        dataTable.ajax.reload();
    });

    $('#ResetProductFilters').on('click', function () {
        $('#ProductSearch').val('');
        $('#ProductBrandFilter').val('');
        $('#ProductCategoryFilter').val('');
        $('#ProductStockStatusFilter').val('');
        dataTable.ajax.reload();
    });

    $('#DisplayCurrency').on('change', function () {
        displayCurrency = this.value;
        localStorage.setItem('productDisplayCurrency', displayCurrency);
        refreshTablePrices();
    });

    editModal.onResult(function () {
        dataTable.ajax.reload(null, false);
    });

    $('#ProductsTable').on('click', '.product-edit-button', function () {
        closeEditor();
        editModal.open({ id: $(this).data('id') });
    });

    $('#ProductsTable').on('click', '.product-delete-button', function () {
        var productId = $(this).data('id');
        var productName = $(this).data('name') || 'bu ürünü';

        abp.message.confirm('"' + productName + '" ürününü silmek istediğinize emin misiniz?')
            .then(function (confirmed) {
                if (!confirmed) {
                    return;
                }

                nexStock.products.product.delete(productId).then(function () {
                    abp.notify.info('Ürün silindi.');
                    dataTable.ajax.reload(null, false);
                });
            });
    });

    fetchRates();
});
