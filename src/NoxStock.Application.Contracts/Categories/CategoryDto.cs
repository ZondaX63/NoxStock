using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Categories;

public class CategoryDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
}
