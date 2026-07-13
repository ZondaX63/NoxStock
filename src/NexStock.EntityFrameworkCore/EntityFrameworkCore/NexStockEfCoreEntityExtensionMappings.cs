using Microsoft.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;
using Volo.Abp.TenantManagement;
using NexStock.MultiTenancy;

namespace NexStock.EntityFrameworkCore;

public static class NexStockEfCoreEntityExtensionMappings
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        NexStockGlobalFeatureConfigurator.Configure();
        NexStockModuleExtensionConfigurator.Configure();

        OneTimeRunner.Run(() =>
        {
                ObjectExtensionManager.Instance
                    .MapEfCoreProperty<Tenant, bool>(
                        NexStockTenantProperties.IsActive,
                        (_, propertyBuilder) => propertyBuilder
                            .HasDefaultValue(true)
                            .HasSentinel(true));

                /* You can configure extra properties for the
                 * entities defined in the modules used by your application.
                 *
                 * This class can be used to map these extra properties to table fields in the database.
                 *
                 * USE THIS CLASS ONLY TO CONFIGURE EF CORE RELATED MAPPING.
                 * USE NexStockModuleExtensionConfigurator CLASS (in the Domain.Shared project)
                 * FOR A HIGH LEVEL API TO DEFINE EXTRA PROPERTIES TO ENTITIES OF THE USED MODULES
                 *
                 * Example: Map a property to a table field:

                     ObjectExtensionManager.Instance
                         .MapEfCoreProperty<IdentityUser, string>(
                             "MyProperty",
                             (entityBuilder, propertyBuilder) =>
                             {
                                 propertyBuilder.HasMaxLength(128);
                             }
                         );

                 * See the documentation for more:
                 * https://docs.abp.io/en/abp/latest/Customizing-Application-Modules-Extending-Entities
                 */
        });
    }
}
