$(function () {
    var stockMovementService = noxStock.stockMovements.stockMovement;
    var table;

    function movementTypeText(type) {
        if (type === 1) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Manuel giriş</span>';
        if (type === 2) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">Manuel çıkış</span>';
        if (type === 3) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Düzeltme</span>';
        if (type === 4) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Alış faturası</span>';
        if (type === 5) return '<span class="bg-orange-100 text-orange-800 text-xs font-medium px-2.5 py-0.5 rounded">Satış faturası</span>';
        if (type === 6) return '<span class="bg-indigo-100 text-indigo-800 text-xs font-medium px-2.5 py-0.5 rounded">POS satış</span>';
        if (type === 7) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">İade</span>';
        if (type === 8) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Ters kayıt</span>';
        if (type === 9) return '<span class="bg-violet-100 text-violet-800 text-xs font-medium px-2.5 py-0.5 rounded">Stok sayımı</span>';
        return type;
    }

    function formatMoney(value, currency) {
        return new Intl.NumberFormat(document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR', {
            style: 'currency', currency: currency || 'TRY', minimumFractionDigits: 2, maximumFractionDigits: 2
        }).format(Number(value || 0));
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function getFilterPayload() {
        return {
            productId: $('#ProductMovementFilter').val() || null
        };
    }

    table = $('#StockMovementsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        stateSave: true,
        stateDuration: -1,
        searchDelay: 300,
        paging: true,
        order: [[0, 'desc']],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(stockMovementService.getList, getFilterPayload),
        columnDefs: [
            {
                title: 'Tarih',
                data: 'movementDate',
                render: function (data) {
                    return data ? luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy HH:mm') : '';
                }
            },
            {
                title: 'Ürün',
                data: 'productName'
            },
            {
                title: 'İşlem Yönü',
                data: 'type',
                render: function (data) {
                    return movementTypeText(data);
                }
            },
            {
                title: 'Kaynak',
                data: 'documentId',
                render: function (data, type, row) {
                    if (data) {
                        var purchase = Number(row.type) === 4 && row.purchaseUnitCost != null
                            ? '<div class="mt-1 text-xs text-slate-500">Alış: ' + escapeHtml(formatMoney(row.purchaseUnitCost, row.purchaseCurrency || 'TRY')) +
                                (Number(row.purchaseExchangeRate || 0) > 0 && row.purchaseCurrency !== 'TRY' ? ' · Kur ' + Number(row.purchaseExchangeRate).toFixed(2) : '') +
                                (row.purchaseUnitCostTry != null ? ' · ' + escapeHtml(formatMoney(row.purchaseUnitCostTry, 'TRY')) + '/birim' : '') +
                                (row.purchaseTotalCostTry != null ? ' · Toplam ' + escapeHtml(formatMoney(row.purchaseTotalCostTry, 'TRY')) : '') + '</div>'
                            : '';
                        return '<div class="font-medium text-slate-800 dark:text-white">' + escapeHtml(row.documentNumber || 'Belge') + '</div>' + purchase;
                    }

                    return '<span class="text-slate-500">Manuel</span><div class="mt-0.5 text-xs text-slate-400">' + escapeHtml(row.description || '-') + '</div>';
                }
            },
            {
                title: 'Miktar',
                data: 'quantity',
                render: function (data) {
                    var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-emerald-600' : 'text-slate-600');
                    var sign = data > 0 ? '+' : '';
                    return '<span class="font-bold ' + color + '">' + sign + data + '</span>';
                }
            },
            {
                title: 'Kalan Bakiye',
                data: 'balanceAfter',
                render: function (data) {
                    return '<span class="font-semibold">' + data + '</span>';
                }
            },
            {
                title: 'Açıklama',
                data: 'description'
            }
        ]
    }));

    $('#ProductMovementFilter').on('change', function () {
        table.ajax.reload();
    });

    $('#ResetMovementFilter').on('click', function () {
        $('#ProductMovementFilter').val('');
        table.ajax.reload();
    });
});
