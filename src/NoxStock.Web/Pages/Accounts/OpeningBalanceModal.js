abp.modals.AccountOpeningBalanceModal = function () {
    return {
        initModal: function (publicApi) {
            var modal = publicApi.getModal();
            var amount = modal.find('[name="Opening.Amount"]');
            var preview = modal.find('#AccountOpeningPreview');
            var current = Number(preview.data('current')) || 0;
            var currency = modal.find('.finance-modal-intro small').text().split(' ')[0] || '';

            function refresh() {
                var value = Number(String(amount.val() || '0').replace(',', '.')) || 0;
                preview.text((current + value).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + currency);
            }

            amount.on('input', refresh);
            refresh();
        }
    };
};
