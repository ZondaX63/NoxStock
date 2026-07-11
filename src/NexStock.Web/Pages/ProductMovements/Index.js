$(function () {
    var stockMovementService = nexStock.stockMovements.stockMovement;

    var dataTable = $('#StockMovementsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[0, "desc"]],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(stockMovementService.getList),
        columnDefs: [
            {
                title: 'Tarih',
                data: "movementDate",
                render: function (data) {
                    if (data) {
                        return luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy HH:mm');
                    }
                    return "";
                }
            },
            {
                title: 'Ürün',
                data: "productName"
            },
            {
                title: 'İşlem Yönü',
                data: "type",
                render: function(data) {
                    if(data === 1) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Satış Faturası</span>';
                    if(data === 2) return '<span class="bg-purple-100 text-purple-800 text-xs font-medium px-2.5 py-0.5 rounded">Alış Faturası</span>';
                    if(data === 3) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Ters Kayıt</span>';
                    if(data === 4) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Manuel Giriş</span>';
                    if(data === 5) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">Manuel Çıkış</span>';
                    return data;
                }
            },
            {
                title: 'Miktar',
                data: "quantity",
                render: function(data) {
                    var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-emerald-600' : 'text-slate-600');
                    var sign = data > 0 ? '+' : '';
                    return '<span class="font-bold ' + color + '">' + sign + data + '</span>';
                }
            },
            {
                title: 'Kalan Bakiye',
                data: "balanceAfter",
                render: function(data) {
                    return '<span class="font-semibold">' + data + '</span>';
                }
            },
            {
                title: 'Açıklama',
                data: "description"
            }
        ]
    }));
});
