using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Accounts;

public class TransferDto
{
    [Required]
    public Guid FromAccountId { get; set; }

    [Required]
    public Guid ToAccountId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? OperationKey { get; set; } = Guid.NewGuid().ToString("N");
}
