using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.BusinessPartners;

public class BusinessPartnerDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public BusinessPartnerType Type { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public string Currency { get; set; }
    public decimal DefaultSalesDiscount1 { get; set; }
    public decimal DefaultSalesDiscount2 { get; set; }
    public decimal DefaultSalesDiscount3 { get; set; }
    public decimal DefaultSalesDiscount4 { get; set; }
}
