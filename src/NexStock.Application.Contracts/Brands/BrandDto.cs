using System;
using Volo.Abp.Application.Dtos;

namespace NexStock.Brands;

public class BrandDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
}
