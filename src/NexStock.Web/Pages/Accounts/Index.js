$(function () {
    var accountService = nexStock.accounts.account;
    var createModal = new abp.ModalManager(abp.appPath + 'Accounts/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'Accounts/EditModal');
    var transactionsModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Accounts/TransactionsModal',
        scriptUrl: '/Pages/Accounts/TransactionsModal.js',
        modalClass: 'TransactionsModal'
    });

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
            {
                title: 'Hesap Adı',
                data: 'name'
            },
            {
                title: 'Tür',
                data: 'type',
                render: function (data) {
                    return accountTypeText(data);
                }
            },
            {
                title: 'Para Birimi',
                data: 'currency'
            },
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

    createModal.onResult(function () {
        dataTable.ajax.reload();
    });

    editModal.onResult(function () {
        dataTable.ajax.reload();
    });

    $('#NewAccountButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });

    var transferModal = new abp.ModalManager(abp.appPath + 'Accounts/TransferModal');
    var addTransactionModal = new abp.ModalManager(abp.appPath + 'Accounts/AddTransactionModal');

    transferModal.onResult(function () {
        dataTable.ajax.reload();
    });

    addTransactionModal.onResult(function () {
        dataTable.ajax.reload();
    });

    $('#TransferButton').click(function (e) {
        e.preventDefault();
        transferModal.open();
    });

    $('#AddTransactionButton').click(function (e) {
        e.preventDefault();
        addTransactionModal.open();
    });
});
