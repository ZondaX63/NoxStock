using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Brands;

public class BrandDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
}
