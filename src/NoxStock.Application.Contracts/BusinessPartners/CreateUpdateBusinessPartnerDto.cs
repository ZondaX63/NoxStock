using System.ComponentModel.DataAnnotations;

namespace NoxStock.BusinessPartners;

public class CreateUpdateBusinessPartnerDto
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; }
    
    [Required]
    public BusinessPartnerType Type { get; set; }
    
    [StringLength(150)]
    public string? Email { get; set; }
    
    [StringLength(50)]
    public string? Phone { get; set; }
    
    public string? Address { get; set; }
    
    [StringLength(100)]
    public string? TaxNumber { get; set; }
    
    [StringLength(100)]
    public string? TaxOffice { get; set; }
    
    public decimal Balance { get; set; }
    
    public decimal CreditLimit { get; set; }
    
    [StringLength(3)]
    public string Currency { get; set; } = "TRY";

    [Range(0, 100)] public decimal DefaultSalesDiscount1 { get; set; }
    [Range(0, 100)] public decimal DefaultSalesDiscount2 { get; set; }
    [Range(0, 100)] public decimal DefaultSalesDiscount3 { get; set; }
    [Range(0, 100)] public decimal DefaultSalesDiscount4 { get; set; }
}
