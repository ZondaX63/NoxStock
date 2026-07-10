using System;
using NexStock.Documents;

namespace NexStock.Dashboard;

public class DashboardDocumentSummaryDto
{
    public Guid Id { get; set; }
    public string DocNumber { get; set; } = string.Empty;
    public string? BusinessPartnerName { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime Date { get; set; }
}

