abp.modals.BusinessPartnerOpeningBalanceModal = function () {
    return {
        initModal: function (publicApi) {
            var modal = publicApi.getModal();
            var preview = modal.find('#PartnerOpeningPreview');
            var amount = modal.find('[name="Opening.Amount"]');
            var direction = modal.find('[name="Opening.Direction"]');
            var current = Number(preview.data('current')) || 0;
            var currency = preview.data('currency') || 'TRY';
            function refresh() {
                var value = Number(String(amount.val() || '0').replace(',', '.')) || 0;
                var effect = Number(direction.val()) === 2 ? -value : value;
                var result = current + effect;
                var text = result > 0 ? 'Bizim alacağımız: ' : (result < 0 ? 'Bizim borcumuz: ' : 'Bakiye yok: ');
                preview.find('strong').text(text + Math.abs(result).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + currency);
            }
            amount.add(direction).on('input change', refresh);
            refresh();
        }
    };
};
