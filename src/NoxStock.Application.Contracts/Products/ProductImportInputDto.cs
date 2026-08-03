using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Products;

public class ProductImportInputDto
{
    public const int MaximumFileSize = 10 * 1024 * 1024;

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(MaximumFileSize)]
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
