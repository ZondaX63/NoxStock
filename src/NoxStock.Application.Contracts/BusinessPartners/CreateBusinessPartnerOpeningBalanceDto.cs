using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class CreateBusinessPartnerOpeningBalanceDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public BusinessPartnerOpeningDirection Direction { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime OpeningDate { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(100)]
    public string OperationKey { get; set; } = Guid.NewGuid().ToString("N");
}
