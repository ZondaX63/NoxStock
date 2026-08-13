$(function () {
    var canViewCost = Boolean(abp.auth && abp.auth.isGranted && abp.auth.isGranted('NoxStock.Products.Edit'));
    var posService = noxStock.sales.posSale;
    var cart = [];
    var products = [];
    var checkoutOptions = { accounts: [] };
    var paymentMethod = 1;
    var searchTimer;
    var operationKey = createOperationKey();
    var posQuantityStep = 1;
    var productRequestSequence = 0;
    var customerRequestSequence = 0;
    var customerSearchTimer;
    var customerLookupCache = new Map();
    var customerLookupById = new Map();
    var quickCustomerModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'Sales/QuickCustomerModal',
        modalClass: 'QuickCustomerModal'
    });
    var quickCustomerName = '';

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
        $('#PosAccount')
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

    function normalizeSearchText(value) {
        return String(value || '').trim().toLocaleLowerCase('tr-TR')
            .replace(/ı/g, 'i').replace(/ğ/g, 'g').replace(/ü/g, 'u')
            .replace(/ş/g, 's').replace(/ö/g, 'o').replace(/ç/g, 'c');
    }

    function formatMoney(value, currency) {
        var locale = document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR';
        return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'TRY' }).format(value || 0);
    }

    function compactInputNumber(value) {
        var number = Number(value || 0);
        if (!Number.isFinite(number)) return '0';
        return number.toFixed(2).replace(/(\.\d*?[1-9])0+$/, '$1').replace(/\.0+$/, '').replace(/\.$/, '');
    }

    function cartCurrency() {
        return 'TRY';
    }

    function cartTotal() {
        return cart.reduce(function (total, item) { return total + customerFinalUnitPrice(item) * item.quantity; }, 0);
    }

    function cartSubtotal() {
        return cart.reduce(function (total, item) { return total + Number(item.salePrice || 0) * item.quantity; }, 0);
    }

    function cartDiscountTotal() {
        return cart.reduce(function (total, item) {
            return total + (Number(item.salePrice || 0) - netSaleUnitPrice(item)) * item.quantity;
        }, 0);
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

    function updateCartTotals() {
        var count = cart.reduce(function (total, item) { return total + item.quantity; }, 0);
        $('#PosCartCount span:last-child').text(count + (count === 1 ? ' ürün' : ' ürün'));
        $('#PosSubtotal').text(formatMoney(cartSubtotal(), cartCurrency()));
        $('#PosDiscountTotal').text('-' + formatMoney(cartDiscountTotal(), cartCurrency()));
        $('#PosTotal').text(formatMoney(cartTotal(), cartCurrency()));
    }

    function updateCartLineSummary(item) {
        var $row = $('#PosCartItems .pos-cart-item[data-product-id="' + item.id + '"]');
        if (!$row.length) {
            updateCartTotals();
            return;
        }

        $row.find('.pos-cart-final-price').text('Müşteri bitiş: ' + formatMoney(customerFinalUnitPrice(item), item.currency));
        $row.find('.pos-manual-price-status')
            .text(item.isUnitPriceOverridden ? 'Manuel fiyat uygulandı' : 'Önerilen fiyat kullanılıyor')
            .toggleClass('text-amber-600', item.isUnitPriceOverridden)
            .toggleClass('text-slate-500', !item.isUnitPriceOverridden);
        $row.find('.pos-vat-status')
            .text(item.isVatRateOverridden ? 'Satır KDV oranı değiştirildi' : '')
            .toggleClass('hidden', !item.isVatRateOverridden);
        $row.find('.pos-price-detail-list').text('Liste fiyatı (KDV hariç): ' + formatMoney(item.salePrice, item.currency));
        $row.find('.pos-price-detail-discount').text('Satış iskontosu: %' + (effectiveSalesDiscountRate(item) * 100).toFixed(2) + ' · KDV: %' + Number(item.vatRate || 0).toFixed(2));
        $row.find('.pos-price-detail-final').text('Müşteri bitiş fiyatı: ' + formatMoney(customerFinalUnitPrice(item), item.currency));
        var cost = Number(item.averageInventoryCostTry || item.lastPurchaseCostTryAmount || 0);
        var netSale = netSaleUnitPrice(item);
        var profit = netSale - cost;
        var rate = cost > 0 ? profit / cost * 100 : 0;
        var changed = item.isUnitPriceOverridden || effectiveSalesDiscountRate(item) > 0;
        var $profit = $row.find('.pos-profit-preview');
        $profit.toggleClass('hidden', !changed || cost <= 0)
            .toggleClass('is-loss', cost > 0 && profit < 0)
            .html(cost > 0 ? (
                (canViewCost ? '<span>Maliyet <strong>' + escapeHtml(formatMoney(cost, 'TRY')) + '</strong></span>' : '') +
                '<span>Yeni satış <strong>' + escapeHtml(formatMoney(netSale, 'TRY')) + '</strong></span>' +
                (canViewCost ? '<span>Tahmini kâr <strong>' + escapeHtml(formatMoney(profit, 'TRY')) + ' · %' + escapeHtml(compactInputNumber(rate)) + '</strong></span>' : '') +
                (profit < 0 ? '<b>Bu fiyat maliyetin altında' + (canViewCost ? '; birim zarar ' + escapeHtml(formatMoney(Math.abs(profit), 'TRY')) : '') + '.</b>' : '')
            ) : '');
        updateCartTotals();
    }

    function renderProducts() {
        var $grid = $('#PosProductGrid');
        $grid.attr('aria-busy', 'false').scrollTop(0).empty();
        if (!products.length) {
            $grid.append('<div class="col-span-full flex min-h-72 flex-col items-center justify-center text-slate-400"><span class="material-icons-round text-5xl opacity-40">inventory_2</span><p class="mt-3 font-medium">Ürün bulunamadı.</p><p class="mt-1 text-sm">Barkod, ürün adı, stok kodu veya OEM ile arayın.</p></div>');
            return;
        }

        products.forEach(function (product) {
            var stockText = product.trackStock ? product.quantity + ' ' + escapeHtml(product.unit) : 'Stok takibi yok';
            var stockClass = product.trackStock && product.quantity <= 0 ? 'text-rose-600' : 'text-slate-500';
            var cannotSell = product.trackStock && product.quantity <= 0;
            var disabled = cannotSell ? 'disabled opacity-50 cursor-not-allowed' : '';
            var isAutomatic = Number(product.pricingMode) === 1;
            var pricingText = isAutomatic
                ? 'Otomatik satış fiyatı'
                : 'Manuel kart fiyatı';
            var cardPriceText = product.isPriceAvailable === false
                ? 'Fiyat bilgisi yok · sepette elle girin'
                : 'Müşteri bitiş fiyatı: ' + formatMoney(product.customerFinalUnitPrice, product.currency);
            $grid.append('<button type="button" data-product-id="' + product.id + '" class="pos-product-card text-left border border-slate-200 dark:border-slate-700 hover:border-indigo-500 hover:bg-indigo-50/50 dark:hover:bg-indigo-900/20 rounded-lg p-4 transition-colors ' + disabled + '" ' + (disabled ? 'disabled' : '') + '>' +
                '<div class="pos-product-card-heading"><p class="pos-product-card-name">' + escapeHtml(product.name) + '</p><p class="pos-product-card-code">' + escapeHtml(product.sku) + (product.barcode ? ' · ' + escapeHtml(product.barcode) : '') + '</p><p class="pos-product-card-oem">' + (product.oemCodes ? 'OEM: ' + escapeHtml(product.oemCodes) : '&nbsp;') + '</p>' + (product.shelfLocation ? '<p class="pos-product-card-shelf">Raf: ' + escapeHtml(product.shelfLocation) + '</p>' : '') + '</div>' +
                '<div class="pos-product-card-details"><p class="pos-product-card-policy">' + pricingText + '</p><p>' + cardPriceText + '</p></div>' +
                '<div class="pos-product-card-footer"><span>' + (product.isPriceAvailable === false ? 'Elle fiyat girin' : formatMoney(product.customerFinalUnitPrice, product.currency)) + '</span><span class="' + stockClass + '">' + stockText + '</span></div></button>');
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
                var costText = Number(item.averageInventoryCostTry || 0) > 0
                    ? 'Güncel maliyet: ' + formatMoney(item.averageInventoryCostTry, 'TRY')
                    : sourceCost
                        ? 'Son alış: ' + formatMoney(sourceCost, sourceCurrency)
                        : 'Maliyet bilgisi yok';
                var isAutomatic = Number(item.pricingMode) === 1;
                var dynamicLabel = isAutomatic ? 'Otomatik fiyat' : 'Manuel kart fiyatı';
                var detail = '<div class="pos-price-detail hidden mt-3 rounded-md border border-indigo-100 bg-indigo-50/70 p-2.5 text-xs text-slate-700 dark:border-indigo-900/60 dark:bg-indigo-950/30 dark:text-slate-200">' +
                    '<p class="font-semibold">Fiyat hesabı</p>' +
                    (canViewCost ? '<p class="mt-1">' + costText + '</p>' : '') +
                    (isAutomatic ? '<p>Hedef kâr oranı: %' + compactInputNumber(item.profitMarkupPercent || 0) + '</p>' : '') +
                    '<p class="mt-1 pos-price-detail-list">Liste fiyatı (KDV hariç): ' + formatMoney(item.salePrice, item.currency) + '</p>' +
                    '<p class="pos-price-detail-discount">Satış iskontosu: %' + compactInputNumber(effectiveSalesDiscountRate(item) * 100) + ' · KDV: %' + compactInputNumber(item.vatRate || 0) + '</p>' +
                    '<p class="mt-1 font-semibold pos-price-detail-final">Müşteri bitiş fiyatı: ' + formatMoney(customerFinalUnitPrice(item), item.currency) + '</p>' +
                    '<p class="mt-1 text-slate-500">Sepete eklenirken kilitlendi. Güncelleme yalnızca yukarıdaki aksiyonla yapılır.</p></div>';
                var manualStatus = item.isUnitPriceOverridden ? '<p class="pos-manual-price-status mt-1 text-xs font-semibold text-amber-600">Manuel fiyat uygulandı</p>' : '<p class="pos-manual-price-status mt-1 text-xs text-slate-500">Önerilen fiyat kullanılıyor</p>';
                var vatStatus = '<p class="pos-vat-status mt-1 text-xs font-semibold text-amber-600' + (item.isVatRateOverridden ? '' : ' hidden') + '">' + (item.isVatRateOverridden ? 'Satır KDV oranı değiştirildi' : '') + '</p>';
                $items.append('<div class="pos-cart-item p-4 flex items-start gap-3" data-product-id="' + item.id + '"><div class="min-w-0 flex-1"><p class="truncate font-semibold text-sm text-slate-800 dark:text-white">' + escapeHtml(item.name) + '</p><p class="text-xs text-slate-500 mt-1">' + dynamicLabel + '</p><button type="button" class="pos-price-detail-toggle mt-2 text-xs font-semibold text-indigo-600 hover:text-indigo-800 dark:text-indigo-300" data-product-id="' + item.id + '">Fiyat hesabını göster</button><div class="pos-cart-price-fields mt-2"><label>Satış fiyatı (KDV hariç)<input type="number" min="0" step="0.01" inputmode="decimal" class="pos-cart-price mt-1 w-full rounded-md border border-slate-200 bg-white px-2 py-1 text-sm font-semibold text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-white" data-product-id="' + item.id + '" value="' + compactInputNumber(item.salePrice) + '"></label><label>KDV (%)<select class="pos-cart-vat mt-1 w-full rounded-md border border-slate-200 bg-white px-2 py-1 text-sm font-semibold text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-white" data-product-id="' + item.id + '"><option value="0"' + (Number(item.vatRate) === 0 ? ' selected' : '') + '>%0</option><option value="1"' + (Number(item.vatRate) === 1 ? ' selected' : '') + '>%1</option><option value="10"' + (Number(item.vatRate) === 10 ? ' selected' : '') + '>%10</option><option value="20"' + (Number(item.vatRate) === 20 ? ' selected' : '') + '>%20</option></select></label></div><div class="pos-profit-preview hidden" aria-live="polite"></div><p class="pos-cart-final-price mt-1 text-sm font-bold text-indigo-600 dark:text-indigo-300">Müşteri bitiş: ' + formatMoney(customerFinalUnitPrice(item), item.currency) + '</p>' + manualStatus + vatStatus + detail + '</div><div class="flex shrink-0 items-center border border-slate-200 dark:border-slate-700 rounded-lg"><button type="button" class="pos-cart-decrease w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Azalt"><span class="material-icons-round text-base">remove</span></button><span class="w-8 text-center text-sm font-bold text-slate-800 dark:text-white">' + item.quantity + '</span><button type="button" class="pos-cart-increase w-8 h-8 flex items-center justify-center text-slate-600 dark:text-slate-300" data-product-id="' + item.id + '" title="Artır"><span class="material-icons-round text-base">add</span></button></div></div>');
                var discountInputs = [1, 2, 3, 4].map(function (index) {
                    return '<label>İsk. ' + index + ' (%)<input type="number" min="0" max="100" step="1" inputmode="decimal" class="pos-cart-discount mt-1 w-full rounded-md border border-slate-200 bg-white px-2 py-1 text-sm font-semibold text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-white" data-product-id="' + item.id + '" data-discount-index="' + index + '" value="' + compactInputNumber(item['salesDiscount' + index] || 0) + '"></label>';
                }).join('');
                $items.children().last().find('.pos-cart-price-fields')
                    .after('<div class="mt-2 grid grid-cols-4 gap-1.5 text-[10px] text-slate-500">' + discountInputs + '</div>');
                updateCartLineSummary(item);
            });
        }
        updateCartTotals();
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

        var isCredit = paymentMethod === 3;
        $('#PosAccountField').toggleClass('hidden', isCredit);
        $('#PosCustomerRequired').toggleClass('hidden', !isCredit);
        $('#PosAccount, #PosCustomer, #PosCustomerSearch').prop('disabled', false);
    }

    function customerLabel(customer) {
        return customer.name +
            (customer.taxNumber ? ' · ' + customer.taxNumber : '') +
            ' (' + formatMoney(customer.balance, customer.currency) + ')';
    }

    function renderCustomerOptions(customers) {
        var $select = $('#PosCustomer');
        var selectedValue = String($select.val() || '');
        var selectedText = selectedValue ? $select.find('option:selected').text() : '';
        $select.empty().append(new Option('Perakende müşteri', ''));
        (customers || []).forEach(function (customer) {
            customerLookupById.set(String(customer.id), customer);
            $select.append(new Option(customerLabel(customer), customer.id));
        });
        if (selectedValue && !$select.find('option[value="' + selectedValue + '"]').length) {
            $select.append(new Option(selectedText, selectedValue));
        }
        $select.val(selectedValue);
    }

    function getCustomerDiscountDefaults() {
        var customer = customerLookupById.get(String($('#PosCustomer').val() || ''));
        return customer
            ? [customer.defaultSalesDiscount1, customer.defaultSalesDiscount2, customer.defaultSalesDiscount3, customer.defaultSalesDiscount4]
            : [0, 0, 0, 0];
    }

    function setItemDiscounts(item, discounts) {
        item.salesDiscount1 = Number(discounts[0]) || 0;
        item.salesDiscount2 = Number(discounts[1]) || 0;
        item.salesDiscount3 = Number(discounts[2]) || 0;
        item.salesDiscount4 = Number(discounts[3]) || 0;
    }

    function applyCustomerDiscountDefaults(shouldRender) {
        var discounts = getCustomerDiscountDefaults();

        cart.forEach(function (item) {
            setItemDiscounts(item, discounts);
        });
        if (shouldRender !== false) {
            renderCart();
        }
    }

    function searchCustomers(selectBestMatch) {
        var query = String($('#PosCustomerSearch').val() || '').trim();
        var cacheKey = normalizeSearchText(query);
        var requestSequence = ++customerRequestSequence;
        $('#PosCustomerSearchStatus').text('Aranıyor...');

        var request = customerLookupCache.has(cacheKey)
            ? Promise.resolve(customerLookupCache.get(cacheKey))
            : posService.getCustomers({ filter: query, skipCount: 0, maxResultCount: 20 }).then(function (result) {
                if (customerLookupCache.size >= 30) {
                    customerLookupCache.delete(customerLookupCache.keys().next().value);
                }
                customerLookupCache.set(cacheKey, result);
                return result;
            });

        return request.then(function (result) {
            if (requestSequence !== customerRequestSequence) {
                return [];
            }
            var customers = result.items || [];
            renderCustomerOptions(customers);
            $('#PosCustomerSearch').data('lookupLoaded', true);
            $('#PosCustomerSearchStatus').text(customers.length
                ? customers.length + (Number(result.totalCount || 0) > customers.length ? '+ sonuç bulundu; aramayı daraltabilirsiniz.' : ' sonuç bulundu.')
                : 'Eşleşen müşteri bulunamadı.');

            if (selectBestMatch && customers.length) {
                var normalizedQuery = normalizeSearchText(query);
                var exactMatch = customers.find(function (customer) {
                        return [customer.name, customer.taxNumber, customer.phone].some(function (value) {
                        return normalizeSearchText(value) === normalizedQuery;
                    });
                });
                var selectedCustomer = exactMatch || (customers.length === 1 ? customers[0] : null);
                if (selectedCustomer) {
                    $('#PosCustomer').val(selectedCustomer.id).trigger('change');
                    $('#PosCustomerSearch').val('');
                }
            }
            return customers;
        }).catch(function () {
            if (requestSequence === customerRequestSequence) {
                $('#PosCustomerSearchStatus').text('Müşteri araması şu anda yapılamıyor. Tekrar deneyin.');
            }
            return [];
        });
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
            item = $.extend({}, product, { quantity: 1, isUnitPriceOverridden: false, isVatRateOverridden: false });
            setItemDiscounts(item, getCustomerDiscountDefaults());
            cart.push(item);
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
                if (!item.isVatRateOverridden) {
                    item.vatRate = latest.vatRate;
                }
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
            $('#PosAccount')
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
                var receipt = '<a class="mr-3 font-semibold text-indigo-600 hover:underline" target="_blank" rel="noopener" href="' + (abp.appPath || '/') + 'Sales/Receipt?id=' + sale.id + '">Fiş</a>';
                var action = receipt + (sale.status === 5 ? '' : '<button type="button" class="pos-cancel-sale text-rose-600 hover:underline" data-sale-id="' + sale.id + '">İptal Et</button>');
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

    $('#PosCustomerSearch').on('input', function () {
        window.clearTimeout(customerSearchTimer);
        customerSearchTimer = window.setTimeout(function () {
            searchCustomers(false);
        }, 250);
    }).on('focus', function () {
        if (!$(this).data('lookupLoaded') && !String($(this).val() || '').trim()) {
            searchCustomers(false);
        }
    }).on('keydown', function (event) {
        if (event.key !== 'Enter') {
            return;
        }
        event.preventDefault();
        window.clearTimeout(customerSearchTimer);
        searchCustomers(true);
    });
    $('#PosCustomer').on('change', applyCustomerDiscountDefaults);

    $('#AddPosCustomerButton').on('click', function () {
        quickCustomerName = '';
        quickCustomerModal.open();
    });

    quickCustomerModal.onResult(function (result) {
        var created = result && result.id ? result : (result && result.result ? result.result : null);
        var query = created && created.name ? created.name : quickCustomerName;
        customerLookupCache.clear();
        $('#PosCustomerSearch').val(query || '').data('lookupLoaded', false);
        searchCustomers(true).then(function () {
            if (created && created.id && $('#PosCustomer option[value="' + created.id + '"]').length) {
                $('#PosCustomer').val(created.id).trigger('change');
            }
        });
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
            updateCartLineSummary(item);
        }
    });
    $('#PosCartItems').on('change', '.pos-cart-vat', function () {
        var item = cart.find(function (cartItem) { return cartItem.id === $(this).data('product-id'); }.bind(this));
        var vatRate = Number($(this).val());
        if (item && vatRate >= 0 && vatRate <= 100) {
            item.vatRate = vatRate;
            item.isVatRateOverridden = true;
            updateCartLineSummary(item);
        }
    });
    $('#PosCartItems').on('input', '.pos-cart-discount', function () {
        var item = cart.find(function (cartItem) { return cartItem.id === $(this).data('product-id'); }.bind(this));
        var discount = Number($(this).val());
        var index = Number($(this).data('discount-index'));
        if (item && index >= 1 && index <= 4 && discount >= 0 && discount <= 100) {
            item['salesDiscount' + index] = discount;
            updateCartLineSummary(item);
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
        if (paymentMethod === 3 && !$('#PosCustomerSearch').data('lookupLoaded')) {
            searchCustomers(false);
        }
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
        var belowCostItems = cart.filter(function (item) {
            var cost = Number(item.averageInventoryCostTry || item.lastPurchaseCostTryAmount || 0);
            return cost > 0 && netSaleUnitPrice(item) < cost;
        });
        var submitSale = function (confirmBelowCost) {
        var $button = $('#CompletePosSaleButton').prop('disabled', true);
        posService.create({
            operationKey: operationKey,
            paymentMethod: paymentMethod,
            accountId: accountId,
            businessPartnerId: businessPartnerId,
            note: $('#PosNote').val() || null,
            confirmBelowCost: confirmBelowCost === true,
            items: cart.map(function (item) {
                return {
                    productId: item.id,
                    quantity: item.quantity,
                    unitPrice: item.salePrice,
                    vatRate: item.vatRate,
                    salesDiscount1: item.salesDiscount1 || 0,
                    salesDiscount2: item.salesDiscount2 || 0,
                    salesDiscount3: item.salesDiscount3 || 0,
                    salesDiscount4: item.salesDiscount4 || 0,
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
        };
        if (belowCostItems.length) {
            abp.message.confirm(
                belowCostItems.map(function (item) { return item.name; }).join(', ') +
                ' için net satış fiyatı stok maliyetinin altında. Yetkiniz varsa satışa devam edilsin mi?',
                'Zararına satış uyarısı'
            ).then(function (confirmed) { if (confirmed) submitSale(true); });
            return;
        }
        submitSale(false);
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
