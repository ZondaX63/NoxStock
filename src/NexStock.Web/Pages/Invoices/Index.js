$(function () {
    var documentService = nexStock.documents.document;
    var documentWorkflowService = nexStock.documents.documentWorkflow;

    var dataTable = $('#InvoicesTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[4, "desc"]],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(documentService.getList),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: {
                    items:
                        [
                            {
                                text: 'Onayla',
                                visible: function (data) {
                                    return data.record.status === 1 || data.record.status === 2;
                                },
                                action: function (data) {
                                    documentWorkflowService
                                        .approve(data.record.id, { accountId: null, paymentAmount: 0 })
                                        .then(function () {
                                            abp.notify.success('Fatura onaylandı.');
                                            dataTable.ajax.reload();
                                        });
                                }
                            },
                            {
                                text: 'İptal Et',
                                visible: function (data) {
                                    return data.record.status === 3 || data.record.status === 6 || data.record.status === 7;
                                },
                                confirmMessage: function (data) {
                                    return data.record.docNumber + ' numaralı faturayı iptal etmek istediğinize emin misiniz?';
                                },
                                action: function (data) {
                                    documentWorkflowService
                                        .cancel(data.record.id)
                                        .then(function () {
                                            abp.notify.info('Fatura iptal edildi.');
                                            dataTable.ajax.reload();
                                        });
                                }
                            },
                            {
                                text: 'Sil',
                                visible: function (data) {
                                    return data.record.status === 1;
                                },
                                confirmMessage: function (data) {
                                    return data.record.docNumber + ' numaralı taslak faturayı silmek istediğinize emin misiniz?';
                                },
                                action: function (data) {
                                    documentService.delete(data.record.id).then(function () {
                                        abp.notify.info('Taslak fatura silindi.');
                                        dataTable.ajax.reload();
                                    });
                                }
                            }
                        ]
                }
            },
            {
                title: 'Fatura No',
                data: "docNumber"
            },
            {
                title: 'Cari Hesap',
                data: "businessPartnerName"
            },
            {
                title: 'Tip',
                data: "type",
                render: function(data) {
                    if(data === 1) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Satış Faturası</span>';
                    if(data === 2) return '<span class="bg-purple-100 text-purple-800 text-xs font-medium px-2.5 py-0.5 rounded">Alış Faturası</span>';
                    return data;
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
                title: 'Genel Toplam',
                data: "grandTotal",
                render: function(data, type, row) {
                    return `<span class="font-semibold text-slate-800">${data} ${row.currency}</span>`;
                }
            },
            {
                title: 'Durum',
                data: "status",
                render: function(data) {
                    if(data === 1) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Taslak</span>';
                    if(data === 3) return '<span class="bg-green-100 text-green-800 text-xs font-medium px-2.5 py-0.5 rounded">Onaylandı</span>';
                    if(data === 5) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">İptal</span>';
                    if(data === 6) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Ödendi</span>';
                    if(data === 7) return '<span class="bg-indigo-100 text-indigo-800 text-xs font-medium px-2.5 py-0.5 rounded">Tamamlandı</span>';
                    return data;
                }
            }
        ]
    }));

});
