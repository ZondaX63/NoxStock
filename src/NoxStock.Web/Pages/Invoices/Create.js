$(function () {
    var $tbody = $('#ItemsTbody');
    var $template = $('#ItemRowTemplate').html();
    var currency = $('#InvoiceForm').data('currency') || 'TRY';
    var useProductPricing = String($('#InvoiceForm').data('use-product-pricing')) === 'true';

    function uuidv4() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function addRow() {
        var id = uuidv4();
        var html = $template.replace(/\{id\}/g, id);
        $tbody.append(html);
        calculateTotals();
    }

    function quoteProductPrice($row) {
        var productId = $row.find('.product-select').val();
        if (!productId) {
            return;
        }

        var quoteRequestId = Number($row.data('quoteRequestId') || 0) + 1;
        $row.data('quoteRequestId', quoteRequestId);
        $row.find('.price-overridden-input').val('false');

        var url = abp.appPath + 'api/app/product-pricing/quote?productId=' + encodeURIComponent(productId) + '&currency=' + encodeURIComponent(currency);
        fetch(url, { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : Promise.reject(); })
            .then(function (quote) {
                var isLatestQuote = Number($row.data('quoteRequestId')) === quoteRequestId;
                var hasManualPrice = $row.find('.price-overridden-input').val() === 'true';
                if (isLatestQuote && !hasManualPrice && useProductPricing) {
                    $row.find('.price-input').val(Number(quote.unitPrice || 0).toFixed(4));
                } else if (isLatestQuote && !hasManualPrice) {
                    $row.find('.price-input').val(Number(quote.purchasePrice || 0).toFixed(4));
                }

                var priceDetails = 'Alış: ' + Number(quote.purchasePrice || 0).toFixed(4) + ' ' + (quote.purchaseCurrency || 'TRY') +
                    ' | Kart satış: ' + Number(quote.salePrice || 0).toFixed(4) + ' ' + (quote.saleCurrency || 'TRY');
                if (quote.usesCurrentExchangeRate) {
                    priceDetails += ' | İşlem fiyatı güncel kur + %' + Number(quote.profitMarkupPercent || 0) + ' marj ile hesaplandı.';
                } else {
                    priceDetails += ' | İşlem fiyatı ürün kartından alındı.';
                }
                $row.find('.price-source').text(priceDetails);
                calculateTotals();
            })
            .catch(function () {
                $row.find('.price-source').text('Güncel ürün fiyatı alınamadı.');
            });
    }

    function calculateTotals() {
        var subtotal = 0;
        var discountTotal = 0;
        var vatTotal = 0;

        $tbody.find('.item-row').each(function () {
            var $row = $(this);
            var quantity = parseFloat($row.find('.quantity-input').val()) || 0;
            var price = parseFloat($row.find('.price-input').val()) || 0;
            var discountRate = parseFloat($row.find('.discount-input').val()) || 0;
            var vatRate = parseFloat($row.find('.vat-input').val()) || 0;

            var lineNet = quantity * price;
            var discount = lineNet * (discountRate / 100);
            var taxable = lineNet - discount;
            var vat = taxable * (vatRate / 100);
            var lineTotal = taxable + vat;

            $row.find('.row-total').text(lineTotal.toFixed(2));

            subtotal += lineNet;
            discountTotal += discount;
            vatTotal += vat;
        });

        var grandTotal = subtotal - discountTotal + vatTotal;

        $('#SumSubtotal').text(subtotal.toFixed(2) + ' ' + currency);
        $('#SumDiscount').text(discountTotal.toFixed(2) + ' ' + currency);
        $('#SumVat').text(vatTotal.toFixed(2) + ' ' + currency);
        $('#SumGrandTotal').text(grandTotal.toFixed(2) + ' ' + currency);
    }

    $('#AddRowBtn').click(function () {
        addRow();
    });

    $tbody.on('click', '.remove-row-btn', function () {
        $(this).closest('.item-row').remove();
        calculateTotals();
    });

    $tbody.on('input', '.calc-input', function () {
        if ($(this).hasClass('price-input')) {
            $(this).closest('.item-row').find('.price-overridden-input').val('true');
        }
        calculateTotals();
    });

    $tbody.on('change', '.product-select', function () {
        quoteProductPrice($(this).closest('.item-row'));
    });

    // Add first row by default
    if ($tbody.children().length === 0) {
        addRow();
    }
});
