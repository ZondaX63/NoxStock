$(function () {
    var createModal = new abp.ModalManager(abp.appPath + 'TenantManagement/Tenants/CreateModal');

    $('#NewTenantButton').on('click', function (event) {
        event.preventDefault();
        createModal.open();
    });

    createModal.onResult(function () {
        window.location.reload();
    });
});
