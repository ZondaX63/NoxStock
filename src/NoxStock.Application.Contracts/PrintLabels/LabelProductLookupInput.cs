using System.ComponentModel.DataAnnotations;

namespace NoxStock.PrintLabels;

public class LabelProductLookupInput
{
    [StringLength(128)]
    public string? Filter { get; set; }

    [Range(1, 30)]
    public int MaxResultCount { get; set; } = 30;
}
