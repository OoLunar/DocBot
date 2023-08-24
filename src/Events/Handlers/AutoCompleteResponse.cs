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
            foreach (DocumentationMember member in _documentationProvider.Members.Values)
            {
                if (!member.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase) && !member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                choices.Add(new DiscordAutoCompleteChoice(member.DisplayName.TrimLength(100), member.GetHashCode().ToString()));
                if (choices.Count == 10)
                {
                    break;
                }
            }

            return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.AutoCompleteResult, new DiscordInteractionResponseBuilder().AddAutoCompleteChoices(choices.OrderByDescending(choice => choice.Name == query).ThenByDescending(choice => choice.Name.StartsWith(query)).ThenByDescending(choice => query.EndsWith(choice.Name)).ThenBy(choice => choice.Name.Length)));
        }
    }
}
