using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OoLunar.DocBot.Events;

namespace OoLunar.DocBot.Commands
{
    public sealed class DocumentationCommand
    {
        private readonly ILogger<DocumentationCommand> _logger;
        private readonly DocumentationProvider _documentationProvider;

        public DocumentationCommand(DocumentationProvider documentationProvider, ILogger<DocumentationCommand>? logger = null)
        {
            _documentationProvider = documentationProvider;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationCommand>();
        }

        [DiscordEvent(0)]
        public Task GetDocumentationAsync(DiscordClient _, InteractionCreateEventArgs eventArgs)
        {
            if (eventArgs.Interaction.Type != InteractionType.ApplicationCommand || eventArgs.Interaction.Data.Name != "documentation")
            {
                return Task.CompletedTask;
            }

            string query = eventArgs.Interaction.Data.Options.First().Value.ToString() ?? string.Empty;
            _logger.LogDebug("Querying documentation for {Query}.", query);

            if (!_documentationProvider.Members.TryGetValue(query, out DiscordEmbedBuilder? documentation))
            {
                _logger.LogDebug("No documentation found for {Query}.", query);
                return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("No documentation found."));
            }

            _logger.LogDebug("Documentation found for {Query}.", query);
            return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(documentation));
        }
    }
}
