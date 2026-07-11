using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.StockMovements;

namespace NexStock;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockBrandToBrandDtoMapper : MapperBase<NexStock.Brands.Brand, NexStock.Brands.BrandDto>
{
    public override partial NexStock.Brands.BrandDto Map(NexStock.Brands.Brand source);
    public override partial void Map(NexStock.Brands.Brand source, NexStock.Brands.BrandDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateBrandDtoToBrandMapper : MapperBase<NexStock.Brands.CreateUpdateBrandDto, NexStock.Brands.Brand>
{
    public override partial NexStock.Brands.Brand Map(NexStock.Brands.CreateUpdateBrandDto source);
    public override partial void Map(NexStock.Brands.CreateUpdateBrandDto source, NexStock.Brands.Brand destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockAccountToAccountDtoMapper : MapperBase<Account, AccountDto>
{
    public override partial AccountDto Map(Account source);
    public override partial void Map(Account source, AccountDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockAccountTransactionToAccountTransactionDtoMapper : MapperBase<AccountTransaction, AccountTransactionDto>
{
    public override partial AccountTransactionDto Map(AccountTransaction source);
    public override partial void Map(AccountTransaction source, AccountTransactionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockStockMovementToStockMovementDtoMapper : MapperBase<StockMovement, StockMovementDto>
{
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    public override partial StockMovementDto Map(StockMovement source);

    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    public override partial void Map(StockMovement source, StockMovementDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockCategoryToCategoryDtoMapper : MapperBase<NexStock.Categories.Category, NexStock.Categories.CategoryDto>
{
    public override partial NexStock.Categories.CategoryDto Map(NexStock.Categories.Category source);
    public override partial void Map(NexStock.Categories.Category source, NexStock.Categories.CategoryDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateCategoryDtoToCategoryMapper : MapperBase<NexStock.Categories.CreateUpdateCategoryDto, NexStock.Categories.Category>
{
    public override partial NexStock.Categories.Category Map(NexStock.Categories.CreateUpdateCategoryDto source);
    public override partial void Map(NexStock.Categories.CreateUpdateCategoryDto source, NexStock.Categories.Category destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockProductToProductDtoMapper : MapperBase<NexStock.Products.Product, NexStock.Products.ProductDto>
{
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.BrandName))]
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.CategoryName))]
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.OemCodes))]
    public override partial NexStock.Products.ProductDto Map(NexStock.Products.Product source);
    
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.BrandName))]
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.CategoryName))]
    [MapperIgnoreTarget(nameof(NexStock.Products.ProductDto.OemCodes))]
    public override partial void Map(NexStock.Products.Product source, NexStock.Products.ProductDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateProductDtoToProductMapper : MapperBase<NexStock.Products.CreateUpdateProductDto, NexStock.Products.Product>
{
    [MapperIgnoreTarget(nameof(NexStock.Products.Product.OemCodes))]
    public override partial NexStock.Products.Product Map(NexStock.Products.CreateUpdateProductDto source);
    [MapperIgnoreTarget(nameof(NexStock.Products.Product.OemCodes))]
    public override partial void Map(NexStock.Products.CreateUpdateProductDto source, NexStock.Products.Product destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockBusinessPartnerToBusinessPartnerDtoMapper : MapperBase<BusinessPartner, BusinessPartnerDto>
{
    public override partial BusinessPartnerDto Map(BusinessPartner source);
    public override partial void Map(BusinessPartner source, BusinessPartnerDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateBusinessPartnerDtoToBusinessPartnerMapper : MapperBase<CreateUpdateBusinessPartnerDto, BusinessPartner>
{
    public override partial BusinessPartner Map(CreateUpdateBusinessPartnerDto source);
    public override partial void Map(CreateUpdateBusinessPartnerDto source, BusinessPartner destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockDocumentToDocumentDtoMapper : MapperBase<Document, DocumentDto>
{
    [MapperIgnoreTarget(nameof(DocumentDto.BusinessPartnerName))]
    public override partial DocumentDto Map(Document source);
    [MapperIgnoreTarget(nameof(DocumentDto.BusinessPartnerName))]
    public override partial void Map(Document source, DocumentDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateDocumentDtoToDocumentMapper : MapperBase<CreateUpdateDocumentDto, Document>
{
    [MapperIgnoreTarget(nameof(Document.SourceDocumentId))]
    public override partial Document Map(CreateUpdateDocumentDto source);
    [MapperIgnoreTarget(nameof(Document.SourceDocumentId))]
    public override partial void Map(CreateUpdateDocumentDto source, Document destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NexStockDocumentItemToDocumentItemDtoMapper : MapperBase<DocumentItem, DocumentItemDto>
{
    [MapperIgnoreTarget(nameof(DocumentItemDto.ProductName))]
    public override partial DocumentItemDto Map(DocumentItem source);
    [MapperIgnoreTarget(nameof(DocumentItemDto.ProductName))]
    public override partial void Map(DocumentItem source, DocumentItemDto destination);
}

[Mapper]
public partial class NexStockCreateUpdateDocumentItemDtoToDocumentItemMapper : MapperBase<CreateUpdateDocumentItemDto, DocumentItem>
{
    public override partial DocumentItem Map(CreateUpdateDocumentItemDto source);
    public override partial void Map(CreateUpdateDocumentItemDto source, DocumentItem destination);
}
