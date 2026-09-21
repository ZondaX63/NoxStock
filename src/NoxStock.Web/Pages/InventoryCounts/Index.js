$(function () {
    'use strict';

    var service = noxStock.inventoryCounts.inventoryCount;
    var productService = noxStock.products.product;
    var l = abp.localization.getResource('NoxStock');
    var current = null;
    var items = [];
    var selectedProduct = null;
    var searchTimer = null;
    var searchRequest = 0;
    var dirty = false;

    function text(key, fallback) {
        var value = l(key);
        return value && value !== key ? value : fallback;
    }

    function compact(value) {
        var number = decimalValue(value);
        if (!Number.isFinite(number)) return '0';
        return number.toLocaleString(undefined, { maximumFractionDigits: 2 });
    }

    function decimalValue(value) {
        if (typeof value === 'string') {
            value = value.trim();
            if (value.indexOf(',') >= 0 && value.indexOf('.') < 0) value = value.replace(',', '.');
        }
        return Number(value || 0);
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : String(value)).html();
    }

    function errorMessage(error, fallback) {
        return error && error.message ? error.message : fallback;
    }

    function asPromise(request) {
        return new Promise(function (resolve, reject) {
            request.then(resolve, reject);
        });
    }

    function statusInfo(status) {
        if (status === 2) return { css: 'waiting', label: text('InventoryCount:StatusAwaiting', 'Onay bekliyor') };
        if (status === 3) return { css: 'completed', label: text('InventoryCount:StatusCompleted', 'Tamamlandı') };
        return { css: 'draft', label: text('InventoryCount:StatusDraft', 'Taslak (Devam Ediyor)') };
    }

    function setDirty(value) {
        dirty = value;
        $('#InventoryCountDirtyBadge').toggleClass('d-none', !dirty);
    }

    function isEditable() {
        return current && current.status === 1;
    }

    function updateHeader() {
        if (!current) return;
        var status = statusInfo(current.status);
        $('#InventoryCountReference').text(current.referenceNumber || '—');
        $('#InventoryCountName').val(current.name || '');
        $('#InventoryCountDate').text(current.countDate ? luxon.DateTime.fromISO(current.countDate).toFormat('dd.MM.yyyy HH:mm') : '—');
        $('#InventoryCountUser').text(current.countedByName || '—');
        $('#InventoryCountStatus').attr('class', 'inventory-count-status ' + status.css).text(status.label);
        $('#InventoryCountName, #InventoryCountProductSearch, #InventoryCountQuantity, #InventoryCountAddProduct, #InventoryCountQuickAdd, #InventoryCountImportFile, #InventoryCountRestoreFile')
            .prop('disabled', !isEditable());
        $('#InventoryCountSave, #InventoryCountSubmit').toggleClass('d-none', !isEditable());
        $('#InventoryCountReopen').toggleClass('d-none', current.status !== 2);
        $('#InventoryCountComplete').toggleClass('d-none', current.status === 3);
        $('#InventoryCountNew').toggleClass('d-none', current.status !== 3);
        if (current.status === 2) {
            $('#InventoryCountComplete').removeClass('d-none');
        }
    }

    function lineStatus(item) {
        var difference = Number(item.countedQuantity) - Number(item.systemQuantity);
        if (item.isQuickCreatedProduct) return '<span class="inventory-count-line-state new">' + escapeHtml(text('InventoryCount:LineNew', 'Sayımda eklendi')) + '</span>';
        if (difference < 0) return '<span class="inventory-count-line-state missing">' + escapeHtml(text('InventoryCount:LineMissing', 'Eksik stok')) + '</span>';
        if (difference > 0) return '<span class="inventory-count-line-state extra">' + escapeHtml(text('InventoryCount:LineExtra', 'Fazla stok')) + '</span>';
        return '<span class="inventory-count-line-state equal">' + escapeHtml(text('InventoryCount:LineEqual', 'Eşleşiyor')) + '</span>';
    }

    function renderItems() {
        var editable = isEditable();
        var html = items.map(function (item) {
            var difference = Number(item.countedQuantity) - Number(item.systemQuantity);
            var differenceClass = difference < 0 ? 'negative' : (difference > 0 ? 'positive' : 'neutral');
            return '<tr data-product-id="' + item.productId + '">' +
                '<td><strong>' + escapeHtml(item.sku || '—') + '</strong><span>' + escapeHtml(item.barcode || text('InventoryCount:NoBarcode', 'Barkod yok')) + '</span></td>' +
                '<td><strong>' + escapeHtml(item.productName) + '</strong><span>' + escapeHtml(item.unit || 'Adet') + (item.isQuickCreatedProduct ? ' · ' + escapeHtml(text('InventoryCount:Incomplete', 'Eksik bilgili')) : '') + '</span></td>' +
                '<td><span class="inventory-count-old-stock">' + compact(item.systemQuantity) + ' ' + escapeHtml(item.unit || '') + '</span></td>' +
                '<td><input class="inventory-count-inline-quantity" type="number" min="0" step="1" value="' + Number(item.countedQuantity || 0) + '" ' + (editable ? '' : 'disabled') + ' aria-label="' + escapeHtml(text('InventoryCount:CountedQuantity', 'Sayılan miktar')) + '" /></td>' +
                '<td><strong class="inventory-count-difference ' + differenceClass + '">' + (difference > 0 ? '+' : '') + compact(difference) + '</strong></td>' +
                '<td><div class="inventory-count-status-note">' + lineStatus(item) + '<input class="inventory-count-inline-note" maxlength="500" value="' + escapeHtml(item.note || '') + '" placeholder="' + escapeHtml(text('InventoryCount:OptionalNote', 'Not ekle')) + '" ' + (editable ? '' : 'disabled') + ' /></div></td>' +
                '<td class="inventory-count-action-column"><button type="button" class="inventory-count-remove" ' + (editable ? '' : 'disabled') + ' title="' + escapeHtml(text('Delete', 'Sil')) + '">' + escapeHtml(text('Delete', 'Sil')) + '</button></td>' +
                '</tr>';
        }).join('');

        $('#InventoryCountLines').html(html);
        $('#InventoryCountEmpty').toggleClass('d-none', items.length > 0);
        updateSummary();
    }

    function updateSummary() {
        var totalQuantity = items.reduce(function (sum, item) { return sum + Number(item.countedQuantity || 0); }, 0);
        var different = items.filter(function (item) { return Number(item.countedQuantity) !== Number(item.systemQuantity); }).length;
        $('#InventoryCountTypeTotal').text(items.length);
        $('#InventoryCountQuantityTotal').text(compact(totalQuantity));
        $('#InventoryCountDifferenceTotal').text(different);
        $('#InventoryCountZeroTotal').text(current ? Number(current.productsThatWillBeZeroed || 0) : 0);
    }

    function refreshRenderedRow(item) {
        var $row = $('#InventoryCountLines tr[data-product-id="' + item.productId + '"]');
        if (!$row.length) return false;
        var difference = Number(item.countedQuantity) - Number(item.systemQuantity);
        $row.find('.inventory-count-inline-quantity').val(Number(item.countedQuantity || 0));
        $row.find('.inventory-count-difference')
            .removeClass('negative positive neutral')
            .addClass(difference < 0 ? 'negative' : (difference > 0 ? 'positive' : 'neutral'))
            .text((difference > 0 ? '+' : '') + compact(difference));
        $row.find('.inventory-count-line-state').replaceWith(lineStatus(item));
        return true;
    }

    function hydrate(dto) {
        current = dto;
        items = (dto.items || []).map(function (item) { return $.extend({}, item); });
        setDirty(false);
        updateHeader();
        renderItems();
    }

    function loadCurrent() {
        $('.inventory-count-page').addClass('is-loading');
        service.getOrCreateDraft().then(function (dto) {
            hydrate(dto);
            loadHistory();
            window.setTimeout(function () { $('#InventoryCountProductSearch').trigger('focus'); }, 50);
        }).catch(function (error) {
            abp.notify.error(errorMessage(error, text('InventoryCount:LoadError', 'Sayım yüklenemedi.')));
        }).always(function () {
            $('.inventory-count-page').removeClass('is-loading');
        });
    }

    function payload() {
        return {
            id: current ? current.id : null,
            name: ($('#InventoryCountName').val() || '').trim(),
            items: items.map(function (item) {
                return { productId: item.productId, countedQuantity: Number(item.countedQuantity || 0), note: item.note || null };
            })
        };
    }

    function safeFilePart(value) {
        return String(value || 'stok-sayimi')
            .replace(/[^a-z0-9ğüşıöç_-]+/gi, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 80) || 'stok-sayimi';
    }

    function downloadBackup(dto) {
        var backup = {
            format: 'NoxStock.InventoryCount.Backup',
            version: 1,
            exportedAtUtc: new Date().toISOString(),
            count: {
                id: dto.id,
                referenceNumber: dto.referenceNumber,
                name: dto.name,
                countDate: dto.countDate,
                status: dto.status,
                countedByName: dto.countedByName,
                countedProductCount: dto.countedProductCount,
                totalCountedQuantity: dto.totalCountedQuantity
            },
            items: (dto.items || []).map(function (item) {
                return {
                    productId: item.productId,
                    productName: item.productName,
                    sku: item.sku,
                    barcode: item.barcode,
                    unit: item.unit,
                    systemQuantity: Number(item.systemQuantity || 0),
                    countedQuantity: Number(item.countedQuantity || 0),
                    note: item.note || null,
                    isQuickCreatedProduct: Boolean(item.isQuickCreatedProduct)
                };
            })
        };
        var blob = new Blob([JSON.stringify(backup, null, 2)], { type: 'application/json;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.href = url;
        link.download = safeFilePart(dto.referenceNumber || dto.name) + '-yedek-' + new Date().toISOString().slice(0, 10) + '.json';
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () { URL.revokeObjectURL(url); }, 0);
    }

    function restoreBackup(backup) {
        if (!backup || backup.format !== 'NoxStock.InventoryCount.Backup' || backup.version !== 1 || !backup.count || !Array.isArray(backup.items)) {
            throw new Error(text('InventoryCount:InvalidBackup', 'Geçerli bir NoxStock stok sayımı yedek dosyası seçin.'));
        }

        var restoredItems = new Map();
        backup.items.forEach(function (item) {
            var productId = String(item && item.productId || '').trim();
            var quantity = Number(item && item.countedQuantity);
            if (!productId || !Number.isFinite(quantity) || quantity < 0) {
                throw new Error(text('InventoryCount:InvalidBackup', 'Geçerli bir NoxStock stok sayımı yedek dosyası seçin.'));
            }
            if (restoredItems.has(productId)) {
                throw new Error(text('InventoryCount:InvalidBackup', 'Geçerli bir NoxStock stok sayımı yedek dosyası seçin.'));
            }
            restoredItems.set(productId, {
                productId: productId,
                sku: item.sku == null ? null : String(item.sku).slice(0, 100),
                barcode: item.barcode == null ? null : String(item.barcode).slice(0, 100),
                countedQuantity: quantity,
                note: item.note == null ? null : String(item.note).slice(0, 500)
            });
        });

        return {
            id: current.id,
            name: String(backup.count.name || current.name || '').trim().slice(0, 150) || current.name,
            items: Array.from(restoredItems.values())
        };
    }

    function saveDraft(showNotice) {
        if (!current || !isEditable()) return Promise.resolve(current);
        var $button = $('#InventoryCountSave');
        $button.prop('disabled', true);
        return asPromise(service.saveDraft(payload())).then(function (dto) {
            hydrate(dto);
            if (showNotice) abp.notify.success(text('InventoryCount:Saved', 'Sayım taslağı kaydedildi.'));
            return dto;
        }).catch(function (error) {
            abp.notify.error(errorMessage(error, text('InventoryCount:SaveError', 'Sayım kaydedilemedi.')));
            throw error;
        }).finally(function () {
            $button.prop('disabled', false);
        });
    }

    function addProduct(product, amount, note, quickCreated) {
        if (!product) return;
        if (product.trackStock === false) {
            abp.notify.warn(text('InventoryCount:UntrackedProduct', 'Stok takibi kapalı ürünler sayıma eklenemez.'));
            return;
        }
        var quantity = decimalValue(amount);
        if (!Number.isFinite(quantity) || quantity < 0) {
            abp.notify.warn(text('InventoryCount:InvalidQuantity', 'Geçerli bir miktar girin.'));
            return;
        }
        var existing = items.find(function (item) { return item.productId === (product.id || product.productId); });
        if (existing) {
            existing.countedQuantity = Number(existing.countedQuantity || 0) + quantity;
            if (note) existing.note = note;
        } else {
            items.unshift({
                id: null,
                productId: product.id || product.productId,
                productName: product.name || product.productName,
                sku: product.sku,
                barcode: product.barcode,
                unit: product.unit || 'Adet',
                systemQuantity: Number(product.quantity != null ? product.quantity : product.systemQuantity || 0),
                countedQuantity: quantity,
                note: note || product.note || '',
                isQuickCreatedProduct: Boolean(quickCreated || product.isQuickCreatedProduct)
            });
        }
        selectedProduct = null;
        $('#InventoryCountProductSearch').val('').trigger('focus');
        $('#InventoryCountQuantity').val(1);
        $('#InventoryCountSearchResults').addClass('d-none').empty();
        setDirty(true);
        if (existing && refreshRenderedRow(existing)) updateSummary();
        else renderItems();
    }

    function renderSearchResults(results) {
        var $results = $('#InventoryCountSearchResults');
        if (!results.length) {
            $results.html('<div class="inventory-count-search-empty">' + escapeHtml(text('InventoryCount:NoProduct', 'Ürün bulunamadı. Hızlı ürün ekleyebilirsiniz.')) + '</div>').removeClass('d-none');
            return;
        }
        $results.html(results.map(function (product, index) {
            return '<button type="button" data-result-index="' + index + '"><span><strong>' + escapeHtml(product.name) + '</strong><small>' + escapeHtml(product.sku) + (product.barcode ? ' · ' + escapeHtml(product.barcode) : '') + '</small></span><em>' + compact(product.quantity) + ' ' + escapeHtml(product.unit || '') + '</em></button>';
        }).join('')).removeClass('d-none').data('products', results);
    }

    function searchProducts(query, addOnExact) {
        var requestId = ++searchRequest;
        return asPromise(productService.getLookupList({ filter: query, skipCount: 0, maxResultCount: 12 })).then(function (result) {
            if (requestId !== searchRequest) return [];
            var allRows = result.items || [];
            var rows = allRows.filter(function (row) { return row.trackStock !== false; });
            renderSearchResults(rows);
            if (addOnExact && rows.length) {
                var normalized = String(query).trim().toLocaleLowerCase('tr-TR');
                var exact = rows.find(function (row) {
                    return String(row.barcode || '').trim().toLocaleLowerCase('tr-TR') === normalized ||
                        String(row.sku || '').trim().toLocaleLowerCase('tr-TR') === normalized;
                });
                if (exact) addProduct(exact, $('#InventoryCountQuantity').val(), null, false);
            } else if (addOnExact && allRows.some(function (row) { return row.trackStock === false; })) {
                abp.notify.warn(text('InventoryCount:UntrackedProduct', 'Stok takibi kapalı ürünler sayıma eklenemez.'));
            }
            return rows;
        });
    }

    function loadHistory() {
        service.getList({ skipCount: 0, maxResultCount: 12 }).then(function (result) {
            var rows = result.items || [];
            $('#InventoryCountHistory').html(rows.length ? rows.map(function (row) {
                var status = statusInfo(row.status);
                return '<button type="button" data-count-id="' + row.id + '"><span><strong>' + escapeHtml(row.name) + '</strong><small>' + escapeHtml(row.referenceNumber) + ' · ' + luxon.DateTime.fromISO(row.countDate).toFormat('dd.MM.yyyy HH:mm') + '</small></span><span class="inventory-count-status ' + status.css + '">' + escapeHtml(status.label) + '</span><em>' + row.countedProductCount + ' ' + escapeHtml(text('InventoryCount:Items', 'kalem')) + '</em></button>';
            }).join('') : '<p class="inventory-count-history-empty">' + escapeHtml(text('InventoryCount:NoHistory', 'Henüz sayım geçmişi yok.')) + '</p>');
        });
    }

    $('#InventoryCountProductSearch').on('input', function () {
        var query = this.value.trim();
        selectedProduct = null;
        window.clearTimeout(searchTimer);
        if (!query) {
            $('#InventoryCountSearchResults').addClass('d-none').empty();
            return;
        }
        searchTimer = window.setTimeout(function () { searchProducts(query, false); }, 220);
    }).on('keydown', function (event) {
        if (event.key !== 'Enter') return;
        event.preventDefault();
        var query = this.value.trim();
        if (selectedProduct) {
            addProduct(selectedProduct, $('#InventoryCountQuantity').val(), null, false);
        } else if (query) {
            searchProducts(query, true);
        }
    });

    $('#InventoryCountSearchResults').on('click', 'button[data-result-index]', function () {
        var products = $('#InventoryCountSearchResults').data('products') || [];
        selectedProduct = products[Number($(this).data('result-index'))];
        addProduct(selectedProduct, $('#InventoryCountQuantity').val(), null, false);
    });

    $('#InventoryCountAddProduct').on('click', function () {
        if (selectedProduct) return addProduct(selectedProduct, $('#InventoryCountQuantity').val(), null, false);
        var query = $('#InventoryCountProductSearch').val().trim();
        if (query) searchProducts(query, true);
    });

    $('#InventoryCountLines').on('input', '.inventory-count-inline-quantity', function () {
        var $row = $(this).closest('tr');
        var productId = $row.data('product-id');
        var item = items.find(function (row) { return row.productId === productId; });
        if (!item) return;
        item.countedQuantity = Math.max(0, decimalValue(this.value));
        refreshRenderedRow(item);
        setDirty(true);
        updateSummary();
    }).on('change', '.inventory-count-inline-note', function () {
        var productId = $(this).closest('tr').data('product-id');
        var item = items.find(function (row) { return row.productId === productId; });
        if (item) { item.note = this.value; setDirty(true); }
    }).on('click', '.inventory-count-remove', function () {
        var productId = $(this).closest('tr').data('product-id');
        items = items.filter(function (row) { return row.productId !== productId; });
        setDirty(true);
        renderItems();
    });

    $('#InventoryCountName').on('input', function () { setDirty(true); });
    $('#InventoryCountSave').on('click', function () { saveDraft(true); });

    $('#InventoryCountBackup').on('click', function () {
        if (!current) return;
        var $button = $(this).prop('disabled', true);
        asPromise(service.get(current.id)).then(function (dto) {
            downloadBackup(dto);
            var message = text('InventoryCount:BackupDownloaded', 'Son kaydedilmiş sayım taslağının yedeği indirildi.');
            if (dirty) message += ' ' + text('InventoryCount:BackupUnsavedNotice', 'Ekrandaki kaydedilmemiş değişiklikler bu yedekte yer almaz.');
            abp.notify.success(message);
        }).catch(function (error) {
            abp.notify.error(errorMessage(error, text('InventoryCount:BackupError', 'Sayım yedeği indirilemedi.')));
        }).finally(function () {
            $button.prop('disabled', false);
        });
    });

    $('#InventoryCountRestoreFile').on('change', function () {
        var file = this.files && this.files[0];
        this.value = '';
        if (!file || !current || !isEditable()) return;
        var $input = $(this).prop('disabled', true);
        var reader = new FileReader();
        reader.onload = function () {
            var restoreInput;
            try {
                restoreInput = restoreBackup(JSON.parse(String(reader.result || '')));
            } catch (error) {
                abp.notify.error(errorMessage(error, text('InventoryCount:InvalidBackup', 'Geçerli bir NoxStock stok sayımı yedek dosyası seçin.')));
                $input.prop('disabled', false);
                return;
            }

            asPromise(abp.message.confirm(
                text('InventoryCount:RestoreBackupWarning', 'Bu işlem açık taslaktaki tüm sayım satırlarını yedekteki satırlarla değiştirir. Ürün stokları sayım tamamlanana kadar değişmez. Devam edilsin mi?'),
                text('InventoryCount:RestoreBackupTitle', 'Sayım yedeğini yükle'))).then(function (confirmed) {
                if (!confirmed) return null;
                return asPromise(service.restoreBackup(restoreInput));
            }).then(function (dto) {
                if (!dto) return;
                hydrate(dto);
                loadHistory();
                abp.notify.success(text('InventoryCount:BackupRestored', 'Sayım yedeği açık taslağa yüklendi. Stoklar henüz değiştirilmedi.'));
            }).catch(function (error) {
                if (error) abp.notify.error(errorMessage(error, text('InventoryCount:RestoreError', 'Sayım yedeği yüklenemedi.')));
            }).finally(function () {
                $input.prop('disabled', false);
            });
        };
        reader.onerror = function () {
            abp.notify.error(text('InventoryCount:RestoreError', 'Sayım yedeği yüklenemedi.'));
            $input.prop('disabled', false);
        };
        reader.readAsText(file, 'utf-8');
    });

    $('#InventoryCountSubmit').on('click', function () {
        saveDraft(false).then(function () {
            return asPromise(service.submitForApproval(current.id));
        }).then(function (dto) {
            hydrate(dto);
            abp.notify.success(text('InventoryCount:Submitted', 'Sayım onaya gönderildi.'));
            loadHistory();
        }).catch(function () { });
    });

    $('#InventoryCountReopen').on('click', function () {
        if (!current || current.status !== 2) return;
        var $button = $(this).prop('disabled', true);
        service.reopenDraft(current.id).then(function (dto) {
            hydrate(dto);
            abp.notify.success(text('InventoryCount:ReopenedNotice', 'Sayım yeniden düzenlemeye açıldı.'));
            loadHistory();
        }).catch(function (error) {
            abp.notify.error(errorMessage(error, text('InventoryCount:ReopenError', 'Sayım yeniden açılamadı.')));
        }).always(function () { $button.prop('disabled', false); });
    });

    $('#InventoryCountComplete').on('click', function () {
        var proceed = isEditable() ? saveDraft(false) : Promise.resolve(current);
        proceed.then(function () {
            return asPromise(abp.message.confirm(
                text('InventoryCount:CompleteWarning', 'DİKKAT: Sayım listesinde bulunmayan stok takipli tüm ürünlerin miktarı 0 yapılacak, listedeki ürünler sayılan miktarla güncellenecektir. Bu işlem geri alınamaz. Onaylıyor musunuz?'),
                text('InventoryCount:CompleteWarningTitle', 'Stokları sıfırla ve güncelle')));
        }).then(function (confirmed) {
            if (!confirmed) return null;
            $('#InventoryCountComplete').prop('disabled', true);
            return asPromise(service.complete({ id: current.id, confirmReset: true }));
        }).then(function (dto) {
            if (!dto) return;
            hydrate(dto);
            abp.notify.success(text('InventoryCount:CompletedNotice', 'Sayım tamamlandı ve stoklar güncellendi.'));
            loadHistory();
        }).catch(function (error) {
            if (error) abp.notify.error(errorMessage(error, text('InventoryCount:CompleteError', 'Sayım tamamlanamadı.')));
        }).finally(function () {
            $('#InventoryCountComplete').prop('disabled', false);
        });
    });

    $('#InventoryCountNew').on('click', function () { loadCurrent(); });

    $('#InventoryCountHistory').on('click', 'button[data-count-id]', function () {
        service.get($(this).data('count-id')).then(function (dto) { hydrate(dto); });
    });

    $('#InventoryCountQuickProductForm').on('submit', function (event) {
        event.preventDefault();
        var input = {
            barcode: $('#InventoryQuickBarcode').val().trim() || null,
            name: $('#InventoryQuickName').val().trim(),
            countedQuantity: decimalValue($('#InventoryQuickQuantity').val()),
            salePrice: $('#InventoryQuickSalePrice').val() === '' ? null : decimalValue($('#InventoryQuickSalePrice').val()),
            note: $('#InventoryQuickNote').val().trim() || null
        };
        var $button = $('#InventoryQuickSave').prop('disabled', true);
        service.quickCreateProduct(input).then(function (result) {
            addProduct(result, result.countedQuantity, result.note, true);
            bootstrap.Modal.getOrCreateInstance(document.getElementById('InventoryCountQuickProductModal')).hide();
            $('#InventoryCountQuickProductForm')[0].reset();
            $('#InventoryQuickQuantity').val(1);
            abp.notify.success(text('InventoryCount:QuickCreated', 'Ürün taslak olarak oluşturuldu ve sayıma eklendi.'));
        }).catch(function (error) {
            abp.notify.error(errorMessage(error, text('InventoryCount:QuickCreateError', 'Ürün oluşturulamadı.')));
        }).always(function () { $button.prop('disabled', false); });
    });

    $('#InventoryCountImportFile').on('change', function () {
        var file = this.files && this.files[0];
        this.value = '';
        if (!file) return;
        var reader = new FileReader();
        reader.onload = function () {
            var bytes = new Uint8Array(reader.result);
            var chunk = 0x8000;
            var binary = '';
            for (var offset = 0; offset < bytes.length; offset += chunk) {
                binary += String.fromCharCode.apply(null, bytes.subarray(offset, offset + chunk));
            }
            service.import({ fileName: file.name, content: btoa(binary) }).then(function (result) {
                (result.items || []).forEach(function (imported) {
                    var existing = items.find(function (row) { return row.productId === imported.productId; });
                    if (existing) {
                        existing.countedQuantity = Number(imported.countedQuantity);
                    } else {
                        items.push({
                            id: null,
                            productId: imported.productId,
                            productName: imported.productName,
                            sku: imported.sku,
                            barcode: imported.barcode,
                            unit: imported.unit || 'Adet',
                            systemQuantity: Number(imported.systemQuantity || 0),
                            countedQuantity: Number(imported.countedQuantity || 0),
                            note: '',
                            isQuickCreatedProduct: false
                        });
                    }
                });
                setDirty(true);
                renderItems();
                var message = result.matchedProductCount + ' ' + text('InventoryCount:ImportMatched', 'ürün sayıma aktarıldı.');
                if (result.skippedRowCount) message += ' ' + result.skippedRowCount + ' ' + text('InventoryCount:ImportSkipped', 'satır atlandı.');
                abp.notify.success(message);
            }).catch(function (error) {
                abp.notify.error(errorMessage(error, text('InventoryCount:ImportError', 'Sayım dosyası aktarılamadı.')));
            });
        };
        reader.readAsArrayBuffer(file);
    });

    $(document).on('click', function (event) {
        if (!$(event.target).closest('.inventory-count-search-wrap').length) {
            $('#InventoryCountSearchResults').addClass('d-none');
        }
    });

    loadCurrent();
});
