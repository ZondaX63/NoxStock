$(function () {
    var posService = noxStock.sales.posSale;
    var cart = [];
    var products = [];
    var checkoutOptions = { accounts: [], customers: [] };
    var paymentMethod = 1;
    var searchTimer;
    var operationKey = createOperationKey();
    var posQuantityStep = 1;

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
        return selectedSaleCurrency() || (cart.length ? cart[0].currency : 'TRY');
    }

    function selectedSaleCurrency() {
        return $('#PosSaleCurrency').val() || null;
    }

    function cartTotal() {
        return cart.reduce(function (total, item) { return total + item.salePrice * item.quantity; }, 0);
    }

    function renderProducts() {
        var $grid = $('#PosProductGrid');
        $grid.empty();
        if (!products.length) {
            $grid.append('<div class="col-span-full flex min-h-72 flex-col items-center justify-center text-slate-400"><span class="material-icons-round text-5xl opacity-40">inventory_2</span><p class="mt-3 font-medium">Ürün bulunamadı.</p><p class="mt-1 text-sm">Barkod, ürün adı, stok kodu veya OEM ile arayın.</p></div>');
            return;
        }

        products.forEach(function (product) {
            var stockText = product.trackStock ? product.quantity + ' ' + escapeHtml(product.unit) : 'Stok takibi yok';
            var stockClass = product.trackStock && product.quantity <= 0 ? 'text-rose-600' : 'text-slate-500';
            var disabled = product.trackStock && product.quantity <= 0 ? 'disabled opacity-50 cursor-not-allowed' : '';
            var pricingText = product.usesCurrentExchangeRate ? 'Güncel kur + %' + Number(product.profitMarkupPercent || 0) + ' marj' : 'Sabit ürün fiyatı';
            var cardPriceText = 'Kart satış: ' + formatMoney(product.originalSalePrice, product.saleCurrency);
            var purchasePriceText = 'Alış: ' + formatMoney(product.purchasePrice, product.purchaseCurrency);
            $grid.append('<button type="button" data-product-id="' + product.id + '" class="pos-product-card text-left border border-slate-200 dark:border-slate-700 hover:border-indigo-500 hover:bg-indigo-50/50 dark:hover:bg-indigo-900/20 rounded-lg p-4 transition-colors ' + disabled + '" ' + (disabled ? 'disabled' : '') + '>' +
                '<p class="font-semibold text-slate-800 dark:text-white truncate">' + escapeHtml(product.name) + '</p>' +
                '<p class="mt-1 text-xs text-slate-500 truncate">' + escapeHtml(product.sku) + (product.barcode ? ' · ' + escapeHtml(product.barcode) : '') + '</p>' +
                (product.oemCodes ? '<p class="mt-1 text-xs text-slate-500 truncate">OEM: ' + escapeHtml(product.oemCodes) + '</p>' : '') +
                '<p class="mt-2 text-xs text-slate-500">' + pricingText + '</p>' +
                '<p class="mt-1 text-xs text-slate-500">' + purchasePriceText + '</p>' +
                '<p class="mt-1 text-xs text-slate-500">' + cardPriceText + '</p>' +
                '<div class="mt-3 flex items-end justify-between gap-2"><span class="font-bold text-indigo-600 dark:text-indigo-400">' + formatMoney(product.salePrice, product.currency) + '</span><span class="text-xs ' + stockClass + '">' + stockText + '</span></div></button>');
        });
    }

    function renderCart() {
        var $items = $('#PosCartItems');
        $items.empty();
        if (!cart.length) {
            $items.append('<div class="p-10 text-center text-slate-400"><span class="material-icons-round text-5xl opacity-40">remove_shopping_cart</span><p class="mt-3 text-sm">Sepet boş.</p></div>');
        } else {
            cart.forEach(function (item) {
                var costText = item.lastPurchaseCostCurrencyAmount
                    ? 'Son alış: ' + formatMoney(item.lastPurchaseCostCurrencyAmount, item.lastPurchaseCostCurrencyCode || item.purchaseCurrency)
                    : 'Kart alış: ' + formatMoney(item.purchasePrice, item.purchaseCurrency);
                $items.append('<div class="p-4 flex items-center gap-3"><div class="min-w-0 flex-1"><p class="truncate font-semibold text-sm text-slate-800 dark:text-white">' + escapeHtml(item.name) + '</p><p class="text-xs text-slate-500 mt-1">' + costText + ' | Kart satış: ' + formatMoney(item.originalSalePrice, item.saleCurrency) + '</p><label class="mt-2 block text-xs text-slate-500">Satış fiyatı<input type="number" min="0" step="0.0001" class="pos-cart-price mt-1 w-full rounded-md border border-slate-200 bg-white px-2 py-1 text-sm font-semibold text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-white" data-product-id="' + item.id + '" value="' + Number(item.salePrice).toFixed(4) + '"></label></div><div class="flex items-center border border-slate-200 dark:border-slate-700 rounded-lg"><button type="button" class="pos-cart-decrease w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Azalt"><span class="material-icons-round text-base">remove</span></button><span class="w-8 text-center text-sm font-bold text-slate-800 dark:text-white">' + item.quantity + '</span><button type="button" class="pos-cart-increase w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Artır"><span class="material-icons-round text-base">add</span></button></div></div>');
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
            return account.type === accountType || (paymentMethod === 2 && account.type === 3);
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
        checkoutOptions.customers.forEach(function (customer) {
            $customer.append('<option value="' + customer.id + '">' + escapeHtml(customer.name) + ' (' + formatMoney(customer.balance, customer.currency) + ')</option>');
        });

        var isCredit = paymentMethod === 3;
        $('#PosAccountField').toggleClass('hidden', isCredit);
        $('#PosCustomerRequired').toggleClass('hidden', !isCredit);
    }

    function addToCart(product) {
        if (cart.length && cartCurrency() !== product.currency) {
            abp.notify.warn('POS sepetinde tek para birimiyle satış yapılabilir.');
            return;
        }
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
        posService.getProducts({ filter: filter, currency: selectedSaleCurrency(), maxResultCount: 30 }).then(function (items) {
            products = items.map(function (product) {
                product.quantityAvailable = product.quantity;
                return product;
            });
            renderProducts();
        });
    }

    function loadCheckoutOptions() {
        posService.getCheckoutOptions().then(function (options) {
            checkoutOptions = options;
            renderCheckoutOptions();
        });
    }

    function paymentMethodText(method) {
        if (method === 1) return 'Nakit';
        if (method === 2) return 'Banka / Kart';
        return 'Cari';
    }

    function loadRecentSales() {
        posService.getRecent().then(function (sales) {
            var $body = $('#RecentPosSalesBody').empty();
            if (!sales.length) {
                $body.append('<tr><td colspan="6" class="px-5 py-8 text-center text-slate-400">Henüz POS satışı yok.</td></tr>');
                return;
            }
            sales.forEach(function (sale) {
                var status = sale.status === 5 ? '<span class="text-xs font-semibold text-rose-600">İptal</span>' : '<span class="text-xs font-semibold text-emerald-600">Tamamlandı</span>';
                var action = sale.status === 5 ? '-' : '<button type="button" class="pos-cancel-sale text-rose-600 hover:underline" data-sale-id="' + sale.id + '">İptal Et</button>';
                $body.append('<tr><td class="px-5 py-3 font-semibold text-slate-800 dark:text-white">' + escapeHtml(sale.receiptNumber) + '</td><td class="px-5 py-3 text-slate-600 dark:text-slate-300">' + luxon.DateTime.fromISO(sale.date).toFormat('dd.MM.yyyy HH:mm') + '</td><td class="px-5 py-3 text-slate-600 dark:text-slate-300">' + escapeHtml(sale.businessPartnerName) + '</td><td class="px-5 py-3">' + paymentMethodText(sale.paymentMethod) + '<div class="mt-1">' + status + '</div></td><td class="px-5 py-3 text-right font-bold text-slate-800 dark:text-white">' + formatMoney(sale.grandTotal, sale.currency) + '</td><td class="px-5 py-3 text-right">' + action + '</td></tr>');
            });
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
            posService.getProducts({ filter: barcode, currency: selectedSaleCurrency(), maxResultCount: 30 }).then(function (items) {
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
    $('#PosCartItems').on('input', '.pos-cart-price', function () {
        var item = cart.find(function (cartItem) { return cartItem.id === $(this).data('product-id'); }.bind(this));
        var price = Number($(this).val());
        if (item && price >= 0) {
            item.salePrice = price;
            item.isUnitPriceOverridden = true;
            $('#PosSubtotal, #PosTotal').text(formatMoney(cartTotal(), cartCurrency()));
        }
    });
    $('#PosSaleCurrency').change(function () {
        if (cart.length) {
            cart = [];
            abp.notify.info('Satış para birimi değiştiği için sepet temizlendi.');
        }
        renderCart();
        renderCheckoutOptions();
        loadProducts();
    });
    $('#ClearPosCartButton').click(function () { cart = []; renderCart(); });
    $('#RefreshPosButton').click(function () { loadProducts(); loadCheckoutOptions(); loadRecentSales(); });

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
            currency: selectedSaleCurrency(),
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
