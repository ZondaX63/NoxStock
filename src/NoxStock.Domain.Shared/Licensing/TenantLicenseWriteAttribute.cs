using System;

namespace NoxStock.Licensing;

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class TenantLicenseWriteAttribute : Attribute
{
}
