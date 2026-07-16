using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Products;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.StockMovements;
using NoxStock.ExchangeRates;
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
    public DbSet<ExchangeRate> ExchangeRates { get; set; }

    public DbSet<BusinessPartner> BusinessPartners { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentItem> DocumentItems { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountTransaction> AccountTransactions { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }

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
            b.Property(x => x.SalePrice).HasColumnType("decimal(15,4)");
            b.Property(x => x.ProfitMarkupPercent).HasColumnType("decimal(9,4)").HasDefaultValue(0m);
            b.Property(x => x.UseCurrentExchangeRateForSale).HasDefaultValue(false);
            b.Property(x => x.Quantity).HasColumnType("decimal(15,4)");
            b.Property(x => x.CriticalStockLevel).HasColumnType("decimal(15,4)");
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.PurchaseCurrency).HasMaxLength(3).HasDefaultValue("TRY");
            b.Property(x => x.SaleCurrency).HasMaxLength(3).HasDefaultValue("TRY");

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

        builder.Entity<DocumentItem>(b =>
        {
            b.ToTable(NoxStockConsts.DbTablePrefix + "DocumentItems", NoxStockConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            b.Property(x => x.VatRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.DiscountRate).HasColumnType("decimal(5,2)");
            b.Property(x => x.Total).HasColumnType("decimal(18,4)");
            b.HasOne<Document>().WithMany(d => d.Items).HasForeignKey(x => x.DocumentId).IsRequired();
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).IsRequired();
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
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).IsRequired();
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).IsRequired(false);
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.OperationKey })
                .IsUnique()
                .HasFilter("\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");
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
    }
}
