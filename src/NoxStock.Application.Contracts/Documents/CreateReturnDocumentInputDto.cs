using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Documents;

public class CreateReturnDocumentInputDto
{
    [Required] public Guid SourceDocumentId { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    [MinLength(1)] public List<CreateReturnDocumentItemInputDto> Items { get; set; } = [];
}

public class CreateReturnDocumentItemInputDto
{
    [Required] public Guid SourceDocumentItemId { get; set; }
    [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
}
