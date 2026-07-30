using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Products;

public class ProductGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? StockStatus { get; set; }
    public string? PricingMode { get; set; }
    public string? CostStatus { get; set; }
    /// <summary>
    /// Currency used for the server-calculated list price preview.
    /// This is a presentation preference only; it never changes a product price.
    /// </summary>
}
