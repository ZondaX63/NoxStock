using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Licensing;

public class TenantLicenseWriteInterceptor : AbpInterceptor, ITransientDependency
{
    private readonly ITenantLicenseAccessChecker _accessChecker;
    private readonly ICurrentTenant _currentTenant;
    private readonly IIdentityUserRepository _identityUserRepository;

    public TenantLicenseWriteInterceptor(
        ITenantLicenseAccessChecker accessChecker,
        ICurrentTenant currentTenant,
        IIdentityUserRepository identityUserRepository)
    {
        _accessChecker = accessChecker;
        _currentTenant = currentTenant;
        _identityUserRepository = identityUserRepository;
    }

    public override async Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        if (_currentTenant.Id.HasValue && IsIdentityUserCreation(invocation))
        {
            await _accessChecker.EnsureCanWriteAsync();
            await _accessChecker.EnsureLimitAsync(
                LicenseLimitNames.MaxUsers,
                await _identityUserRepository.GetCountAsync());
        }
        else if (_currentTenant.Id.HasValue && RequiresLicenseWrite(invocation))
        {
            await _accessChecker.EnsureCanWriteAsync();
        }

        await invocation.ProceedAsync();
    }

    private static bool IsIdentityUserCreation(IAbpMethodInvocation invocation)
    {
        return invocation.Method.Name == nameof(IIdentityUserAppService.CreateAsync) &&
               invocation.TargetObject is IIdentityUserAppService;
    }

    private static bool RequiresLicenseWrite(IAbpMethodInvocation invocation)
    {
        if (invocation.Method.IsDefined(typeof(TenantLicenseWriteAttribute), inherit: true))
        {
            return true;
        }

        if (invocation.TargetObject is null)
        {
            return false;
        }

        var parameterTypes = invocation.Method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var implementationMethod = invocation.TargetObject.GetType().GetMethod(
            invocation.Method.Name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null);

        return implementationMethod?.IsDefined(
            typeof(TenantLicenseWriteAttribute),
            inherit: true) == true;
    }
}
