using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class CancelBusinessPartnerOpeningBalanceDto
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
