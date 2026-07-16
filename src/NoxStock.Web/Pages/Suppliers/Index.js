$(function () {
    var l = abp.localization.getResource('NoxStock');
    var businessPartnerService = noxStock.businessPartners.businessPartner;
    var createModal = new abp.ModalManager(abp.appPath + 'Suppliers/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'Suppliers/EditModal');
    var paymentModal = new abp.ModalManager(abp.appPath + 'Shared/PaymentModal');

    paymentModal.onResult(function () {
        dataTable.ajax.reload();
    });

    var dataTable = $('#SuppliersTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, "asc"]],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(businessPartnerService.getList, function () {
            return { type: 2 }; // Supplier = 2
        }),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: {
                    items:
                        [
                            {
                                text: 'Ödeme Yap',
                                action: function (data) {
                                    paymentModal.open({ businessPartnerId: data.record.id, isCollection: false });
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
                                    return "Tedarikçiyi silmek istediğinize emin misiniz: " + data.record.name;
                                },
                                action: function (data) {
                                    businessPartnerService.delete(data.record.id)
                                        .then(function () {
                                            abp.notify.info("Başarıyla silindi.");
                                            dataTable.ajax.reload();
                                        });
                                }
                            }
                        ]
                }
            },
            {
                title: 'Adı Soyadı / Ünvan',
                data: "name"
            },
            {
                title: 'Telefon',
                data: "phone"
            },
            {
                title: 'Bakiye',
                data: "balance",
                render: function (data, type, row) {
                    var color = data < 0 ? 'text-red-600' : (data > 0 ? 'text-green-600' : 'text-slate-600');
                    return `<span class="font-semibold ${color}">${data} ${row.currency}</span>`;
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

    $('#NewSupplierButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });
});
