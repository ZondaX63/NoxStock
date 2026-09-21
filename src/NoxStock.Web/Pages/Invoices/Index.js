$(function () {
    function normalizeSearchText(value) {
        return String(value || '').trim().toLocaleLowerCase('tr-TR')
            .replace(/ı/g, 'i').replace(/ğ/g, 'g').replace(/ü/g, 'u')
            .replace(/ş/g, 's').replace(/ö/g, 'o').replace(/ç/g, 'c');
    }

    var documentService = noxStock.documents.document;
    var documentWorkflowService = noxStock.documents.documentWorkflow;

    abp.modals.RecordPaymentModal = function () {
        return {
            initModal: function (publicApi) {
                var $modal = publicApi.getModal();
                var $form = $modal.find('form[data-account-lookup-url]');
                var $search = $form.find('[data-account-search]');
                var $select = $form.find('#Payment_AccountId');
                var $status = $form.find('[data-account-search-status]');
                var lookupUrl = $form.data('account-lookup-url');
                var currency = String($form.data('document-currency') || 'TRY');
                var timer = null;
                var sequence = 0;
                var cache = new Map();

                function accountLabel(account) {
                    return account.name + ' - ' + Number(account.balance || 0).toLocaleString('tr-TR') + ' ' + account.currency;
                }

                function renderAccounts(accounts) {
                    var selectedValue = $select.val();
                    var selectedText = $select.find('option:selected').text();
                    $select.empty().append($('<option>', { value: '', text: 'Kasa/Banka Seçiniz...' }));

                    if (selectedValue) {
                        $select.append($('<option>', { value: selectedValue, text: selectedText, selected: true }));
                    }

                    accounts.forEach(function (account) {
                        if (String(account.id) === String(selectedValue)) return;
                        $select.append($('<option>', { value: account.id, text: accountLabel(account) }));
                    });
                }

                function loadAccounts() {
                    var filter = String($search.val() || '').trim();
                    var cacheKey = currency + '|' + normalizeSearchText(filter);
                    if (cache.has(cacheKey)) {
                        renderAccounts(cache.get(cacheKey));
                        $status.text(cache.get(cacheKey).length ? '' : 'Eşleşen aktif hesap bulunamadı.');
                        return;
                    }

                    var requestSequence = ++sequence;
                    $status.text('Hesaplar yükleniyor...');
                    fetch(lookupUrl + '&currency=' + encodeURIComponent(currency) + '&filter=' + encodeURIComponent(filter), {
                        credentials: 'same-origin',
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    })
                        .then(function (response) {
                            if (!response.ok) throw new Error('Account lookup failed');
                            return response.json();
                        })
                        .then(function (result) {
                            if (requestSequence !== sequence) return;
                            var accounts = result.items || [];
                            if (cache.size >= 30) cache.delete(cache.keys().next().value);
                            cache.set(cacheKey, accounts);
                            renderAccounts(accounts);
                            $status.text(accounts.length ? '' : 'Eşleşen aktif hesap bulunamadı.');
                        })
                        .catch(function () {
                            if (requestSequence === sequence) {
                                $status.text('Hesaplar yüklenemedi. Lütfen tekrar deneyin.');
                            }
                        });
                }

                $search.on('input', function () {
                    window.clearTimeout(timer);
                    timer = window.setTimeout(loadAccounts, 250);
                });
                loadAccounts();
            }
        };
    };

    var recordPaymentModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Invoices/RecordPaymentModal',
        modalClass: 'RecordPaymentModal'
    });

    function recordOf(data) {
        return data && (data.record || data);
    }

    recordPaymentModal.onResult(function () {
        dataTable.ajax.reload();
    });

    var dataTable = $('#InvoicesTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        stateSave: true,
        stateDuration: -1,
        searchDelay: 300,
        paging: true,
        order: [[4, "desc"]],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(documentService.getList, function () {
            return { dueFilter: Number($('#DueFilter').val()), scope: 1 };
        }),
        columnDefs: [
            {
                title: 'İşlemler',
                rowAction: {
                    items:
                        [
                            {
                                text: 'Yazdır',
                                action: function (data) {
                                    window.open(abp.appPath + 'Invoices/Print?id=' + encodeURIComponent(recordOf(data).id), '_blank', 'noopener');
                                }
                            },
                            {
                                text: 'Düzenle',
                                visible: function (data) {
                                    var record = recordOf(data);
                                    return record && record.status === 1;
                                },
                                action: function (data) {
                                    window.location.href = abp.appPath + 'Invoices/Create/' + encodeURIComponent(recordOf(data).id);
                                }
                            },
                            {
                                text: 'Onayla',
                                visible: function (data) {
                                    var record = recordOf(data);
                                    return record && (record.status === 1 || record.status === 2);
                                },
                                action: function (data) {
                                    var record = recordOf(data);
                                    var operationKey = window.crypto && window.crypto.randomUUID
                                        ? window.crypto.randomUUID()
                                        : Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
                                    var approve = function (confirmBelowCost) {
                                        return documentWorkflowService
                                            .approve(record.id, {
                                                operationKey: operationKey,
                                                accountId: null,
                                                paymentAmount: 0,
                                                confirmBelowCost: confirmBelowCost === true
                                            })
                                            .then(function () {
                                                abp.notify.success('Fatura onaylandı.');
                                                dataTable.ajax.reload();
                                            }, function (error) {
                                                var code = error && error.error && error.error.code;
                                                var canOverride = error && error.error && error.error.data &&
                                                    String(error.error.data.CanOverride || error.error.data.canOverride).toLowerCase() === 'true';
                                                if (code !== 'NoxStock:SaleBelowInventoryCost' || confirmBelowCost || !canOverride) {
                                                    return $.Deferred().reject(error).promise();
                                                }
                                                return abp.message.confirm(
                                                    'Bu faturada stok maliyetinin altında kalan satır var. Yetkinizi kullanarak onaylamak istiyor musunuz?',
                                                    'Zararına satış uyarısı'
                                                ).then(function (confirmed) { if (confirmed) return approve(true); });
                                            });
                                    };
                                    approve(false);
                                }
                            },
                            {
                                text: function (data) {
                                    return recordOf(data).type === 1 ? 'Tahsilat Kaydet' : 'Ödeme Kaydet';
                                },
                                visible: function (data) {
                                    var record = recordOf(data);
                                    return record && (record.type === 1 || record.type === 2) &&
                                        (record.status === 3 || record.status === 8);
                                },
                                action: function (data) {
                                    recordPaymentModal.open({ documentId: recordOf(data).id });
                                }
                            },
                            {
                                text: 'İptal Et',
                                visible: function (data) {
                                    var record = recordOf(data);
                                    return record && (record.status === 3 || record.status === 6 || record.status === 7 || record.status === 8);
                                },
                                confirmMessage: function (data) {
                                    return recordOf(data).docNumber + ' numaralı faturayı iptal etmek istediğinize emin misiniz?';
                                },
                                action: function (data) {
                                    var record = recordOf(data);
                                    documentWorkflowService
                                        .cancel(record.id)
                                        .then(function () {
                                            abp.notify.info('Fatura iptal edildi.');
                                            dataTable.ajax.reload();
                                        });
                                }
                            },
                            {
                                text: 'Sil',
                                visible: function (data) {
                                    var record = recordOf(data);
                                    return record && record.status === 1;
                                },
                                confirmMessage: function (data) {
                                    return recordOf(data).docNumber + ' numaralı taslak faturayı silmek istediğinize emin misiniz?';
                                },
                                action: function (data) {
                                    documentService.delete(recordOf(data).id).then(function () {
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
                title: 'Vade',
                data: 'dueDate',
                render: function (data, type, row) {
                    if (!data) {
                        return '<span class="text-slate-400">-</span>';
                    }

                    var dueDate = luxon.DateTime.fromISO(data);
                    var isOpen = row.status === 3 || row.status === 8;
                    var isOverdue = isOpen && dueDate.startOf('day') < luxon.DateTime.now().startOf('day');
                    var style = isOverdue ? 'font-semibold text-red-600' : 'text-slate-600';
                    return '<span class="' + style + '">' + dueDate.toFormat('dd.MM.yyyy') + '</span>';
                }
            },
            {
                title: 'Genel Toplam',
                data: "grandTotal",
                render: function(data, type, row) {
                    return `<span class="font-semibold text-slate-800">${Number(data).toFixed(2)} ${row.currency}</span>`;
                }
            },
            {
                title: 'Kalan',
                data: null,
                render: function(data, type, row) {
                    var outstandingAmount = row.grandTotal - row.paidAmount;
                    return `<span class="font-semibold text-slate-800">${Number(outstandingAmount).toFixed(2)} ${row.currency}</span>`;
                }
            },
            {
                title: 'Durum',
                data: "status",
                render: function(data) {
                    if(data === 1) return '<span class="bg-slate-100 text-slate-800 text-xs font-medium px-2.5 py-0.5 rounded">Taslak</span>';
                    if(data === 2) return '<span class="bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded">Gönderildi</span>';
                    if(data === 3) return '<span class="bg-green-100 text-green-800 text-xs font-medium px-2.5 py-0.5 rounded">Onaylandı</span>';
                    if(data === 4) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">Reddedildi</span>';
                    if(data === 5) return '<span class="bg-red-100 text-red-800 text-xs font-medium px-2.5 py-0.5 rounded">İptal</span>';
                    if(data === 6) return '<span class="bg-emerald-100 text-emerald-800 text-xs font-medium px-2.5 py-0.5 rounded">Ödendi</span>';
                    if(data === 7) return '<span class="bg-indigo-100 text-indigo-800 text-xs font-medium px-2.5 py-0.5 rounded">Tamamlandı</span>';
                    if(data === 8) return '<span class="bg-amber-100 text-amber-800 text-xs font-medium px-2.5 py-0.5 rounded">Kısmi Ödendi</span>';
                    return data;
                }
            }
        ]
    }));

    $('#DueFilter').change(function () {
        dataTable.ajax.reload();
    });

});
