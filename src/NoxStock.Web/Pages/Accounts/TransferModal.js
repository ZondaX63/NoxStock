abp.modals.AccountTransferModal = function () {
    return {
        initModal: function (publicApi) {
            var modal = publicApi.getModal();
            var from = modal.find('[name="Transfer.FromAccountId"]');
            var to = modal.find('[name="Transfer.ToAccountId"]');
            var amount = modal.find('[name="Transfer.Amount"]');
            var warning = modal.find('#TransferCurrencyWarning');
            var save = modal.find('.modal-footer .btn-primary');
            save.text('Transferi Tamamla');

            function selected(select) {
                var option = select.find('option:selected');
                return { balance: Number(option.data('balance')) || 0, currency: option.data('currency') || '' };
            }

            function money(value, currency) {
                return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + currency;
            }

            function refresh() {
                var source = selected(from);
                var target = selected(to);
                var value = Number(String(amount.val() || '0').replace(',', '.')) || 0;
                var mismatch = source.currency && target.currency && source.currency !== target.currency;
                warning.toggleClass('d-none', !mismatch);
                save.prop('disabled', mismatch || !from.val() || !to.val() || from.val() === to.val() || value <= 0 || source.balance < value);
                modal.find('#TransferSourcePreview').text(source.currency ? money(source.balance - value, source.currency) : '—');
                modal.find('#TransferTargetPreview').text(target.currency ? money(target.balance + value, target.currency) : '—');
            }

            from.add(to).add(amount).on('change input', refresh);
            refresh();
        }
    };
};
