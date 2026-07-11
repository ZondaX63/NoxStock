/* Your Global Scripts */

// Suppress "Could not find localization source: AbpUi" console warnings.
// AbpUi keys are merged into the NexStock resource on the backend,
// but ABP's JS still looks for them under a separate "AbpUi" key.
// We pre-register the AbpUi namespace so the lookup succeeds silently.
(function () {
    var originalWarn = abp.log ? abp.log.warn : null;
    if (originalWarn) {
        abp.log.warn = function (msg) {
            if (msg && msg.indexOf && msg.indexOf('Could not find localization source: AbpUi') !== -1) {
                return; // swallow this specific warning
            }
            originalWarn.apply(abp.log, arguments);
        };
    }

    // Also pre-seed AbpUi values so actual lookups don't fail
    if (abp && abp.localization && abp.localization.values) {
        abp.localization.values['AbpUi'] = abp.localization.values['AbpUi'] || abp.localization.values['NexStock'] || {};
    }
})();

// Datatable localization overrides (Turkish)
$(function () {
    if (abp && abp.localization && abp.localization.values) {
        abp.localization.values['AbpUi'] = abp.localization.values['AbpUi'] || {};
        var ui = abp.localization.values['AbpUi'];
        ui['DatatableActionDropdownDefaultText'] = ui['DatatableActionDropdownDefaultText'] || 'İşlemler';
        ui['PagerShowMenuEntries'] = ui['PagerShowMenuEntries'] || '_MENU_ kayıt göster';
        ui['PagerInfo'] = ui['PagerInfo'] || 'Toplam _TOTAL_ kayıttan _START_ - _END_ arası gösteriliyor';
        ui['PagerInfoEmpty'] = ui['PagerInfoEmpty'] || 'Kayıt yok';
        ui['PagerFirst'] = ui['PagerFirst'] || 'İlk';
        ui['PagerPrevious'] = ui['PagerPrevious'] || 'Önceki';
        ui['PagerNext'] = ui['PagerNext'] || 'Sonraki';
        ui['PagerLast'] = ui['PagerLast'] || 'Son';
        ui['NoDataAvailableInDatatable'] = ui['NoDataAvailableInDatatable'] || 'Tabloda veri bulunamadı';
    }
});
