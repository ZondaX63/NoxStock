using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Accounts;

/// <summary>
/// Hesap tanımını ve isteğe bağlı açılış bakiyesini tek ticari işlemde oluşturur.
/// </summary>
public class CreateAccountWithOpeningDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Cash;

    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "TRY";

    public bool IsActive { get; set; } = true;

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal OpeningAmount { get; set; }

    public DateTime OpeningDate { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? OpeningDescription { get; set; }

    [StringLength(100)]
    public string? OperationKey { get; set; } = Guid.NewGuid().ToString("N");
}
