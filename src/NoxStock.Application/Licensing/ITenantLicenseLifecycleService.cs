using System.Threading.Tasks;

namespace NoxStock.Licensing;

public interface ITenantLicenseLifecycleService
{
    Task ProcessAsync();
}
