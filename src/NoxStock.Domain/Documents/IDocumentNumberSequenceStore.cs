using System;
using System.Threading.Tasks;

namespace NoxStock.Documents;
public interface IDocumentNumberSequenceStore
{
    Task<int?> TryAllocateAsync(Guid tenantId, DocumentType type, int year, string prefix);
}
