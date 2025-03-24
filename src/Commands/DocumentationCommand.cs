using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OoLunar.DocBot.Interactivity;
using OoLunar.DocBot.Interactivity.Moments.Pagination;

namespace OoLunar.DocBot.Commands
{
    public sealed class DocumentationCommand : IAutoCompleteProvider
    {
        private readonly ILogger<DocumentationCommand> _logger;
        private readonly DocumentationProvider _documentationProvider;
        private readonly Procrastinator _procrastinator;

        public DocumentationCommand(DocumentationProvider documentationProvider, Procrastinator procrastinator, ILogger<DocumentationCommand>? logger = null)
        {
            _documentationProvider = documentationProvider;
            _procrastinator = procrastinator;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationCommand>();
        }

        [Command("documentation"), TextAlias("doc", "docs"), Description("Retrieves documentation for a given type or member.")]
        public async Task GetDocumentationAsync(CommandContext context,
            [Description("Which type or member to grab documentation upon."), SlashAutoCompleteProvider<DocumentationCommand>, RemainingText] string? query = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Select a random member
                await ReplyWithDocumentationAsync(context, _documentationProvider.Members.Values.ElementAt(Random.Shared.Next(_documentationProvider.Members.Count)));
                return;
            }

            IReadOnlyList<DocumentationMember> foundDocs = _documentationProvider.FindMatchingDocs(query);
            if (foundDocs.Count == 0)
            {
                _logger.LogDebug("No documentation found for: {Query}.", query);
                await context.RespondAsync("No documentation found.");
                return;
            }

            bool deferred = false;
            List<Page> pages = [];
            foreach (DocumentationMember member in foundDocs)
            {
                if (!member.SourceUri.IsValueCreated && !deferred)
                {
                    deferred = true;
                    await context.DeferResponseAsync();

                    Uri? source = await member.SourceUri.Value;
                    if (source is not null)
                    {
                        string[] lines = member.Content.Split('\n');
                        lines[0] = $"## [{lines[0][3..]}](<{source}>)";
                        member.Content = string.Join('\n', lines);
                    }
                }

                pages.Add(new Page(new DiscordMessageBuilder().WithContent(member.Content), member.DisplayName, member.Content.Split('\n')[2]));
            }

            await context.PaginateAsync(pages);
        }

        public ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            string query = context.UserInput?.Trim() ?? string.Empty;
            _logger.LogDebug("Querying documentation for: \"{Query}\"", query);

            Dictionary<string, DiscordAutoCompleteChoice> choices = [];
            foreach (DocumentationMember member in _documentationProvider.Members.Values)
            {
                if (!member.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    && !member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string trimmedDisplayName = member.DisplayName.TrimLength(100);
                DiscordAutoCompleteChoice choice = new(trimmedDisplayName,
                    member.GetHashCode().ToString(CultureInfo.InvariantCulture));

                if (!choices.TryAdd(trimmedDisplayName, choice))
                {
                    continue;
                }

                if (choices.Count == 10)
                {
                    break;
                }
            }

            return ValueTask.FromResult<IEnumerable<DiscordAutoCompleteChoice>>(choices.Values);
        }

        private async Task ReplyWithDocumentationAsync(CommandContext context, DocumentationMember foundDocs)
        {
            _logger.LogDebug("Documentation found for: {Query}.", foundDocs.DisplayName);
            if (foundDocs.SourceUri.IsValueCreated)
            {
                await context.RespondAsync(foundDocs.Content);
                return;
            }

            // Defer
            await context.DeferResponseAsync();
            Uri? source = await foundDocs.SourceUri.Value;
            if (source is not null)
            {
                string[] lines = foundDocs.Content.Split('\n');
                lines[0] = $"## [{lines[0][3..]}](<{source}>)";
                foundDocs.Content = string.Join('\n', lines);
            }

            await context.EditResponseAsync(foundDocs.Content);
        }

        private static void FormatDocumentationList(DiscordEmbedBuilder embedBuilder, IReadOnlyList<DocumentationMember> foundDocs)
        {
            embedBuilder.WithTitle($"Your query returned {foundDocs.Count} matching items!")
                .WithColor(0xEED202);

            foreach (DocumentationMember member in foundDocs)
            {
                // Plus one to account for final period
                int trimLength = member.DisplayName.Length + 1;

                if (member.DisplayName.EndsWith("()"))
                {
                    trimLength -= 2;
                }

                embedBuilder.AddField(member.DisplayName, member.FullName[..^trimLength]);
            }
        }
    }
}
