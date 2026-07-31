$(function () {
    var l = abp.localization.getResource('NoxStock');
    var createModal = new abp.ModalManager(abp.appPath + 'Brands/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'Brands/EditModal');

    var dataTable = $('#BrandsTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            stateSave: true,
            stateDuration: -1,
            searchDelay: 300,
            paging: true,
            order: [[1, "asc"]],
            searching: false,
            scrollX: true,
            ajax: abp.libs.datatables.createAjax(noxStock.brands.brand.getList),
            columnDefs: [
                {
                    title: l('Actions'),
                    rowAction: {
                        items:
                            [
                                {
                                    text: l('Edit'),
                                    action: function (data) {
                                        editModal.open({ id: data.record.id });
                                    }
                                },
                                {
                                    text: l('Delete'),
                                    confirmMessage: function (data) {
                                        return l('BrandDeletionConfirmationMessage', data.record.name);
                                    },
                                    action: function (data) {
                                        noxStock.brands.brand
                                            .delete(data.record.id)
                                            .then(function() {
                                                abp.notify.info(l('SuccessfullyDeleted'));
                                                dataTable.ajax.reload();
                                            });
                                    }
                                }
                            ]
                    }
                },
                {
                    title: l('Name'),
                    data: "name"
                }
            ]
        })
    );

    createModal.onResult(function () {
        dataTable.ajax.reload();
    });

    editModal.onResult(function () {
        dataTable.ajax.reload();
    });

    $('#NewBrandButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });
});
