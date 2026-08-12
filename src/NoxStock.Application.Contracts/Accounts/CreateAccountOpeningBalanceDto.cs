using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Accounts;

/// <summary>
/// Var olan aktif bir hesaba tek seferlik açılış hareketi ekler.
/// Kullanıcı gelir/gider hareket türü seçmez; para birimi daima hesabın para birimidir.
/// </summary>
public class CreateAccountOpeningBalanceDto
{
    [Required]
    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public DateTime OpeningDate { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? OperationKey { get; set; } = Guid.NewGuid().ToString("N");
}
