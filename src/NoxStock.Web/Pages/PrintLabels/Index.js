$(function () {
    var lookupService = noxStock.printLabels.labelProductLookup;
    var products = [];
    var queue = [];
    var searchTimer = null;
    var searchSequence = 0;

    function escapeHtml(value) { return $('<div>').text(value || '').html(); }
    function money(value, currency) { return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: currency || 'TRY' }).format(value || 0); }
    function codeValue(product) { return product.barcode || product.sku || product.id; }
    function addQuantity() { return Math.max(1, Math.min(100, Number($('#LabelAddQuantity').val()) || 1)); }

    function renderProducts() {
        var $body = $('#LabelProductResults').empty();
        if (!products.length) { $body.append('<tr><td colspan="4" class="px-4 py-8 text-center text-slate-400">Ürün bulunamadı.</td></tr>'); return; }
        products.forEach(function (product) { $body.append('<tr class="border-b border-slate-100 dark:border-slate-700"><td class="px-4 py-2"><button type="button" class="label-add-product rounded-md p-1 text-indigo-600 hover:bg-indigo-50" data-id="' + product.id + '" title="Etikete ekle"><span class="material-icons-round text-base">add_circle</span></button></td><td class="px-4 py-2 font-medium text-slate-800 dark:text-white">' + escapeHtml(product.name) + '</td><td class="px-4 py-2 text-xs text-slate-500">' + escapeHtml(product.barcode || product.sku) + '</td><td class="px-4 py-2 text-right font-semibold">' + money(product.salePrice, product.currency) + '</td></tr>'); });
    }

    function loadProducts() {
        var sequence = ++searchSequence;
        $('#LabelProductResults').html('<tr><td colspan="4" class="px-4 py-8 text-center text-slate-400">Ürünler aranıyor…</td></tr>');
        lookupService.getList({ filter: $('#LabelSearch').val() || null, maxResultCount: 30 }).then(function (items) {
            if (sequence !== searchSequence) return;
            products = items || [];
            renderProducts();
        }).catch(function () {
            if (sequence !== searchSequence) return;
            products = [];
            $('#LabelProductResults').html('<tr><td colspan="4" class="px-4 py-8 text-center text-rose-500">Ürünler yüklenemedi.</td></tr>');
        });
    }

    function renderQueue() {
        var $body = $('#LabelQueue').empty();
        if (!queue.length) { $body.append('<tr><td colspan="3" class="px-4 py-8 text-center text-slate-400">Yazdırma kuyruğu boş.</td></tr>'); }
        queue.forEach(function (item) { $body.append('<tr><td class="px-4 py-3"><div class="font-medium text-slate-800 dark:text-white">' + escapeHtml(item.product.name) + '</div><div class="mt-0.5 text-xs text-slate-500">' + escapeHtml(codeValue(item.product)) + '</div></td><td class="px-4 py-3"><div class="flex items-center justify-end gap-1"><button type="button" class="label-quantity rounded p-1 text-slate-500 hover:bg-slate-100" data-id="' + item.product.id + '" data-change="-1" title="Azalt"><span class="material-icons-round text-base">remove</span></button><span class="w-8 text-center font-semibold">' + item.quantity + '</span><button type="button" class="label-quantity rounded p-1 text-slate-500 hover:bg-slate-100" data-id="' + item.product.id + '" data-change="1" title="Artır"><span class="material-icons-round text-base">add</span></button></div></td><td class="px-4 py-3 text-right"><button type="button" class="label-remove rounded p-1 text-rose-600 hover:bg-rose-50" data-id="' + item.product.id + '" title="Çıkar"><span class="material-icons-round text-base">delete</span></button></td></tr>'); });
        renderPreview();
    }

    function renderPreview() {
        var $preview = $('#LabelPreview').empty();
        var format = $('#LabelFormat').val();
        var showName = $('#LabelShowName').is(':checked');
        var showPrice = $('#LabelShowPrice').is(':checked');
        queue.forEach(function (item) { for (var i = 0; i < item.quantity; i++) { var product = item.product; var $label = $('<div class="print-label"></div>'); if (showName) $label.append('<div class="print-label-name">' + escapeHtml(product.name) + '</div>'); var $code = $('<div class="print-label-code"></div>'); $label.append($code); if (showPrice) $label.append('<div class="print-label-price">' + money(product.salePrice, product.currency) + '</div>'); $preview.append($label); if (format === 'barcode' && window.JsBarcode) { var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); $code.append(svg); try { window.JsBarcode(svg, codeValue(product), { format: 'CODE128', displayValue: true, fontSize: 10, margin: 0, height: 34 }); } catch (error) { $code.text(codeValue(product)); } } else if (format === 'qr' && window.QRCode) { new window.QRCode($code[0], { text: codeValue(product), width: 50, height: 50, correctLevel: window.QRCode.CorrectLevel.M }); } else { $code.text(codeValue(product)); } } });
        if (!queue.length) $preview.append('<div class="col-span-full py-10 text-center text-sm text-slate-400">Önizleme için ürün ekleyin.</div>');
    }

    function addProduct(id) { var product = products.find(function (item) { return item.id === id; }); if (!product) return; var entry = queue.find(function (item) { return item.product.id === id; }); if (entry) entry.quantity += addQuantity(); else queue.push({ product: product, quantity: addQuantity() }); renderQueue(); }
    $('#LabelSearch').on('input', function () {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(loadProducts, 300);
    });
    $('#LabelProductResults').on('click', '.label-add-product', function () { addProduct($(this).data('id')); });
    $('#LabelQueue').on('click', '.label-quantity', function () { var id = $(this).data('id'); var entry = queue.find(function (item) { return item.product.id === id; }); if (!entry) return; entry.quantity += Number($(this).data('change')); if (entry.quantity <= 0) queue = queue.filter(function (item) { return item.product.id !== id; }); renderQueue(); });
    $('#LabelQueue').on('click', '.label-remove', function () { var id = $(this).data('id'); queue = queue.filter(function (item) { return item.product.id !== id; }); renderQueue(); });
    $('#LabelFormat, #LabelShowName, #LabelShowPrice').on('change', renderPreview);
    $('#PrintLabelsButton').on('click', function () { if (!queue.length) { abp.notify.warn('Yazdırmak için en az bir ürün ekleyin.'); return; } window.print(); });
    loadProducts();
    renderQueue();
});
