$(function () {
    var posService = noxStock.sales.posSale;
    var cart = [];
    var products = [];
    var checkoutOptions = { accounts: [], customers: [] };
    var paymentMethod = 1;
    var searchTimer;
    var operationKey = createOperationKey();
    var posQuantityStep = 1;
    var productRequestSequence = 0;

    function productLoadingMarkup() {
        return Array.from({ length: 6 }, function () {
            return '<div class="pos-loading-card" aria-hidden="true"><i></i><i></i><i></i><i></i></div>';
        }).join('');
    }

    function recentSalesLoadingMarkup() {
        return Array.from({ length: 3 }, function () {
            return '<tr class="pos-loading-row" aria-hidden="true"><td colspan="6"><span></span><span></span><span></span></td></tr>';
        }).join('');
    }

    function setProductsLoading() {
        $('#PosProductGrid')
            .attr('aria-busy', 'true')
            .html(productLoadingMarkup());
    }

    function setCheckoutOptionsLoading() {
        $('#PosAccount, #PosCustomer')
            .prop('disabled', true)
            .html('<option>Seçenekler yükleniyor…</option>');
    }

    function setRecentSalesLoading() {
        $('#RecentPosSalesBody')
            .attr('aria-busy', 'true')
            .html(recentSalesLoadingMarkup());
    }

    function createOperationKey() {
        return window.crypto && window.crypto.randomUUID
            ? window.crypto.randomUUID()
            : Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
    }

    function escapeHtml(value) {
        return $('<div>').text(value || '').html();
    }

    function formatMoney(value, currency) {
        var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
        return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'TRY' }).format(value || 0);
    }

    function cartCurrency() {
        return 'TRY';
    }

    function cartTotal() {
        return cart.reduce(function (total, item) { return total + customerFinalUnitPrice(item) * item.quantity; }, 0);
    }

    function effectiveSalesDiscountRate(item) {
        var remainingFactor = 1;
        [item.salesDiscount1, item.salesDiscount2, item.salesDiscount3, item.salesDiscount4].forEach(function (discount) {
            remainingFactor *= 1 - Math.min(100, Math.max(0, Number(discount) || 0)) / 100;
        });
        return 1 - remainingFactor;
    }

    function netSaleUnitPrice(item) {
        return Number(item.salePrice || 0) * (1 - effectiveSalesDiscountRate(item));
    }

    function customerFinalUnitPrice(item) {
        return netSaleUnitPrice(item) * (1 + (Number(item.vatRate) || 0) / 100);
    }

    function renderProducts() {
        var $grid = $('#PosProductGrid');
        $grid.attr('aria-busy', 'false').empty();
        if (!products.length) {
            $grid.append('<div class="col-span-full flex min-h-72 flex-col items-center justify-center text-slate-400"><span class="material-icons-round text-5xl opacity-40">inventory_2</span><p class="mt-3 font-medium">Ürün bulunamadı.</p><p class="mt-1 text-sm">Barkod, ürün adı, stok kodu veya OEM ile arayın.</p></div>');
            return;
        }

        products.forEach(function (product) {
            var stockText = product.trackStock ? product.quantity + ' ' + escapeHtml(product.unit) : 'Stok takibi yok';
            var stockClass = product.trackStock && product.quantity <= 0 ? 'text-rose-600' : 'text-slate-500';
            var cannotSell = product.isPriceAvailable === false || (product.trackStock && product.quantity <= 0);
            var disabled = cannotSell ? 'disabled opacity-50 cursor-not-allowed' : '';
            var pricingText = product.usesCurrentExchangeRate
                ? 'Dinamik fiyat · ' + escapeHtml(product.referenceCurrencyCode || product.purchaseCurrency || 'TRY') + ' bazlı'
                : 'Manuel kart fiyatı';
            var cardPriceText = product.isPriceAvailable === false
                ? 'Onaylı alış maliyeti bekleniyor'
                : 'Müşteri bitiş fiyatı: ' + formatMoney(product.customerFinalUnitPrice, product.currency);
            var costText = product.lastPurchaseSourceNetUnitCost != null
                ? 'Son alış: ' + formatMoney(product.lastPurchaseSourceNetUnitCost, product.lastPurchaseSourceCurrencyCode || product.purchaseCurrency)
                : 'Maliyet bilgisi yok';
            $grid.append('<button type="button" data-product-id="' + product.id + '" class="pos-product-card text-left border border-slate-200 dark:border-slate-700 hover:border-indigo-500 hover:bg-indigo-50/50 dark:hover:bg-indigo-900/20 rounded-lg p-4 transition-colors ' + disabled + '" ' + (disabled ? 'disabled' : '') + '>' +
                '<p class="font-semibold text-slate-800 dark:text-white truncate">' + escapeHtml(product.name) + '</p>' +
                '<p class="mt-1 text-xs text-slate-500 truncate">' + escapeHtml(product.sku) + (product.barcode ? ' · ' + escapeHtml(product.barcode) : '') + '</p>' +
                (product.oemCodes ? '<p class="mt-1 text-xs text-slate-500 truncate">OEM: ' + escapeHtml(product.oemCodes) + '</p>' : '') +
                '<p class="mt-2 text-xs font-medium text-indigo-600 dark:text-indigo-300">' + pricingText + '</p>' +
                '<p class="mt-1 text-xs text-slate-500">' + costText + '</p>' +
                '<p class="mt-1 text-xs text-slate-500">' + cardPriceText + '</p>' +
                '<div class="mt-3 flex items-end justify-between gap-2"><span class="font-bold text-indigo-600 dark:text-indigo-400">' + (product.isPriceAvailable === false ? 'Fiyat hesaplanamadı' : formatMoney(product.customerFinalUnitPrice, product.currency)) + '</span><span class="text-xs ' + stockClass + '">' + stockText + '</span></div></button>');
        });
    }

    function renderCart() {
        var $items = $('#PosCartItems');
        $items.empty();
        if (!cart.length) {
            $items.append('<div class="p-10 text-center text-slate-400"><span class="material-icons-round text-5xl opacity-40">remove_shopping_cart</span><p class="mt-3 text-sm">Sepet boş.</p></div>');
        } else {
            cart.forEach(function (item) {
                var sourceCurrency = item.lastPurchaseSourceCurrencyCode || item.lastPurchaseCostCurrencyCode || item.purchaseCurrency || 'TRY';
                var sourceCost = item.lastPurchaseSourceNetUnitCost || item.lastPurchaseCostCurrencyAmount;
                var costText = sourceCost
                    ? 'Son alış: ' + formatMoney(sourceCost, sourceCurrency)
                    : 'Maliyet bilgisi yok';
                var dynamicLabel = item.usesCurrentExchangeRate ? 'Dinamik fiyat' : 'Manuel kart fiyatı';
                var detail = '<div class="pos-price-detail hidden mt-3 rounded-md border border-indigo-100 bg-indigo-50/70 p-2.5 text-xs text-slate-700 dark:border-indigo-900/60 dark:bg-indigo-950/30 dark:text-slate-200">' +
                    '<p class="font-semibold">Fiyat hesabı</p>' +
                    '<p class="mt-1">' + costText + '</p>' +
                    (item.usesCurrentExchangeRate ? '<p>Güncel ' + escapeHtml(item.referenceCurrencyCode || sourceCurrency) + '/TRY kuru: ' + Number(item.referenceCurrencyExchangeRate || 1).toFixed(4) + '</p><p>Maliyet üzeri kâr: %' + Number(item.profitMarkupPercent || 0).toFixed(2) + '</p>' : '') +
                    (item.referenceSaleUnitPrice ? '<p>Referans satış: ' + formatMoney(item.referenceSaleUnitPrice, item.referenceCurrencyCode || sourceCurrency) + '</p>' : '') +
                    '<p class="mt-1">Liste fiyatı (KDV hariç): ' + formatMoney(item.salePrice, item.currency) + '</p>' +
                    '<p>Satış iskontosu: %' + (effectiveSalesDiscountRate(item) * 100).toFixed(2) + ' · KDV: %' + Number(item.vatRate || 0).toFixed(2) + '</p>' +
                    '<p class="mt-1 font-semibold">Müşteri bitiş fiyatı: ' + formatMoney(customerFinalUnitPrice(item), item.currency) + '</p>' +
                    '<p class="mt-1 text-slate-500">Sepete eklenirken kilitlendi. Güncelleme yalnızca yukarıdaki aksiyonla yapılır.</p></div>';
                var manualStatus = item.isUnitPriceOverridden ? '<p class="pos-manual-price-status mt-1 text-xs font-semibold text-amber-600">Manuel fiyat uygulandı</p>' : '<p class="pos-manual-price-status mt-1 text-xs text-slate-500">Önerilen fiyat kullanılıyor</p>';
                $items.append('<div class="pos-cart-item p-4 flex items-start gap-3"><div class="min-w-0 flex-1"><p class="truncate font-semibold text-sm text-slate-800 dark:text-white">' + escapeHtml(item.name) + '</p><p class="text-xs text-slate-500 mt-1">' + dynamicLabel + ' · ' + costText + '</p><button type="button" class="pos-price-detail-toggle mt-2 text-xs font-semibold text-indigo-600 hover:text-indigo-800 dark:text-indigo-300" data-product-id="' + item.id + '">Fiyat hesabını göster</button><label class="mt-2 block text-xs text-slate-500">Liste satış fiyatı (KDV hariç)<input type="number" min="0" step="0.0001" class="pos-cart-price mt-1 w-full rounded-md border border-slate-200 bg-white px-2 py-1 text-sm font-semibold text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-white" data-product-id="' + item.id + '" value="' + Number(item.salePrice).toFixed(4) + '"></label><p class="mt-1 text-sm font-bold text-indigo-600 dark:text-indigo-300">Müşteri bitiş: ' + formatMoney(customerFinalUnitPrice(item), item.currency) + '</p>' + manualStatus + detail + '</div><div class="flex shrink-0 items-center border border-slate-200 dark:border-slate-700 rounded-lg"><button type="button" class="pos-cart-decrease w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Azalt"><span class="material-icons-round text-base">remove</span></button><span class="w-8 text-center text-sm font-bold text-slate-800 dark:text-white">' + item.quantity + '</span><button type="button" class="pos-cart-increase w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Artır"><span class="material-icons-round text-base">add</span></button></div></div>');
            });
        }
        var count = cart.reduce(function (total, item) { return total + item.quantity; }, 0);
        $('#PosCartCount').text(count + ' ürün');
        $('#PosSubtotal, #PosTotal').text(formatMoney(cartTotal(), cartCurrency()));
    }

    function renderCheckoutOptions() {
        var $account = $('#PosAccount').empty();
        var accountType = paymentMethod === 1 ? 1 : 2;
        var eligibleAccounts = checkoutOptions.accounts.filter(function (account) {
            return account.currency === 'TRY' &&
                (account.type === accountType || (paymentMethod === 2 && account.type === 3));
        });
        $account.append('<option value="">Hesap seçiniz</option>');
        eligibleAccounts.forEach(function (account) {
            $account.append('<option value="' + account.id + '">' + escapeHtml(account.name) + ' (' + escapeHtml(account.currency) + ')</option>');
        });
        var preferredAccountId = paymentMethod === 1 ? checkoutOptions.defaultCashAccountId : checkoutOptions.defaultBankAccountId;
        var preferredAccount = eligibleAccounts.find(function (account) {
            return account.id === preferredAccountId && account.currency === cartCurrency();
        });
        if (preferredAccount) {
            $account.val(preferredAccount.id);
        }

        var $customer = $('#PosCustomer').empty();
        $customer.append('<option value="">Perakende müşteri</option>');
        checkoutOptions.customers.filter(function (customer) { return customer.currency === 'TRY'; }).forEach(function (customer) {
            $customer.append('<option value="' + customer.id + '">' + escapeHtml(customer.name) + ' (' + formatMoney(customer.balance, customer.currency) + ')</option>');
        });

        var isCredit = paymentMethod === 3;
        $('#PosAccountField').toggleClass('hidden', isCredit);
        $('#PosCustomerRequired').toggleClass('hidden', !isCredit);
        $('#PosAccount, #PosCustomer').prop('disabled', false);
    }

    function addToCart(product) {
        var item = cart.find(function (cartItem) { return cartItem.id === product.id; });
        if (item) {
            if (product.trackStock && item.quantity >= product.quantity) {
                abp.notify.warn('Stok miktarından fazla ürün eklenemez.');
                return;
            }
            item.quantity += posQuantityStep;
        } else {
            cart.push($.extend({}, product, { quantity: 1, isUnitPriceOverridden: false }));
        }
        renderCart();
        renderCheckoutOptions();
    }

    function updateCartQuantity(productId, delta) {
        var item = cart.find(function (cartItem) { return cartItem.id === productId; });
        if (!item) return;
        if (delta > 0 && item.trackStock && item.quantity >= item.quantityAvailable) {
            abp.notify.warn('Stok miktarından fazla ürün eklenemez.');
            return;
        }
        item.quantity += delta * posQuantityStep;
        if (item.quantity <= 0) {
            cart = cart.filter(function (cartItem) { return cartItem.id !== productId; });
        }
        renderCart();
    }

    function loadProducts() {
        var filter = $('#PosSearch').val();
        var requestSequence = ++productRequestSequence;
        setProductsLoading();
        posService.getProducts({ filter: filter, maxResultCount: 30 }).then(function (items) {
            if (requestSequence !== productRequestSequence) return;
            products = items.map(function (product) {
                product.quantityAvailable = product.quantity;
                return product;
            });
            renderProducts();
        }).catch(function () {
            if (requestSequence !== productRequestSequence) return;
            $('#PosProductGrid').attr('aria-busy', 'false').html('<div class="col-span-full flex min-h-72 flex-col items-center justify-center text-amber-600"><span class="material-icons-round text-4xl">currency_exchange</span><p class="mt-3 font-medium">Ürünler yüklenemedi.</p><p class="mt-1 text-sm text-slate-500">Bağlantıyı kontrol edip tekrar deneyin.</p></div>');
        });
    }

    function refreshCartPrices() {
        if (!cart.length) {
            return;
        }
        var productIds = cart.map(function (item) { return item.id; });
        Promise.all(productIds.map(function (productId) {
            var item = cart.find(function (cartItem) { return cartItem.id === productId; });
            return posService.getProducts({ filter: item ? item.sku : null, maxResultCount: 10 });
        })).then(function (resultSets) {
            var latestById = {};
            resultSets.forEach(function (items) {
                items.forEach(function (product) { latestById[product.id] = product; });
            });
            var refreshedCount = 0;
            cart.forEach(function (item) {
                var latest = latestById[item.id];
                if (!latest || item.isUnitPriceOverridden) {
                    return;
                }
                item.salePrice = latest.salePrice;
                item.listSalePrice = latest.listSalePrice;
                item.netSalePrice = latest.netSalePrice;
                item.customerFinalUnitPrice = latest.customerFinalUnitPrice;
                item.vatRate = latest.vatRate;
                item.salesDiscount1 = latest.salesDiscount1;
                item.salesDiscount2 = latest.salesDiscount2;
                item.salesDiscount3 = latest.salesDiscount3;
                item.salesDiscount4 = latest.salesDiscount4;
                item.currency = latest.currency;
                item.exchangeRate = latest.exchangeRate;
                item.referenceCurrencyExchangeRate = latest.referenceCurrencyExchangeRate;
                item.lastPurchaseSourceCurrencyCode = latest.lastPurchaseSourceCurrencyCode;
                item.lastPurchaseSourceNetUnitCost = latest.lastPurchaseSourceNetUnitCost;
                refreshedCount++;
            });
            renderCart();
            abp.notify.info(refreshedCount + ' satır güncel fiyatla yenilendi. Manuel fiyatlar korunur.');
        });
    }

    function loadCheckoutOptions() {
        setCheckoutOptionsLoading();
        posService.getCheckoutOptions().then(function (options) {
            checkoutOptions = options;
            renderCheckoutOptions();
        }).catch(function () {
            $('#PosAccount, #PosCustomer')
                .prop('disabled', false)
                .html('<option value="">Seçenekler yüklenemedi</option>');
        });
    }

    function paymentMethodText(method) {
        if (method === 1) return 'Nakit';
        if (method === 2) return 'Banka / Kart';
        return 'Cari';
    }

    function loadRecentSales() {
        setRecentSalesLoading();
        posService.getRecent().then(function (sales) {
            var $body = $('#RecentPosSalesBody').attr('aria-busy', 'false').empty();
            if (!sales.length) {
                $body.append('<tr><td colspan="6" class="px-5 py-8 text-center text-slate-400">Henüz POS satışı yok.</td></tr>');
                return;
            }
            sales.forEach(function (sale) {
                var status = sale.status === 5 ? '<span class="text-xs font-semibold text-rose-600">İptal</span>' : '<span class="text-xs font-semibold text-emerald-600">Tamamlandı</span>';
                var action = sale.status === 5 ? '-' : '<button type="button" class="pos-cancel-sale text-rose-600 hover:underline" data-sale-id="' + sale.id + '">İptal Et</button>';
                $body.append('<tr><td class="px-5 py-3 font-semibold text-slate-800 dark:text-white">' + escapeHtml(sale.receiptNumber) + '</td><td class="px-5 py-3 text-slate-600 dark:text-slate-300">' + luxon.DateTime.fromISO(sale.date).toFormat('dd.MM.yyyy HH:mm') + '</td><td class="px-5 py-3 text-slate-600 dark:text-slate-300">' + escapeHtml(sale.businessPartnerName) + '</td><td class="px-5 py-3">' + paymentMethodText(sale.paymentMethod) + '<div class="mt-1">' + status + '</div></td><td class="px-5 py-3 text-right font-bold text-slate-800 dark:text-white">' + formatMoney(sale.grandTotal, sale.currency) + '</td><td class="px-5 py-3 text-right">' + action + '</td></tr>');
            });
        }).catch(function () {
            $('#RecentPosSalesBody').attr('aria-busy', 'false').html('<tr><td colspan="6" class="px-5 py-8 text-center text-amber-600">Son POS satışları yüklenemedi.</td></tr>');
        });
    }

    $('#PosSearch').on('input', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(loadProducts, 250);
    }).on('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            var barcode = $('#PosSearch').val().trim();
            if (!barcode) return;
            posService.getProducts({ filter: barcode, maxResultCount: 30 }).then(function (items) {
                products = items.map(function (product) {
                    product.quantityAvailable = product.quantity;
                    return product;
                });
                renderProducts();
                var exactMatch = products.find(function (product) { return product.barcode === barcode; });
                if (exactMatch) addToCart(exactMatch);
            });
        }
    });

    $('#PosProductGrid').on('click', '.pos-product-card', function () {
        var product = products.find(function (item) { return item.id === $(this).data('product-id'); }.bind(this));
        if (product) addToCart(product);
    });
    $('#PosCartItems').on('click', '.pos-cart-increase', function () { updateCartQuantity($(this).data('product-id'), 1); });
    $('#PosCartItems').on('click', '.pos-cart-decrease', function () { updateCartQuantity($(this).data('product-id'), -1); });
    $('#PosCartItems').on('click', '.pos-price-detail-toggle', function () {
        var $detail = $(this).closest('.pos-cart-item').find('.pos-price-detail');
        $detail.toggleClass('hidden');
        $(this).text($detail.hasClass('hidden') ? 'Fiyat hesabını göster' : 'Fiyat hesabını gizle');
    });
    $('#PosCartItems').on('input', '.pos-cart-price', function () {
        var item = cart.find(function (cartItem) { return cartItem.id === $(this).data('product-id'); }.bind(this));
        var price = Number($(this).val());
        if (item && price >= 0) {
            item.salePrice = price;
            item.isUnitPriceOverridden = true;
            // Re-render so the VAT/discount-inclusive customer price and all totals
            // always reflect the manually entered list price immediately.
            renderCart();
        }
    });
    $('#ClearPosCartButton').click(function () { cart = []; renderCart(); });
    $('#RefreshPosButton').click(function () { loadProducts(); loadCheckoutOptions(); loadRecentSales(); });
    $('#RefreshPosPricesButton').click(refreshCartPrices);

    $('#PosPaymentMethods').on('click', '.pos-payment-method', function () {
        paymentMethod = Number($(this).data('method'));
        $('#PosPaymentMethods .pos-payment-method').removeClass('border-indigo-600 bg-indigo-600 text-white').addClass('border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300');
        $(this).removeClass('border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300').addClass('border-indigo-600 bg-indigo-600 text-white');
        renderCheckoutOptions();
    });

    $('#CompletePosSaleButton').click(function () {
        if (!cart.length) {
            abp.notify.warn('Satış için sepete en az bir ürün ekleyin.');
            return;
        }
        var accountId = $('#PosAccount').val() || null;
        var businessPartnerId = $('#PosCustomer').val() || null;
        if (paymentMethod !== 3 && !accountId) {
            abp.notify.warn('Kasa veya banka hesabı seçin.');
            return;
        }
        if (paymentMethod === 3 && !businessPartnerId) {
            abp.notify.warn('Cari satış için müşteri seçin.');
            return;
        }
        var $button = $(this).prop('disabled', true);
        posService.create({
            operationKey: operationKey,
            paymentMethod: paymentMethod,
            accountId: accountId,
            businessPartnerId: businessPartnerId,
            note: $('#PosNote').val() || null,
            items: cart.map(function (item) {
                return {
                    productId: item.id,
                    quantity: item.quantity,
                    unitPrice: item.salePrice,
                    isUnitPriceOverridden: item.isUnitPriceOverridden === true
                };
            })
        }).then(function (sale) {
            abp.notify.success(sale.receiptNumber + ' numaralı POS satışı tamamlandı.');
            cart = [];
            operationKey = createOperationKey();
            $('#PosNote').val('');
            renderCart();
            loadProducts();
            loadRecentSales();
        }).always(function () {
            $button.prop('disabled', false);
        });
    });

    $('#RecentPosSalesBody').on('click', '.pos-cancel-sale', function () {
        var saleId = $(this).data('sale-id');
        abp.message.confirm('Bu POS satışını iptal etmek istediğinize emin misiniz? Stok ve tahsilat ters kayıtla geri alınır.').then(function (confirmed) {
            if (!confirmed) return;
            posService.cancel(saleId).then(function () {
                abp.notify.info('POS satışı iptal edildi.');
                loadProducts();
                loadRecentSales();
            });
        });
    });

    renderCart();
    loadProducts();
    loadCheckoutOptions();
    loadRecentSales();
});
