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
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseDocumentNumber))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSupplierName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceCurrencyCode))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceNetUnitCost))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.IsSuggestedSaleAvailable))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleUnavailableReason))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleUnitPrice))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSalePriceInTry))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleRateDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentReferenceCurrencyExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentReferenceCurrencyRateDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentNetCostInTry))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.ReferenceSaleUnitPrice))]
    public override partial NoxStock.Products.ProductDto Map(NoxStock.Products.Product source);
    
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.BrandName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CategoryName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.OemCodes))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseDocumentNumber))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSupplierName))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceCurrencyCode))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceNetUnitCost))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.LastPurchaseSourceExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.IsSuggestedSaleAvailable))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleUnavailableReason))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleUnitPrice))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSalePriceInTry))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.SuggestedSaleRateDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentReferenceCurrencyExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentReferenceCurrencyRateDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.CurrentNetCostInTry))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.ProductDto.ReferenceSaleUnitPrice))]
    public override partial void Map(NoxStock.Products.Product source, NoxStock.Products.ProductDto destination);
}

[Mapper]
public partial class NoxStockCreateUpdateProductDtoToProductMapper : MapperBase<NoxStock.Products.CreateUpdateProductDto, NoxStock.Products.Product>
{
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.OemCodes))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostCurrencyCode))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostCurrencyAmount))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostTryAmount))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDocumentId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDocumentItemId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostHistoryId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseSupplierId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastCostUpdatedAt))]
    public override partial NoxStock.Products.Product Map(NoxStock.Products.CreateUpdateProductDto source);
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.OemCodes))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostCurrencyCode))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostCurrencyAmount))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostTryAmount))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseExchangeRate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDate))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDocumentId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseDocumentItemId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseCostHistoryId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastPurchaseSupplierId))]
    [MapperIgnoreTarget(nameof(NoxStock.Products.Product.LastCostUpdatedAt))]
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
    [MapperIgnoreSource(nameof(CreateUpdateDocumentItemDto.IsUnitPriceOverridden))]
    [MapperIgnoreTarget(nameof(DocumentItem.IsUnitPriceOverridden))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceExchangeRate))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceRateDate))]
    [MapperIgnoreTarget(nameof(DocumentItem.UnitPriceTry))]
    [MapperIgnoreTarget(nameof(DocumentItem.ProductCostHistoryId))]
    [MapperIgnoreTarget(nameof(DocumentItem.PricingMode))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostCurrencyAmount))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostTryAmount))]
    [MapperIgnoreTarget(nameof(DocumentItem.AppliedMarkupPercent))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceUnitPrice))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceExchangeRate))]
    [MapperIgnoreTarget(nameof(DocumentItem.PricingSnapshotDate))]
    public override partial DocumentItem Map(CreateUpdateDocumentItemDto source);

    [MapperIgnoreSource(nameof(CreateUpdateDocumentItemDto.IsUnitPriceOverridden))]
    [MapperIgnoreTarget(nameof(DocumentItem.IsUnitPriceOverridden))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceExchangeRate))]
    [MapperIgnoreTarget(nameof(DocumentItem.PriceRateDate))]
    [MapperIgnoreTarget(nameof(DocumentItem.UnitPriceTry))]
    [MapperIgnoreTarget(nameof(DocumentItem.ProductCostHistoryId))]
    [MapperIgnoreTarget(nameof(DocumentItem.PricingMode))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostCurrencyAmount))]
    [MapperIgnoreTarget(nameof(DocumentItem.CostTryAmount))]
    [MapperIgnoreTarget(nameof(DocumentItem.AppliedMarkupPercent))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceCurrencyCode))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceUnitPrice))]
    [MapperIgnoreTarget(nameof(DocumentItem.ReferenceExchangeRate))]
    [MapperIgnoreTarget(nameof(DocumentItem.PricingSnapshotDate))]
    public override partial void Map(CreateUpdateDocumentItemDto source, DocumentItem destination);
}
