using System.Collections.Generic;

namespace NoxStock.Products;

public class ProductImportResultDto
{
    public int SourceRowCount { get; set; }
    public int UniqueProductCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DuplicateRowCount { get; set; }
    public int SkippedRowCount { get; set; }
    public int CreatedBrandCount { get; set; }
    public int CreatedCategoryCount { get; set; }
    public int IgnoredScientificBarcodeCount { get; set; }
    public List<ProductImportIssueDto> Issues { get; set; } = new();
}

public class ProductImportIssueDto
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
