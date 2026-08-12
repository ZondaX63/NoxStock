using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class CancelBusinessPartnerPaymentDto
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
