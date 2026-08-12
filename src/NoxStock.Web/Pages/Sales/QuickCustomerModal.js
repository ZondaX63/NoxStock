abp.modals.QuickCustomerModal = function () {
    var initModal = function (publicApi, args) {
        var $form = publicApi.getForm();
        $form.find('input[name="Customer.Name"]').trigger('focus');
    };
    return { initModal: initModal };
};
