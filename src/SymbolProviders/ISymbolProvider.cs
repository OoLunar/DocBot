using System.Threading.Tasks;

namespace OoLunar.DocBot.SymbolProviders
{
    public interface ISymbolProvider
    {
        string ConfigurationSectionName { get; }

        ValueTask LoadAsync();
        string? GetDocumentation(string query);
    }
}
