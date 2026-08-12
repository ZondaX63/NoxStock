using System.ComponentModel.DataAnnotations;

namespace NoxStock.Accounts;

public class CancelAccountTransactionDto
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
