using System.Threading.Tasks;

namespace NoxStock.Documents;

public interface IDocumentNumberGenerator
{
    Task<string> GetNextAsync(DocumentType type);
}
