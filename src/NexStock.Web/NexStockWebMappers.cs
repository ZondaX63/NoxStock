using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
namespace NexStock.Web;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockBrandDtoToCreateUpdateBrandDtoMapper : MapperBase<NexStock.Brands.BrandDto, NexStock.Brands.CreateUpdateBrandDto>
{
    public override partial NexStock.Brands.CreateUpdateBrandDto Map(NexStock.Brands.BrandDto source);
    public override partial void Map(NexStock.Brands.BrandDto source, NexStock.Brands.CreateUpdateBrandDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockCategoryDtoToCreateUpdateCategoryDtoMapper : MapperBase<NexStock.Categories.CategoryDto, NexStock.Categories.CreateUpdateCategoryDto>
{
    public override partial NexStock.Categories.CreateUpdateCategoryDto Map(NexStock.Categories.CategoryDto source);
    public override partial void Map(NexStock.Categories.CategoryDto source, NexStock.Categories.CreateUpdateCategoryDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockProductDtoToCreateUpdateProductDtoMapper : MapperBase<NexStock.Products.ProductDto, NexStock.Products.CreateUpdateProductDto>
{
    public override partial NexStock.Products.CreateUpdateProductDto Map(NexStock.Products.ProductDto source);
    public override partial void Map(NexStock.Products.ProductDto source, NexStock.Products.CreateUpdateProductDto destination);
}
