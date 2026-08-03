$(function () {
    var $themeMode = $('#ThemeMode');
    var savedTheme = localStorage.getItem('noxstock.theme');
    if (!savedTheme) {
        savedTheme = localStorage.getItem('darkMode') === 'true' ? 'dark' : 'light';
    }

    $themeMode.val(savedTheme);
    $themeMode.on('change', function () {
        var isDark = this.value === 'dark';
        localStorage.setItem('noxstock.theme', this.value);
        localStorage.setItem('darkMode', isDark ? 'true' : 'false');
        document.documentElement.classList.toggle('dark', isDark);
        if (window.Alpine) {
            window.Alpine.$data(document.body).darkMode = isDark;
        }
    });

    var $resetConfirmation = $('#TenantDataResetConfirmation');
    var $resetAcknowledge = $('#TenantDataResetAcknowledge');
    var $resetSubmit = $('#TenantDataResetSubmit');

    function refreshResetButton() {
        var isConfirmed = ($resetConfirmation.val() || '').trim().toLocaleUpperCase('tr-TR') === 'SIFIRLA';
        $resetSubmit.prop('disabled', !isConfirmed || !$resetAcknowledge.is(':checked'));
    }

    $resetConfirmation.on('input', refreshResetButton);
    $resetAcknowledge.on('change', refreshResetButton);

    $('#TenantDataResetModal').on('hidden.bs.modal', function () {
        $resetConfirmation.val('');
        $resetAcknowledge.prop('checked', false);
        refreshResetButton();
    });

    $('#TenantDataResetForm').on('submit', function () {
        $resetSubmit.prop('disabled', true).html(
            '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>Sıfırlanıyor…'
        );
    });
});
