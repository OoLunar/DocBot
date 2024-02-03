using System;
using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands.Attributes;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;
using OoLunar.DocBot.ContextChecks;

namespace OoLunar.DocBot.Commands
{
    public sealed class ReloadCommand
    {
        private readonly DocumentationProvider _documentationProvider;

        public ReloadCommand(DocumentationProvider documentationProvider) => _documentationProvider = documentationProvider ?? throw new ArgumentNullException(nameof(documentationProvider));

        [Command("reload"), TextAlias("restart"), Description("Reloads the bot documentation"), RequireOwnerOrSelfBot, SlashCommandTypes(ApplicationCommandType.SlashCommand)]
        public async ValueTask ExecuteAsync(CommandContext context)
        {
            await context.DeferResponseAsync();
            await _documentationProvider.ReloadAsync();
            await context.RespondAsync("Documentation reloaded.");
        }
    }
}
