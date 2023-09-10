using System;
using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus.CommandAll.Attributes;
using DSharpPlus.CommandAll.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.Commands
{
    public sealed class DocumentationCommand : BaseCommand
    {
        private readonly ILogger<DocumentationCommand> _logger;
        private readonly DocumentationProvider _documentationProvider;

        public DocumentationCommand(DocumentationProvider documentationProvider, ILogger<DocumentationCommand>? logger = null)
        {
            _documentationProvider = documentationProvider;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationCommand>();
        }

        [Command("documentation"), Description("Retrieves documentation for a given type or member.")]
        public async Task GetDocumentationAsync(CommandContext context, [Description("Which type or member to grab documentation upon."), AutoComplete, RemainingText] string query)
        {
            DocumentationMember? documentation = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("No query provided.");
                await context.ReplyAsync("No query provided.");
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
                await context.ReplyAsync("No documentation found.");
                return;
            }

            _logger.LogDebug("Documentation found for: {Query}.", documentation.DisplayName);
            if (documentation.SourceUri.IsValueCreated)
            {
                await context.ReplyAsync(documentation.Content);
                return;
            }

            // Defer
            await context.DelayAsync();
            Uri? source = await documentation.SourceUri.Value;
            if (source is not null)
            {
                string[] lines = documentation.Content.Split('\n');
                lines[0] = $"## [{lines[0][3..]}](<{source}>)";
                documentation.Content = string.Join('\n', lines);
            }
            await context.ReplyAsync(documentation.Content);
        }
    }
}
