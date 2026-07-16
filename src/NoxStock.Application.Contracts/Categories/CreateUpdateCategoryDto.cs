using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Categories;

public class CreateUpdateCategoryDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; }
    
    public Guid? ParentId { get; set; }
}
