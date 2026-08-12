using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Products;
using NoxStock.BusinessPartners;
using NoxStock.BusinessPartnerTransfers;
using NoxStock.Documents;
using NoxStock.StockMovements;
using NoxStock.ExchangeRates;
using NoxStock.Licensing;
using NoxStock.InventoryCounts;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace NoxStock.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class NoxStockDbContext :
    AbpDbContext<NoxStockDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    public DbSet<Brand> Brands { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductOEMCode> ProductOEMCodes { get; set; }
    public DbSet<ProductCostHistory> ProductCostHistories { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }

    public DbSet<BusinessPartner> BusinessPartners { get; set; }
    public DbSet<BusinessPartnerOpeningBalance> BusinessPartnerOpeningBalances { get; set; }
    public DbSet<BusinessPartnerTransfer> BusinessPartnerTransfers { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentItem> DocumentItems { get; set; }
    public DbSet<DocumentExchangeRateSnapshot> DocumentExchangeRateSnapshots { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountTransaction> AccountTransactions { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<InventoryCount> InventoryCounts { get; set; }
    public DbSet<InventoryCountItem> InventoryCountItems { get; set; }
    public DbSet<LicensePlan> LicensePlans { get; set; }
    public DbSet<TenantLicense> TenantLicenses { get; set; }
    public DbSet<TenantLicenseFeature> TenantLicenseFeatures { get; set; }
    public DbSet<TenantLicenseLimit> TenantLicenseLimits { get; set; }
    public DbSet<TenantLicenseAuditLog> TenantLicenseAuditLogs { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public NoxStockDbContext(DbContextOptions<NoxStockDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();

        /* Configure your own tables/entities inside here */

        builder.Entity<Brand>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "Brands", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        });

        builder.Entity<Category>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "Categories", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).IsRequired(false);
        });

        builder.Entity<Product>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "Products", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Sku).IsRequired().HasMaxLength(100);
            b.Property(x => x.Barcode).HasMaxLength(100);
            b.Property(x => x.Unit).HasMaxLength(50).HasDefaultValue("Adet");
            b.Property(x => x.ShelfLocation).HasMaxLength(100);
            b.Property(x => x.PurchasePrice).HasColumnType("decimal(15,4)");
            b.Property(x => x.PurchaseListPrice).HasColumnType("decimal(18,6)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount1).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount2).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount3).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount4).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.ExchangeRateAtPurchase).HasColumnType("decimal(18,6)").HasDefaultValue(1m);
            b.Property(x => x.SalePrice).HasColumnType("decimal(15,4)");
            b.Property(x => x.SalesDiscount1).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount2).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount3).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount4).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.VatRate).HasDefaultValue(20);
            b.Property(x => x.ProfitMarkupPercent).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.UseCurrentExchangeRateForSale).HasDefaultValue(false);
            b.Property(x => x.PricingMode).HasDefaultValue(ProductPricingMode.Manual);
            b.Property(x => x.LastPurchaseCostCurrencyCode).HasMaxLength(3);
            b.Property(x => x.LastPurchaseCostCurrencyAmount).HasColumnType("decimal(18,6)");
            b.Property(x => x.LastPurchaseCostTryAmount).HasColumnType("decimal(18,6)");
            b.Property(x => x.AverageInventoryCostTry).HasColumnType("decimal(18,2)");
            b.Property(x => x.LastPurchaseExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(x => x.Quantity).HasColumnType("decimal(15,4)");
            b.Property(x => x.CriticalStockLevel).HasColumnType("decimal(15,4)");
            b.Property(x => x.PurchaseCurrency).IsRequired().HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.IsIncompleteProfile).HasDefaultValue(false);

            b.HasIndex(x => new { x.TenantId, x.Sku });
            b.HasIndex(x => new { x.TenantId, x.Barcode });

            b.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).IsRequired(false);
            b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).IsRequired(false);
        });

        builder.Entity<ProductOEMCode>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "ProductOEMCodes", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.OemCode).IsRequired().HasMaxLength(150);
            b.Property(x => x.SearchCode).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.SearchCode });
            b.HasOne<Product>().WithMany(p => p.OemCodes).HasForeignKey(x => x.ProductId).IsRequired();
        });

        builder.Entity<ProductCostHistory>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "ProductCostHistories", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.SourceCurrencyCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.SourceNetUnitCost).HasColumnType("decimal(18,6)");
            b.Property(x => x.SourceExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(x => x.TryEquivalentNetUnitCost).HasColumnType("decimal(18,6)");
            b.Property(x => x.ReferenceCurrencyCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.ReferenceCurrencyEquivalentCost).HasColumnType("decimal(18,6)");
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.QuantityBefore).HasColumnType("decimal(18,4)");
            b.Property(x => x.AverageCostBeforeTry).HasColumnType("decimal(18,2)");
            b.Property(x => x.QuantityAfter).HasColumnType("decimal(18,4)");
            b.Property(x => x.AverageCostAfterTry).HasColumnType("decimal(18,2)");
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).IsRequired();
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.SourceDocumentId).IsRequired();
            b.HasOne<DocumentItem>().WithMany().HasForeignKey(x => x.SourceDocumentItemId).IsRequired();
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.SupplierId).IsRequired(false);
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.CostDate });
            b.HasIndex(x => new { x.TenantId, x.SourceDocumentItemId, x.ChangeType }).IsUnique();
        });

        builder.Entity<BusinessPartner>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "BusinessPartners", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(255);
            b.Property(x => x.Email).HasMaxLength(150);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.TaxNumber).HasMaxLength(100);
            b.Property(x => x.TaxOffice).HasMaxLength(100);
            b.Property(x => x.Balance).HasColumnType("decimal(18,4)");
            b.Property(x => x.CreditLimit).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.DefaultSalesDiscount1).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.DefaultSalesDiscount2).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.DefaultSalesDiscount3).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.DefaultSalesDiscount4).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
        });

        builder.Entity<BusinessPartnerOpeningBalance>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "BusinessPartnerOpeningBalances", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.OperationKey).IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.CancellationReason).HasMaxLength(500);
            b.Property(x => x.Status).HasDefaultValue(BusinessPartnerOpeningStatus.Active);
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BusinessPartnerOpeningBalance>().WithMany().HasForeignKey(x => x.ReversalOfOpeningBalanceId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.OperationKey }).IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            b.HasIndex(x => new { x.TenantId, x.BusinessPartnerId }).IsUnique()
                .HasFilter("\"Status\" = 1 AND \"IsDeleted\" = FALSE");
        });

        builder.Entity<BusinessPartnerTransfer>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "BusinessPartnerTransfers", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReferenceNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.ReferenceNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.TransferDate });
            b.HasIndex(x => new { x.TenantId, x.SupplierId, x.TransferDate });
        });

        builder.Entity<Document>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "Documents", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DocNumber).IsRequired().HasMaxLength(100);
            b.Property(x => x.Subtotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.VatTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.DiscountTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.GrandTotal).HasColumnType("decimal(18,4)");
            b.Property(x => x.PaidAmount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.OperationKey).HasMaxLength(100);
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.BusinessPartnerId).IsRequired();
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.SourceDocumentId).IsRequired(false);
            b.HasIndex(x => x.SourceDocumentId);
            b.HasIndex(x => new { x.TenantId, x.Type, x.DocNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Type, x.OperationKey })
                .IsUnique()
                .HasFilter("\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        });

        builder.Entity<DocumentExchangeRateSnapshot>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "DocumentExchangeRateSnapshots", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.RateToTry).HasColumnType("decimal(18,6)");
            b.HasOne<Document>().WithMany(d => d.ExchangeRateSnapshots).HasForeignKey(x => x.DocumentId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.CurrencyCode })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        builder.Entity<DocumentItem>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "DocumentItems", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.VatRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.DiscountRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.PurchaseDiscount1).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount2).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount3).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.PurchaseDiscount4).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount1).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount2).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount3).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.SalesDiscount4).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.Total).HasColumnType("decimal(18,4)");
            b.Property(x => x.PriceCurrencyCode).HasMaxLength(3);
            b.Property(x => x.PriceExchangeRate).HasColumnType("decimal(18,6)");
            b.Property(x => x.UnitPriceTry).HasColumnType("decimal(18,6)");
            b.Property(x => x.CostCurrencyCode).HasMaxLength(3);
            b.Property(x => x.CostCurrencyAmount).HasColumnType("decimal(18,6)");
            b.Property(x => x.CostTryAmount).HasColumnType("decimal(18,6)");
            b.Property(x => x.TotalCostTryAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AppliedMarkupPercent).HasColumnType("decimal(9,4)");
            b.Property(x => x.ReferenceCurrencyCode).HasMaxLength(3);
            b.Property(x => x.ReferenceUnitPrice).HasColumnType("decimal(18,6)");
            b.Property(x => x.ReferenceExchangeRate).HasColumnType("decimal(18,6)");
            b.HasOne<Document>().WithMany(d => d.Items).HasForeignKey(x => x.DocumentId).IsRequired();
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).IsRequired();
            b.HasOne<ProductCostHistory>().WithMany().HasForeignKey(x => x.ProductCostHistoryId).IsRequired(false);
        });

        builder.Entity<ExchangeRate>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "ExchangeRates", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Rate).HasColumnType("decimal(18,6)");
            b.Property(x => x.RateDate).IsRequired();
            b.HasIndex(x => x.Currency).IsUnique();
        });

        builder.Entity<Account>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "Accounts", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.Balance).HasColumnType("decimal(18,4)");
            b.Property(x => x.IsActive).HasDefaultValue(true);
        });

        builder.Entity<AccountTransaction>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "AccountTransactions", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.OperationKey).HasMaxLength(100);
            b.Property(x => x.CancellationReason).HasMaxLength(500);
            b.Property(x => x.PaymentMethod).HasMaxLength(30);
            b.Property(x => x.ReferenceNumber).HasMaxLength(100);
            b.Property(x => x.Status).HasDefaultValue(AccountTransactionStatus.Active);
            b.Property(x => x.BalanceAfter).HasColumnType("decimal(18,4)");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).IsRequired(false);
            b.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.BusinessPartnerId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.RelatedAccountId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccountTransaction>().WithMany().HasForeignKey(x => x.ReversalOfTransactionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.OperationKey })
                .IsUnique()
                .HasFilter("\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");
            b.HasIndex(x => new { x.TenantId, x.OperationKey, x.Type })
                .IsUnique()
                .HasFilter("\"OperationKey\" IS NOT NULL AND \"DocumentId\" IS NULL AND \"IsDeleted\" = FALSE");
            b.HasIndex(x => new { x.TenantId, x.AccountId })
                .IsUnique()
                .HasFilter("\"Type\" = 1 AND \"Status\" = 1 AND \"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        });

        builder.Entity<StockMovement>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "StockMovements", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.BalanceAfter).HasColumnType("decimal(18,4)");
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).IsRequired();
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).IsRequired(false);
        });

        builder.Entity<InventoryCount>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "InventoryCounts", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ReferenceNumber).IsRequired().HasMaxLength(40);
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.CountedByName).IsRequired().HasMaxLength(150);
            b.Property(x => x.CompletedByName).HasMaxLength(150);
            b.Property(x => x.TotalCountedQuantity).HasColumnType("decimal(18,4)");
            b.HasIndex(x => new { x.TenantId, x.ReferenceNumber })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            b.HasIndex(x => new { x.TenantId, x.Status, x.CountDate });
            b.HasIndex(x => x.TenantId)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE AND \"Status\" IN (1, 2)");
        });

        builder.Entity<InventoryCountItem>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "InventoryCountItems", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ProductName).IsRequired().HasMaxLength(150);
            b.Property(x => x.Sku).IsRequired().HasMaxLength(100);
            b.Property(x => x.Barcode).HasMaxLength(100);
            b.Property(x => x.Unit).IsRequired().HasMaxLength(50);
            b.Property(x => x.SystemQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.CountedQuantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasOne<InventoryCount>().WithMany(x => x.Items)
                .HasForeignKey(x => x.InventoryCountId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne<Product>().WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasIndex(x => new { x.TenantId, x.InventoryCountId, x.ProductId })
                .IsUnique();
        });

        builder.Entity<LicensePlan>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "LicensePlans", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => x.Code)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        builder.Entity<TenantLicense>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "TenantLicenses", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.LicenseReference).IsRequired().HasMaxLength(100);
            b.Property(x => x.InternalNote).HasMaxLength(1000);
            b.Property(x => x.SuspensionReason).HasMaxLength(500);
            b.HasOne<LicensePlan>()
                .WithMany()
                .HasForeignKey(x => x.LicensePlanId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasIndex(x => x.LicenseReference)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            b.HasIndex(x => x.TenantId)
                .IsUnique()
                .HasFilter("\"IsCurrent\" = TRUE AND \"IsDeleted\" = FALSE");
            b.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        });

        builder.Entity<TenantLicenseFeature>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "TenantLicenseFeatures", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.FeatureName).IsRequired().HasMaxLength(64);
            b.HasOne<TenantLicense>()
                .WithMany()
                .HasForeignKey(x => x.TenantLicenseId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasIndex(x => new { x.TenantLicenseId, x.FeatureName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        builder.Entity<TenantLicenseLimit>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "TenantLicenseLimits", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.LimitName).IsRequired().HasMaxLength(64);
            b.HasOne<TenantLicense>()
                .WithMany()
                .HasForeignKey(x => x.TenantLicenseId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasIndex(x => new { x.TenantLicenseId, x.LimitName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        builder.Entity<TenantLicenseAuditLog>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "TenantLicenseAuditLogs", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Action).IsRequired().HasMaxLength(64);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasOne<TenantLicense>()
                .WithMany()
                .HasForeignKey(x => x.TenantLicenseId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CreationTime });
        });
    }
}
