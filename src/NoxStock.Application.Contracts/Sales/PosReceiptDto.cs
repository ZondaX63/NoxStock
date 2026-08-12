using System;
using System.Collections.Generic;
using NoxStock.Documents;

namespace NoxStock.Sales;

public class PosReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public PosPaymentMethod PaymentMethod { get; set; }
    public DateTime Date { get; set; }
    public DocumentStatus Status { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Note { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TaxOffice { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<PosReceiptItemDto> Items { get; set; } = new();
}

public class PosReceiptItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SalesDiscount1 { get; set; }
    public decimal SalesDiscount2 { get; set; }
    public decimal SalesDiscount3 { get; set; }
    public decimal SalesDiscount4 { get; set; }
    public decimal EffectiveDiscountRate { get; set; }
    public decimal VatRate { get; set; }
    public decimal Total { get; set; }
}
