using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Products;

public class ProductGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? StockStatus { get; set; }
}
