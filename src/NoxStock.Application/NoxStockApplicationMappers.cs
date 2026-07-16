using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.StockMovements;

namespace NoxStock;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockBrandToBrandDtoMapper : MapperBase<NoxStock.Brands.Brand, NoxStock.Brands.BrandDto>
{
    public override partial NoxStock.Brands.BrandDto Map(NoxStock.Brands.Brand source);
    public override partial void Map(NoxStock.Brands.Brand source, NoxStock.Brands.BrandDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateBrandDtoToBrandMapper : MapperBase<NoxStock.Brands.CreateUpdateBrandDto, NoxStock.Brands.Brand>
{
    public override partial NoxStock.Brands.Brand Map(NoxStock.Brands.CreateUpdateBrandDto source);
    public override partial void Map(NoxStock.Brands.CreateUpdateBrandDto source, NoxStock.Brands.Brand destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockAccountToAccountDtoMapper : MapperBase<Account, AccountDto>
{
    public override partial AccountDto Map(Account source);
    public override partial void Map(Account source, AccountDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockAccountTransactionToAccountTransactionDtoMapper : MapperBase<AccountTransaction, AccountTransactionDto>
{
    public override partial AccountTransactionDto Map(AccountTransaction source);
    public override partial void Map(AccountTransaction source, AccountTransactionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockStockMovementToStockMovementDtoMapper : MapperBase<StockMovement, StockMovementDto>
{
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    public override partial StockMovementDto Map(StockMovement source);

    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    public override partial void Map(StockMovement source, StockMovementDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockCategoryToCategoryDtoMapper : MapperBase<NoxStock.Categories.Category, NoxStock.Categories.CategoryDto>
{
    public override partial NoxStock.Categories.CategoryDto Map(NoxStock.Categories.Category source);
    public override partial void Map(NoxStock.Categories.Category source, NoxStock.Categories.CategoryDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateCategoryDtoToCategoryMapper : MapperBase<NoxStock.Categories.CreateUpdateCategoryDto, NoxStock.Categories.Category>
{
    public override partial NoxStock.Categories.Category Map(NoxStock.Categories.CreateUpdateCategoryDto source);
    public override partial void Map(NoxStock.Categories.CreateUpdateCategoryDto source, NoxStock.Categories.Category destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockProductToProductDtoMapper : MapperBase<NoxStock.Products.Product, NoxStock.Products.ProductDto>
{
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.BrandName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CategoryName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.OemCodes))]
    public override partial NoxStock.Products.ProductDto Map(NoxStock.Products.Product source);
    
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.BrandName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CategoryName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.OemCodes))]
    public override partial void Map(NoxStock.Products.Product source, NoxStock.Products.ProductDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateProductDtoToProductMapper : MapperBase<NoxStock.Products.CreateUpdateProductDto, NoxStock.Products.Product>
{
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.OemCodes))]
    public override partial NoxStock.Products.Product Map(NoxStock.Products.CreateUpdateProductDto source);
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.OemCodes))]
    public override partial void Map(NoxStock.Products.CreateUpdateProductDto source, NoxStock.Products.Product destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockBusinessPartnerToBusinessPartnerDtoMapper : MapperBase<BusinessPartner, BusinessPartnerDto>
{
    public override partial BusinessPartnerDto Map(BusinessPartner source);
    public override partial void Map(BusinessPartner source, BusinessPartnerDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateBusinessPartnerDtoToBusinessPartnerMapper : MapperBase<CreateUpdateBusinessPartnerDto, BusinessPartner>
{
    public override partial BusinessPartner Map(CreateUpdateBusinessPartnerDto source);
    public override partial void Map(CreateUpdateBusinessPartnerDto source, BusinessPartner destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockDocumentToDocumentDtoMapper : MapperBase<Document, DocumentDto>
{
    [MapperIgnoreTarget(nameof(DocumentDto.BusinessPartnerName))]
    public override partial DocumentDto Map(Document source);
    [MapperIgnoreTarget(nameof(DocumentDto.BusinessPartnerName))]
    public override partial void Map(Document source, DocumentDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateDocumentDtoToDocumentMapper : MapperBase<CreateUpdateDocumentDto, Document>
{
    [MapperIgnoreTarget(nameof(Document.SourceDocumentId))]
    [MapperIgnoreTarget(nameof(Document.OperationKey))]
    public override partial Document Map(CreateUpdateDocumentDto source);
    [MapperIgnoreTarget(nameof(Document.SourceDocumentId))]
    [MapperIgnoreTarget(nameof(Document.OperationKey))]
    public override partial void Map(CreateUpdateDocumentDto source, Document destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NoxStockDocumentItemToDocumentItemDtoMapper : MapperBase<DocumentItem, DocumentItemDto>
{
    [MapperIgnoreTarget(nameof(DocumentItemDto.ProductName))]
    public override partial DocumentItemDto Map(DocumentItem source);
    [MapperIgnoreTarget(nameof(DocumentItemDto.ProductName))]
    public override partial void Map(DocumentItem source, DocumentItemDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateDocumentItemDtoToDocumentItemMapper : MapperBase<CreateUpdateDocumentItemDto, DocumentItem>
{
    public override partial DocumentItem Map(CreateUpdateDocumentItemDto source);
    public override partial void Map(CreateUpdateDocumentItemDto source, DocumentItem destination);
}
