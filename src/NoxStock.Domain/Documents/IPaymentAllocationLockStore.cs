using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoxStock.Documents;

/// <summary>
/// Provider-backed aggregate locks used only while allocating or reversing a payment.
/// The implementation must acquire payment sources before documents and retain the
/// locks for the ambient unit of work transaction.
/// </summary>
public interface IPaymentAllocationLockStore
{
    Task LockAsync(Guid tenantId, IReadOnlyCollection<Guid> accountTransactionIds, IReadOnlyCollection<Guid> documentIds);
}
