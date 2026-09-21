$(function () {
    var service = noxStock.businessPartners.businessPartner;
    var customerCreate = new abp.ModalManager(abp.appPath + 'Customers/CreateModal');
    var supplierCreate = new abp.ModalManager(abp.appPath + 'Suppliers/CreateModal');
    var customerEdit = new abp.ModalManager(abp.appPath + 'Customers/EditModal');
    var supplierEdit = new abp.ModalManager(abp.appPath + 'Suppliers/EditModal');
    var payment = new abp.ModalManager({ viewUrl: abp.appPath + 'Shared/PaymentModal', modalClass: 'BusinessPartnerPaymentModal' });
    var opening = new abp.ModalManager({ viewUrl: abp.appPath + 'Shared/BusinessPartnerOpeningBalanceModal', modalClass: 'BusinessPartnerOpeningBalanceModal' });
    var isGranted = abp.auth.isGranted;

    function selectedType() {
        var value = $('#PartnerTypeFilter').val();
        return value ? Number(value) : null;
    }

    function money(value, currency) {
        return Math.abs(value || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + currency;
    }

    function isCustomer(partner) {
        return partner.type === 1 || partner.type === 3;
    }

    function isSupplier(partner) {
        return partner.type === 2 || partner.type === 3;
    }

    var table = $('#BusinessPartnersTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        stateSave: true,
        searchDelay: 300,
        scrollX: true,
        order: [[1, 'asc']],
        ajax: abp.libs.datatables.createAjax(service.getList, function () {
            return { type: selectedType(), filter: $('#PartnerSearch').val() };
        }),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: {
                    items: [
                        {
                            text: 'Satış geçmişi',
                            visible: function (data) { return isCustomer(data.record); },
                            action: function (data) { window.location.href = abp.appPath + 'Customers/' + data.record.id + '/SalesHistory'; }
                        },
                        {
                            text: 'Alış geçmişi',
                            visible: function (data) { return isSupplier(data.record); },
                            action: function (data) { window.location.href = abp.appPath + 'Suppliers/' + data.record.id + '/PurchaseHistory'; }
                        },
                        {
                            text: 'Tahsilat al',
                            visible: function (data) { return isCustomer(data.record) && isGranted('NoxStock.BusinessPartners.Collection'); },
                            action: function (data) { payment.open({ businessPartnerId: data.record.id, isCollection: true }); }
                        },
                        {
                            text: 'Ödeme yap',
                            visible: function (data) { return isSupplier(data.record) && isGranted('NoxStock.BusinessPartners.Payment'); },
                            action: function (data) { payment.open({ businessPartnerId: data.record.id, isCollection: false }); }
                        },
                        {
                            text: 'Açılış / devir bakiyesi',
                            visible: isGranted('NoxStock.BusinessPartners.OpeningBalance'),
                            action: function (data) { opening.open({ businessPartnerId: data.record.id }); }
                        },
                        {
                            text: 'Düzenle',
                            visible: isGranted('NoxStock.BusinessPartners.Edit'),
                            action: function (data) { (isCustomer(data.record) ? customerEdit : supplierEdit).open({ id: data.record.id }); }
                        }
                    ]
                }
            },
            { data: 'name', title: 'Cari' },
            { data: 'type', title: 'Tür', render: function (value) { return value === 1 ? 'Müşteri' : value === 2 ? 'Tedarikçi' : 'Müşteri & tedarikçi'; } },
            { data: 'phone', title: 'Telefon', defaultContent: '—' },
            { data: 'openDocumentCount', title: 'Açık belge' },
            { data: 'overdueAmount', title: 'Vadesi geçmiş', render: function (value, _, row) { return value > 0 ? '<span class="font-semibold text-rose-600">' + money(value, row.currency) + '</span>' : '—'; } },
            {
                data: 'balance', title: 'Bakiye', render: function (value, _, row) {
                    if (value === 0) return 'Bakiye yok';
                    var amount = money(value, row.currency);
                    return '<span class="font-semibold ' + (value > 0 ? 'text-emerald-600' : 'text-rose-600') + '">' + amount + (value > 0 ? ' alacak' : ' borç') + '</span>';
                }
            },
            {
                data: null, title: 'Son hareket', render: function (_, __, row) {
                    var dates = [row.lastSaleDate, row.lastPurchaseDate, row.lastCollectionDate, row.lastPaymentDate].filter(Boolean);
                    return dates.length ? new Date(Math.max.apply(null, dates.map(Date.parse))).toLocaleDateString('tr-TR') : '—';
                }
            }
        ]
    }));

    [customerCreate, supplierCreate, customerEdit, supplierEdit, payment, opening].forEach(function (modal) {
        modal.onResult(function () { table.ajax.reload(); });
    });

    if (!isGranted('NoxStock.BusinessPartners.Create')) {
        $('#NewCustomerButton, #NewSupplierButton').hide();
    }

    $('#PartnerTypeFilter').on('change', function () { table.ajax.reload(); });
    $('#PartnerSearch').on('input', function () { table.ajax.reload(); });
    $('#NewCustomerButton').on('click', function () { customerCreate.open(); });
    $('#NewSupplierButton').on('click', function () { supplierCreate.open(); });
});
