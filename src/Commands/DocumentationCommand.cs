using System;
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
        public async Task GetDocumentationAsync(DiscordClient _, InteractionCreateEventArgs eventArgs)
        {
            if (eventArgs.Interaction.Type != InteractionType.ApplicationCommand || eventArgs.Interaction.Data.Name != "documentation")
            {
                return;
            }

            DocumentationMember? documentation = null;
            string query = eventArgs.Interaction.Data.Options.First().Value.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("No query provided.");
                await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("No query provided."));
            }
            else if (!Ulid.TryParse(query, out Ulid id) || !_documentationProvider.Members.TryGetValue(id, out documentation))
            {
                foreach (DocumentationMember member in _documentationProvider.Members.Values)
                {
                    if (member.FullName.Equals(query, StringComparison.OrdinalIgnoreCase))
                    {
                        documentation = member;
                        break;
                    }
                    else if (member.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase))
                    {
                        documentation = member;
                        break;
                    }
                    else if (member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        documentation = member;
                    }
                }
            }

            if (documentation is null)
            {
                _logger.LogDebug("No documentation found for: {Query}.", query);
                await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("No documentation found."));
                return;
            }

            _logger.LogDebug("Documentation found for: {Query}.", documentation.DisplayName);
            if (!documentation.SourceUri.IsValueCreated)
            {
                Uri? source = await documentation.SourceUri.Value;
                if (source is not null)
                {
                    string[] lines = documentation.Content.Split('\n');
                    lines[0] = $"## [{lines[0][3..]}](<{source}>)";
                    documentation.Content = string.Join('\n', lines);
                }
            }

            await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(documentation.Content));
        }
    }
}
