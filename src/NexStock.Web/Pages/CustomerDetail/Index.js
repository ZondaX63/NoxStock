$(function () {
    var businessPartnerId = $('#BusinessPartnerId').val();
    if (!businessPartnerId || businessPartnerId === '00000000-0000-0000-0000-000000000000') {
        return;
    }

    var bpService = nexStock.businessPartners.businessPartner;
    var documentService = nexStock.documents.document;
    
    var createPaymentModal = new abp.ModalManager(abp.appPath + 'Payments/CreateModal');

    createPaymentModal.onResult(function () {
        // reload the datatable and bp details
        $('#BpDocumentsTable').DataTable().ajax.reload();
        loadBpDetails();
    });

    $('#NewCollectionBtn').click(function(e) {
        e.preventDefault();
        createPaymentModal.open({ type: 'Collection' });
    });

    $('#NewPaymentBtn').click(function(e) {
        e.preventDefault();
        createPaymentModal.open({ type: 'Payment' });
    });

    function loadBpDetails() {
        bpService.get(businessPartnerId).then(function (bp) {
            $('#BpName').text(bp.name);
            
            var typeText = bp.type === 1 ? 'Müşteri' : 'Tedarikçi';
            $('#BpType').text(typeText);
            
            $('#BpPhone').text(bp.phoneNumber || '--');
            $('#BpEmail').text(bp.email || '--');
            $('#BpTaxNumber').text(bp.taxNumber || '--');

            var balanceColor = bp.balance < 0 ? 'text-red-600' : (bp.balance > 0 ? 'text-emerald-600' : 'text-slate-600');
            var balanceSign = bp.balance > 0 ? '+' : '';
            $('#BpBalance').text(balanceSign + bp.balance.toFixed(2) + ' ' + bp.currency)
                           .removeClass('text-indigo-700 dark:text-indigo-300 text-red-600 text-emerald-600 text-slate-600')
                           .addClass(balanceColor);
        });
    }

    // Load initially
    loadBpDetails();

    // Load Documents
    var dataTable = $('#BpDocumentsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, "desc"]], // Date desc
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(documentService.getList, function() {
            return { businessPartnerId: businessPartnerId };
        }),
        columnDefs: [
            {
                title: 'Belge No',
                data: "docNumber",
                render: function(data, type, row) {
                    return '<span class="font-semibold">' + data + '</span>';
                }
            },
            {
                title: 'Tarih',
                data: "date",
                render: function (data) {
                    if (data) {
                        return luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy');
                    }
                    return "";
                }
            },
            {
                title: 'Tip',
                data: "type",
                render: function(data) {
                    if(data === 1) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Satış Faturası</span>';
                    if(data === 2) return '<span class="bg-purple-100 text-purple-800 text-xs font-medium px-2.5 py-0.5 rounded">Alış Faturası</span>';
                    if(data === 6) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Tahsilat Makbuzu</span>';
                    if(data === 7) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">Tediye Makbuzu</span>';
                    return data;
                }
            },
            {
                title: 'Tutar',
                data: "grandTotal",
                className: 'text-right',
                render: function(data, type, row) {
                    return '<span class="font-bold text-slate-800">' + data + ' ' + row.currency + '</span>';
                }
            },
            {
                title: 'Ödenen',
                data: "paidAmount",
                className: 'text-right',
                render: function(data, type, row) {
                    var color = data > 0 ? 'text-emerald-600' : 'text-slate-500';
                    return '<span class="font-semibold ' + color + '">' + data + ' ' + row.currency + '</span>';
                }
            },
            {
                title: 'Durum',
                data: "status",
                className: 'text-center',
                render: function(data) {
                    if(data === 1) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2 py-0.5 rounded">Taslak</span>';
                    if(data === 3) return '<span class="bg-green-100 text-green-800 text-xs font-medium px-2 py-0.5 rounded">Onaylandı</span>';
                    if(data === 5) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2 py-0.5 rounded">İptal</span>';
                    if(data === 6) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2 py-0.5 rounded">Ödendi</span>';
                    if(data === 7) return '<span class="bg-indigo-100 text-indigo-800 text-xs font-medium px-2 py-0.5 rounded">Tamamlandı</span>';
                    if(data === 8) return '<span class="bg-yellow-100 text-yellow-800 text-xs font-medium px-2 py-0.5 rounded">Kısmen Ödendi</span>';
                    return data;
                }
            }
        ]
    }));
});
