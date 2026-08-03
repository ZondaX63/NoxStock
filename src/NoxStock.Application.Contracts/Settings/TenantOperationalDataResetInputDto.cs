using System.ComponentModel.DataAnnotations;

namespace NoxStock.Settings;

public class TenantOperationalDataResetInputDto
{
    public const string ConfirmationText = "SIFIRLA";

    [Required]
    [StringLength(16)]
    public string Confirmation { get; set; } = string.Empty;
}
