using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class CreateBusinessPartnerTransferDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    public DateTime TransferDate { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Description { get; set; }
}

public class BusinessPartnerTransferDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime TransferDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
