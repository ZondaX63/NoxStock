using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Products;

public class ProductLookupInput : PagedResultRequestDto
{
    public string? Filter { get; set; }
    public List<Guid>? Ids { get; set; }
}
