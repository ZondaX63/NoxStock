using System.ComponentModel.DataAnnotations;

namespace NoxStock.Brands;

public class CreateUpdateBrandDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; }
}
