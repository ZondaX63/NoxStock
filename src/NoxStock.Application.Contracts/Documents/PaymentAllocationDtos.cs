using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Documents;

public class CreatePaymentAllocationDto
{
    [Required]
    public Guid AccountTransactionId { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime AllocationDate { get; set; }

    [StringLength(100)]
    public string? OperationKey { get; set; }
}

public class CreatePaymentAllocationBatchDto
{
    [Required]
    public Guid AccountTransactionId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreatePaymentAllocationLineDto> Allocations { get; set; } = [];

    [StringLength(100)]
    public string? OperationKey { get; set; }
}

public class ReversePaymentAllocationDto
{
    [StringLength(100)]
    public string? OperationKey { get; set; }
}

public class CreatePaymentAllocationLineDto
{
    [Required]
    public Guid DocumentId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime AllocationDate { get; set; }
}

public class PaymentAllocationDto : FullAuditedEntityDto<Guid>
{
    public Guid AccountTransactionId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime AllocationDate { get; set; }
    public PaymentAllocationStatus Status { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? OperationKey { get; set; }
}
