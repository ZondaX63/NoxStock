using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
namespace NoxStock.Web;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockBrandDtoToCreateUpdateBrandDtoMapper : MapperBase<NoxStock.Brands.BrandDto, NoxStock.Brands.CreateUpdateBrandDto>
{
    public override partial NoxStock.Brands.CreateUpdateBrandDto Map(NoxStock.Brands.BrandDto source);
    public override partial void Map(NoxStock.Brands.BrandDto source, NoxStock.Brands.CreateUpdateBrandDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockCategoryDtoToCreateUpdateCategoryDtoMapper : MapperBase<NoxStock.Categories.CategoryDto, NoxStock.Categories.CreateUpdateCategoryDto>
{
    public override partial NoxStock.Categories.CreateUpdateCategoryDto Map(NoxStock.Categories.CategoryDto source);
    public override partial void Map(NoxStock.Categories.CategoryDto source, NoxStock.Categories.CreateUpdateCategoryDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockProductDtoToCreateUpdateProductDtoMapper : MapperBase<NoxStock.Products.ProductDto, NoxStock.Products.CreateUpdateProductDto>
{
    public override partial NoxStock.Products.CreateUpdateProductDto Map(NoxStock.Products.ProductDto source);
    public override partial void Map(NoxStock.Products.ProductDto source, NoxStock.Products.CreateUpdateProductDto destination);
}
