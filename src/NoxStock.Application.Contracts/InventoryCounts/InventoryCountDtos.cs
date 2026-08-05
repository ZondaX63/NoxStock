using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using NoxStock.Products;

namespace NoxStock.InventoryCounts;

public class InventoryCountDto : FullAuditedEntityDto<Guid>
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public InventoryCountStatus Status { get; set; }
    public string CountedByName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public int CountedProductCount { get; set; }
    public decimal TotalCountedQuantity { get; set; }
    public int ZeroedProductCount { get; set; }
    public int AdjustedProductCount { get; set; }
    public int ProductsThatWillBeZeroed { get; set; }
    public List<InventoryCountItemDto> Items { get; set; } = new();
}

public class InventoryCountItemDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Difference => CountedQuantity - SystemQuantity;
    public string? Note { get; set; }
    public bool IsQuickCreatedProduct { get; set; }
}

public class InventoryCountListDto : EntityDto<Guid>
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public InventoryCountStatus Status { get; set; }
    public string CountedByName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public int CountedProductCount { get; set; }
    public decimal TotalCountedQuantity { get; set; }
    public int ZeroedProductCount { get; set; }
    public int AdjustedProductCount { get; set; }
}

public class InventoryCountGetListInput : PagedAndSortedResultRequestDto
{
    public InventoryCountStatus? Status { get; set; }
}

public class SaveInventoryCountDto
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public List<SaveInventoryCountItemDto> Items { get; set; } = new();
}

public class SaveInventoryCountItemDto
{
    public Guid ProductId { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal CountedQuantity { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class RestoreInventoryCountBackupDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public List<RestoreInventoryCountBackupItemDto> Items { get; set; } = new();
}

public class RestoreInventoryCountBackupItemDto
{
    public Guid? ProductId { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal CountedQuantity { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class CompleteInventoryCountDto
{
    public Guid Id { get; set; }
    public bool ConfirmReset { get; set; }
}

public class QuickCreateInventoryProductDto
{
    [StringLength(100)]
    public string? Barcode { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal CountedQuantity { get; set; } = 1m;

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal? SalePrice { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class QuickCreateInventoryProductResultDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "Adet";
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public string? Note { get; set; }
    public bool IsQuickCreatedProduct { get; set; }
}

public class InventoryCountImportInputDto
{
    public const int MaximumFileSize = 10 * 1024 * 1024;

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(MaximumFileSize)]
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class InventoryCountImportResultDto
{
    public int SourceRowCount { get; set; }
    public int MatchedProductCount { get; set; }
    public int SkippedRowCount { get; set; }
    public List<InventoryCountImportedItemDto> Items { get; set; } = new();
    public List<ProductImportIssueDto> Issues { get; set; } = new();
}

public class InventoryCountImportedItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
}
