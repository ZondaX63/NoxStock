$(function () {
    var documentService = noxStock.documents.document;
    var workflowService = noxStock.documents.commercialDocumentWorkflow;
    var recordOf = function (data) { return data && (data.record || data); };

    function statusText(status) {
        if (status === 1) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2 py-0.5 rounded">Taslak</span>';
        if (status === 2) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2 py-0.5 rounded">Gönderildi</span>';
        if (status === 7) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2 py-0.5 rounded">Dönüştürüldü</span>';
        if (status === 5) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2 py-0.5 rounded">İptal</span>';
        return status;
    }

    var table = $('#OffersTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[3, 'desc']],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(documentService.getList, function () { return { scope: 2 }; }),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: { items: [
                    {
                        text: 'Gönder',
                        visible: function (data) { var record = recordOf(data); return record && record.status === 1; },
                        action: function (data) { workflowService.sendOffer(data.record.id).then(function () { abp.notify.success('Teklif gönderildi.'); table.ajax.reload(); }); }
                    },
                    {
                        text: 'Siparişe Dönüştür',
                        visible: function (data) { var record = recordOf(data); return record && record.status === 2; },
                        action: function (data) { workflowService.convertOfferToOrder(data.record.id).then(function () { abp.notify.success('Teklif satış siparişine dönüştürüldü.'); table.ajax.reload(); }); }
                    },
                    {
                        text: 'Faturaya Dönüştür',
                        visible: function (data) { var record = recordOf(data); return record && record.status === 2; },
                        action: function (data) { workflowService.convertOfferToInvoice(data.record.id).then(function () { abp.notify.success('Teklif satış faturasına dönüştürüldü.'); table.ajax.reload(); }); }
                    },
                    {
                        text: 'Sil',
                        visible: function (data) { var record = recordOf(data); return record && record.status === 1; },
                        confirmMessage: function (data) { return data.record.docNumber + ' numaralı taslak teklifi silmek istiyor musunuz?'; },
                        action: function (data) { documentService.delete(data.record.id).then(function () { table.ajax.reload(); }); }
                    }
                ] }
            },
            { title: 'Teklif No', data: 'docNumber' },
            { title: 'Cari Hesap', data: 'businessPartnerName' },
            { title: 'Tarih', data: 'date', render: function (data) { return data ? luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy') : ''; } },
            { title: 'Geçerlilik', data: 'dueDate', render: function (data) { return data ? luxon.DateTime.fromISO(data).toFormat('dd.MM.yyyy') : '-'; } },
            { title: 'Toplam', data: 'grandTotal', render: function (data, type, row) { return '<span class="font-semibold">' + data + ' ' + row.currency + '</span>'; } },
            { title: 'Durum', data: 'status', render: statusText }
        ]
    }));
});
