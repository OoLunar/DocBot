using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.Events.Handlers
{
    public sealed class AutoCompleteEventHandler
    {
        private readonly ILogger<AutoCompleteEventHandler> _logger;
        private readonly DocumentationProvider _documentationProvider;

        public AutoCompleteEventHandler(DocumentationProvider documentationProvider, ILogger<AutoCompleteEventHandler>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(documentationProvider);

            _documentationProvider = documentationProvider;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<AutoCompleteEventHandler>();
        }

        [DiscordEvent(0)]
        public Task OnAutoCompleteResponse(DiscordClient _, InteractionCreateEventArgs eventArgs)
        {
            if (eventArgs.Interaction.Type != InteractionType.AutoComplete || eventArgs.Interaction.Data.Name != "documentation")
            {
                return Task.CompletedTask;
            }

            DiscordInteractionDataOption focusedOption = eventArgs.Interaction.Data.Options.First(option => option.Focused);
            string query = focusedOption.Value.ToString() ?? string.Empty;

            _logger.LogDebug("Querying documentation for: \"{Query}\"", query);

            List<DiscordAutoCompleteChoice> choices = new(10);
            foreach (string choice in _documentationProvider.Members.Keys)
            {
                if (!choice.StartsWith(query, StringComparison.OrdinalIgnoreCase) && !choice.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                choices.Add(new DiscordAutoCompleteChoice(choice.TrimLength(100), choice.TrimLength(100)));
                if (choices.Count == 10)
                {
                    break;
                }
            }

            return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.AutoCompleteResult,
                new DiscordInteractionResponseBuilder().AddAutoCompleteChoices(choices));
        }
    }
}
