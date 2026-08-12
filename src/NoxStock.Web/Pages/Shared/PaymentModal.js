abp.modals.BusinessPartnerPaymentModal = function () {
    return {
        initModal: function (publicApi) {
            var modal = publicApi.getModal();
            var preview = modal.find('#PaymentPreview');
            var amount = modal.find('[name="Payment.Amount"]');
            var account = modal.find('[name="Payment.AccountId"]');
            var save = modal.find('.modal-footer .btn-primary');
            var partnerBalance = Number(preview.data('partner-balance')) || 0;
            var isCollection = String(preview.data('collection')) === 'true';
            var currency = preview.data('currency') || 'TRY';
            var format = function (value) { return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + currency; };

            function refresh() {
                var value = Number(String(amount.val() || '0').replace(',', '.')) || 0;
                var option = account.find('option:selected');
                var accountBalance = Number(option.data('balance'));
                var available = isCollection ? Math.max(0, partnerBalance) : Math.max(0, -partnerBalance);
                var warning = '';
                if (value > available) warning = isCollection
                    ? 'Bu müşteriden en fazla ' + format(available) + ' tahsilat kaydedebilirsiniz.'
                    : 'Bu tedarikçiye en fazla ' + format(available) + ' ödeme kaydedebilirsiniz.';
                if (!isCollection && Number.isFinite(accountBalance) && value > accountBalance)
                    warning = 'Seçilen hesapta bu ödeme için yeterli bakiye bulunmuyor.';

                preview.find('[data-partner-preview]').text(format(partnerBalance + (isCollection ? -value : value)));
                preview.find('[data-account-preview]').text(Number.isFinite(accountBalance) ? format(accountBalance + (isCollection ? value : -value)) : 'Hesap seçin');
                preview.find('[data-payment-warning]').text(warning).toggleClass('hidden', !warning);
                save.prop('disabled', !!warning || value <= 0 || !account.val());
            }

            amount.add(account).on('input change', refresh);
            save.text(isCollection ? 'Tahsilatı Kaydet' : 'Ödemeyi Kaydet');
            refresh();
        }
    };
};
