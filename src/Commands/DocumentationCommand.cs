using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.Attributes;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.Commands
{
    public sealed class DocumentationCommand : IAutoCompleteProvider
    {
        private readonly ILogger<DocumentationCommand> _logger;
        private readonly DocumentationProvider _documentationProvider;

        public DocumentationCommand(DocumentationProvider documentationProvider, ILogger<DocumentationCommand>? logger = null)
        {
            _documentationProvider = documentationProvider;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationCommand>();
        }

        [Command("documentation"), TextAlias("doc", "docs"), Description("Retrieves documentation for a given type or member.")]
        public async Task GetDocumentationAsync(CommandContext context, [Description("Which type or member to grab documentation upon."), SlashAutoCompleteProvider<DocumentationCommand>, RemainingText] string query)
        {
            DocumentationMember? documentation = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("No query provided.");
                await context.RespondAsync("No query provided.");
            }
            else if (!int.TryParse(query, out int id) || !_documentationProvider.Members.TryGetValue(id, out documentation))
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
                await context.RespondAsync("No documentation found.");
                return;
            }

            _logger.LogDebug("Documentation found for: {Query}.", documentation.DisplayName);
            if (documentation.SourceUri.IsValueCreated)
            {
                await context.RespondAsync(documentation.Content);
                return;
            }

            // Defer
            await context.DeferResponseAsync();
            Uri? source = await documentation.SourceUri.Value;
            if (source is not null)
            {
                string[] lines = documentation.Content.Split('\n');
                lines[0] = $"## [{lines[0][3..]}](<{source}>)";
                documentation.Content = string.Join('\n', lines);
            }

            await context.EditResponseAsync(documentation.Content);
        }

        public ValueTask<Dictionary<string, object>> AutoCompleteAsync(AutoCompleteContext context)
        {
            string query = context.UserInput.ToString() ?? string.Empty;
            _logger.LogDebug("Querying documentation for: \"{Query}\"", query);

            Dictionary<string, object> choices = [];
            foreach (DocumentationMember member in _documentationProvider.Members.Values)
            {
                if (!member.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    && !member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string identifier = member.DisplayName.TrimLength(100);
                if (choices.ContainsKey(identifier))
                {
                    continue;
                }

                choices.Add(identifier, member.GetHashCode().ToString(CultureInfo.InvariantCulture));
                if (choices.Count == 10)
                {
                    break;
                }
            }

            return ValueTask.FromResult(choices);
        }
    }
}
