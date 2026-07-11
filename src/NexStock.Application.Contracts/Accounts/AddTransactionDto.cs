using System;
using System.ComponentModel.DataAnnotations;

namespace NexStock.Accounts;

public class AddTransactionDto
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public AccountTransactionType Type { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    [MaxLength(500)]
    public string? Description { get; set; }
}
