using System;
using NoxStock.Documents;

namespace NoxStock.Dashboard;

public class DashboardDueDocumentDto
{
    public Guid Id { get; set; }
    public string DocNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = "TRY";
}
