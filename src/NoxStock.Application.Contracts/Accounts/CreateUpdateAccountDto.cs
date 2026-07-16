using System.ComponentModel.DataAnnotations;

namespace NoxStock.Accounts;

public class CreateUpdateAccountDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Cash;

    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "TRY";

    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
}

