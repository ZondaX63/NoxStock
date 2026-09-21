using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoxStock.Documents;

public interface ICommercialCommandLockStore
{
    Task<CommercialCommand?> LockForExecutionAsync(Guid id);
    Task<List<CommercialCommand>> LockStaleAsync(DateTime now, int maxCount);
    Task<bool> TryCompleteAsync(Guid id, Guid? tenantId, Guid executionToken, CommercialCommandStatus status, DateTime completedAt, string? failureCode);
    Task<bool> TryExpireAsync(Guid id, Guid? tenantId, Guid executionToken, DateTime now);
}
