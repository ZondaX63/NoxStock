$(function () {
    var accountService = noxStock.accounts.account;
    var createModal = new abp.ModalManager(abp.appPath + 'Accounts/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'Accounts/EditModal');

    // ── Hesap Hareketleri Modal ──────────────────────────────────────────────
    // The TransactionsModal class is defined HERE (not in a separate scriptUrl file)
    // so there is no async script-loading race condition on first open.
    abp.modals.TransactionsModal = function () {
        return {
            initModal: function (publicApi, args) {
                var $modal = publicApi.getModal();
                var accountId = (args && args.accountId)
                    ? args.accountId
                    : $modal.find('#TransactionsAccountId').val();

                function buildDataTable() {
                    var tableEl = $modal.find('#AccountTransactionsTable');
                    if ($.fn.DataTable.isDataTable(tableEl)) {
                        tableEl.DataTable().destroy();
                    }
                    tableEl.DataTable(abp.libs.datatables.normalizeConfiguration({
                        serverSide: true,
                        paging: true,
                        order: [[0, 'desc']],
                        searching: false,
                        scrollX: true,
                        ajax: abp.libs.datatables.createAjax(function (input) {
                            return noxStock.accounts.account.getTransactions(accountId, input);
                        }),
                        columnDefs: [
                            {
                                title: 'Tarih',
                                data: 'transactionDate',
                                render: function (data) {
                                    if (data) {
                                        var d = new Date(data);
                                        return d.toLocaleDateString('tr-TR') + ' ' +
                                            d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
                                    }
                                    return '';
                                }
                            },
                            {
                                title: 'İşlem Tipi',
                                data: 'type',
                                render: function (data) {
                                    var map = {
                                        1: ['bg-blue-100 text-blue-800', 'Açılış Bakiyesi'],
                                        2: ['bg-emerald-100 text-emerald-800', 'Gelir'],
                                        3: ['bg-red-100 text-red-800', 'Gider'],
                                        4: ['bg-blue-100 text-blue-800', 'Transfer Giriş'],
                                        5: ['bg-orange-100 text-orange-800', 'Transfer Çıkış'],
                                        6: ['bg-emerald-100 text-emerald-800', 'Tahsilat'],
                                        7: ['bg-red-100 text-red-800', 'Ödeme'],
                                        8: ['bg-slate-100 text-slate-800', 'Ters Kayıt']
                                    };
                                    var entry = map[data];
                                    if (entry) {
                                        return '<span class="' + entry[0] + ' text-xs font-medium px-2.5 py-0.5 rounded">' + entry[1] + '</span>';
                                    }
                                    return data;
                                }
                            },
                            {
                                title: 'Tutar',
                                data: 'amount',
                                render: function (data) {
                                    var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-emerald-600' : 'text-slate-600');
                                    return '<span class="font-bold ' + color + '">' + (data > 0 ? '+' : '') + data + '</span>';
                                }
                            },
                            { title: 'Para Birimi', data: 'currency' },
                            { title: 'Açıklama', data: 'description' }
                        ]
                    }));
                }

                // Since this class is defined synchronously with the page,
                // initModal runs BEFORE the Bootstrap modal animation starts.
                // We wait for shown.bs.modal so DataTable calculates widths correctly.
                $modal.one('shown.bs.modal', buildDataTable);
            }
        };
    };

    var transactionsModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Accounts/TransactionsModal',
        modalClass: 'TransactionsModal'
    });

    // ── Hesaplar DataTable ───────────────────────────────────────────────────
    function accountTypeText(type) {
        if (type === 1) return 'Kasa';
        if (type === 2) return 'Banka';
        if (type === 3) return 'Kredi Kartı';
        if (type === 4) return 'Personel';
        return 'Diğer';
    }

    var dataTable = $('#AccountsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, 'asc']],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(accountService.getList),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: {
                    items: [
                        {
                            text: 'Hareketler',
                            action: function (data) {
                                transactionsModal.open({ accountId: data.record.id });
                            }
                        },
                        {
                            text: 'Düzenle',
                            action: function (data) {
                                editModal.open({ id: data.record.id });
                            }
                        },
                        {
                            text: 'Sil',
                            confirmMessage: function (data) {
                                return data.record.name + ' hesabını silmek istediğinize emin misiniz?';
                            },
                            action: function (data) {
                                accountService.delete(data.record.id).then(function () {
                                    abp.notify.info('Hesap silindi.');
                                    dataTable.ajax.reload();
                                });
                            }
                        }
                    ]
                }
            },
            { title: 'Hesap Adı', data: 'name' },
            {
                title: 'Tür',
                data: 'type',
                render: function (data) { return accountTypeText(data); }
            },
            { title: 'Para Birimi', data: 'currency' },
            {
                title: 'Bakiye',
                data: 'balance',
                className: 'text-end',
                render: function (data, type, row) {
                    var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-emerald-600' : 'text-slate-600');
                    return '<span class="font-semibold ' + color + '">' + data + ' ' + row.currency + '</span>';
                }
            },
            {
                title: 'Durum',
                data: 'isActive',
                render: function (data) {
                    return data
                        ? '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Aktif</span>'
                        : '<span class="bg-slate-100 text-slate-700 text-xs font-medium px-2.5 py-0.5 rounded">Pasif</span>';
                }
            }
        ]
    }));

    createModal.onResult(function () { dataTable.ajax.reload(); });
    editModal.onResult(function () { dataTable.ajax.reload(); });

    $('#NewAccountButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });

    var transferModal = new abp.ModalManager(abp.appPath + 'Accounts/TransferModal');
    var addTransactionModal = new abp.ModalManager(abp.appPath + 'Accounts/AddTransactionModal');

    transferModal.onResult(function () { dataTable.ajax.reload(); });
    addTransactionModal.onResult(function () { dataTable.ajax.reload(); });

    $('#TransferButton').click(function (e) {
        e.preventDefault();
        transferModal.open();
    });

    $('#AddTransactionButton').click(function (e) {
        e.preventDefault();
        addTransactionModal.open();
    });
});
