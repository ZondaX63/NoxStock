var abp = abp || {};
$(function () {
    abp.modals.TransactionsModal = function () {
        var initModal = function (publicApi, args) {
            var $modal = publicApi.getModal();
            var accountId = $modal.find('#TransactionsAccountId').val();
            var accountTransactionService = nexStock.accounts.accountTransaction;

            var dataTable = $modal.find('#AccountTransactionsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
                serverSide: true,
                paging: true,
                order: [[0, "desc"]],
                searching: false,
                scrollX: true,
                ajax: abp.libs.datatables.createAjax(accountTransactionService.getList, function () {
                    return { accountId: accountId };
                }),
                columnDefs: [
                    {
                        title: 'Tarih',
                        data: "transactionDate",
                        render: function (data) {
                            if (data) {
                                return luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy HH:mm');
                            }
                            return "";
                        }
                    },
                    {
                        title: 'İşlem Tipi',
                        data: "type",
                        render: function(data) {
                            if(data === 1) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Açılış Bakiyesi</span>';
                            if(data === 2) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Tahsilat</span>';
                            if(data === 3) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">Ödeme</span>';
                            if(data === 4) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Ters Kayıt</span>';
                            return data;
                        }
                    },
                    {
                        title: 'Tutar',
                        data: "amount",
                        render: function(data) {
                            var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-emerald-600' : 'text-slate-600');
                            var sign = data > 0 ? '+' : '';
                            return '<span class="font-bold ' + color + '">' + sign + data + '</span>';
                        }
                    },
                    {
                        title: 'Para Birimi',
                        data: "currency"
                    },
                    {
                        title: 'Açıklama',
                        data: "description"
                    }
                ]
            }));
        };

        return {
            initModal: initModal
        };
    };
});
