using System;

namespace NoxStock.Documents;

public class CreateUpdateDocumentItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    // Dynamic exchange-rate pricing is a suggestion; a user-entered price belongs to the document.
    public bool IsUnitPriceOverridden { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal PurchaseDiscount1 { get; set; }
    public decimal PurchaseDiscount2 { get; set; }
    public decimal PurchaseDiscount3 { get; set; }
    public decimal PurchaseDiscount4 { get; set; }
    public decimal SalesDiscount1 { get; set; }
    public decimal SalesDiscount2 { get; set; }
    public decimal SalesDiscount3 { get; set; }
    public decimal SalesDiscount4 { get; set; }
    public decimal Total { get; set; }
}
