using System.ComponentModel.DataAnnotations;

namespace NexStock.Tenants;

public class RegisterTenantDto
{
    [Required]
    [StringLength(64)]
    public string TenantName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string AdminEmailAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string AdminPassword { get; set; } = string.Empty;
}
