using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Licensing;

public class LicensePlanDto : FullAuditedEntityDto<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsCustom { get; set; }
}
