using System;
using System.Collections.Generic;

namespace NoxStock.Sales;

public class CreatePosSaleDto
{
    public string? OperationKey { get; set; }
    public PosPaymentMethod PaymentMethod { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? Note { get; set; }
    public bool ConfirmBelowCost { get; set; }
    public List<CreatePosSaleItemDto> Items { get; set; } = new();
}

public class PosCancelInputDto
{
    public string? OperationKey { get; set; }
}

public class CreatePosSaleItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? SalesDiscount1 { get; set; }
    public decimal? SalesDiscount2 { get; set; }
    public decimal? SalesDiscount3 { get; set; }
    public decimal? SalesDiscount4 { get; set; }
    public bool IsUnitPriceOverridden { get; set; }
}
