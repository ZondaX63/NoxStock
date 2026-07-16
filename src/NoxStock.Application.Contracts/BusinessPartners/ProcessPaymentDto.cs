using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class ProcessPaymentDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    /// <summary>
    /// True for collection (tahsilat), False for payment (ödeme).
    /// </summary>
    public bool IsCollection { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    [MaxLength(500)]
    public string? Description { get; set; }
}
