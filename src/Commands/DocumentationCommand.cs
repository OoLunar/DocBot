using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;
using OoLunar.DocBot.SymbolProviders;

namespace OoLunar.DocBot.Commands
{
    public sealed class DocumentationCommand
    {
        private readonly ISymbolProvider _symbolProvider;

        public DocumentationCommand(ISymbolProvider symbolProvider) => _symbolProvider = symbolProvider;

        [Command("documentation"), TextAlias("doc", "docs"), Description("Retrieves documentation for a given type or member.")]
        public async ValueTask ExecuteAsync(CommandContext context, [RemainingText, Description("The type or member to retrieve documentation for.")] string query)
        {
            string? documentation = _symbolProvider.GetDocumentation(query);
            if (documentation is null)
            {
                await context.RespondAsync("No documentation found.");
                return;
            }

            await context.RespondAsync(documentation);
        }
    }
}
