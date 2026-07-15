using System.Threading.Tasks;

namespace NexStock.Documents;

public interface IDocumentNumberGenerator
{
    Task<string> GetNextAsync(DocumentType type);
}
