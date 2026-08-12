$(function () {
    var accountService = noxStock.accounts.account;
    var createModal = new abp.ModalManager(abp.appPath + 'Accounts/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'Accounts/EditModal');
    var openingModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Accounts/OpeningBalanceModal',
        scriptUrl: abp.appPath + 'Pages/Accounts/OpeningBalanceModal.js',
        modalClass: 'AccountOpeningBalanceModal'
    });
    var cancelTransactionModal = new abp.ModalManager(abp.appPath + 'Accounts/CancelTransactionModal');

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
                                        2: ['bg-emerald-100 text-emerald-800', 'Para girişi'],
                                        3: ['bg-red-100 text-red-800', 'Para çıkışı'],
                                        4: ['bg-blue-100 text-blue-800', 'Hesaplar arası transfer (giriş)'],
                                        5: ['bg-orange-100 text-orange-800', 'Hesaplar arası transfer (çıkış)'],
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
                                title: 'Açıklama', data: 'description'
                            },
                            {
                                title: 'Giriş', data: null, className: 'text-end',
                                render: function (data, type, row) {
                                    var isEntry = [1, 2, 4, 6].indexOf(row.type) >= 0 || (row.type === 8 && row.amount > 0);
                                    return isEntry ? '<span class="font-bold text-emerald-600">' + Math.abs(row.amount).toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ' + row.currency + '</span>' : '—';
                                }
                            },
                            {
                                title: 'Çıkış', data: null, className: 'text-end',
                                render: function (data, type, row) {
                                    var isExit = [3, 5, 7].indexOf(row.type) >= 0 || (row.type === 8 && row.amount < 0);
                                    return isExit ? '<span class="font-bold text-red-600">' + Math.abs(row.amount).toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ' + row.currency + '</span>' : '—';
                                }
                            },
                            {
                                title: 'İşlem Sonrası',
                                data: 'balanceAfter',
                                render: function (data, type, row) {
                                    return data === null || data === undefined ? '—' : Number(data).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + row.currency;
                                }
                            },
                            {
                                title: 'Durum',
                                data: 'status',
                                render: function (data) {
                                    return data === 2
                                        ? '<span class="badge bg-secondary">İptal edildi</span>'
                                        : '<span class="badge bg-success">Geçerli</span>';
                                }
                            },
                            {
                                title: 'Aksiyon',
                                rowAction: {
                                    items: [{
                                        text: 'İptal et',
                                        visible: function (data) {
                                            var row = data.record;
                                            if (row.status !== 1 || row.type === 8 || row.documentId) return false;
                                            if (row.type === 1) return abp.auth.isGranted('NoxStock.Accounts.CancelOpeningBalance');
                                            if (row.type === 4 || row.type === 5) return abp.auth.isGranted('NoxStock.Accounts.CancelTransfer');
                                            return abp.auth.isGranted('NoxStock.Accounts.CancelTransaction');
                                        },
                                        action: function (data) {
                                            cancelTransactionModal.open({ transactionId: data.record.id });
                                        }
                                    }]
                                }
                            }
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
        stateSave: true,
        stateDuration: -1,
        searchDelay: 300,
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
                            text: 'Açılış bakiyesi gir',
                            visible: function () { return abp.auth.isGranted('NoxStock.Accounts.OpeningBalance'); },
                            action: function (data) {
                                openingModal.open({ accountId: data.record.id });
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
                title: 'Bugünkü Giriş', data: 'todayIncome', className: 'text-end',
                render: function (data, type, row) { return Number(data || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + row.currency; }
            },
            {
                title: 'Bugünkü Çıkış', data: 'todayExpense', className: 'text-end',
                render: function (data, type, row) { return Number(data || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + row.currency; }
            },
            {
                title: 'Son Hareket', data: 'lastTransactionDate',
                render: function (data) { return data ? new Date(data).toLocaleString('tr-TR') : 'Henüz hareket yok'; }
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
    openingModal.onResult(function () { dataTable.ajax.reload(null, false); });
    cancelTransactionModal.onResult(function () {
        dataTable.ajax.reload(null, false);
        if ($.fn.DataTable.isDataTable('#AccountTransactionsTable')) {
            $('#AccountTransactionsTable').DataTable().ajax.reload(null, false);
        }
    });

    $('#NewAccountButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });

    var transferModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Accounts/TransferModal',
        scriptUrl: abp.appPath + 'Pages/Accounts/TransferModal.js',
        modalClass: 'AccountTransferModal'
    });
    var addTransactionModal = new abp.ModalManager(abp.appPath + 'Accounts/AddTransactionModal');

    transferModal.onResult(function () { dataTable.ajax.reload(); });
    addTransactionModal.onResult(function () { dataTable.ajax.reload(); });

    $('#TransferButton').click(function (e) {
        e.preventDefault();
        transferModal.open();
    });

    if (!abp.auth.isGranted('NoxStock.Accounts.Transfer')) $('#TransferButton').remove();
    if (!abp.auth.isGranted('NoxStock.Accounts.AddTransaction')) $('#AddTransactionButton').remove();

    $('#AddTransactionButton').click(function (e) {
        e.preventDefault();
        addTransactionModal.open();
    });
});
