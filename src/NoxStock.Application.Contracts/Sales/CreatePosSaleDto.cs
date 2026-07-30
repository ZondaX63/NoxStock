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
    public List<CreatePosSaleItemDto> Items { get; set; } = new();
}

public class CreatePosSaleItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool IsUnitPriceOverridden { get; set; }
}
