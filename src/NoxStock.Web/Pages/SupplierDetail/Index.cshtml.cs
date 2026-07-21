using System;
using Microsoft.AspNetCore.Mvc;

namespace NoxStock.Web.Pages.SupplierDetail;

public class IndexModel : NoxStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }
}
