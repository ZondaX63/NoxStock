using System;
using NexStock.Documents;

namespace NexStock.Sales;

public class PosSaleDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public PosPaymentMethod PaymentMethod { get; set; }
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime Date { get; set; }
    public DocumentStatus Status { get; set; }
}
