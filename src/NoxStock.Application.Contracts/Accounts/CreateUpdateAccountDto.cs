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

    /// <summary>
    /// Eski istemcilerle geriye uyumluluk için korunur.
    /// Sunucu bu değeri hesap oluşturma veya güncelleme sırasında uygulamaz.
    /// Açılış bakiyesi için <see cref="CreateAccountWithOpeningDto"/> veya
    /// <see cref="CreateAccountOpeningBalanceDto"/> kullanılmalıdır.
    /// </summary>
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
}
