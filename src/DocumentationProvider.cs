using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Namotion.Reflection;

namespace OoLunar.DocBot
{
    public sealed class DocumentationProvider
    {
        private static readonly XmlDocsOptions _defaultXmlDocsOptions = new()
        {
            FormattingMode = XmlDocsFormattingMode.Markdown,
            ResolveExternalXmlDocs = true
        };

        public IReadOnlyDictionary<string, DiscordEmbedBuilder> Members { get; private set; }

        private readonly AssemblyProviderAsync _assemblyProvider;
        private readonly ILogger<DocumentationProvider> _logger;

        public DocumentationProvider(AssemblyProviderAsync assemblyProvider, ILogger<DocumentationProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationProvider>();
            _assemblyProvider = assemblyProvider;
            Members = new Dictionary<string, DiscordEmbedBuilder>().ToFrozenDictionary();
        }

        public async ValueTask ReloadAsync()
        {
            _logger.LogInformation("Reloading documentation...");

            IEnumerable<Assembly> assemblies;
            try
            {
                assemblies = await _assemblyProvider();
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Failed to reload documentation.");
                return;
            }

            Dictionary<string, DiscordEmbedBuilder> members = new(StringComparer.Ordinal);
            foreach ((string key, DiscordEmbedBuilder embedBuilder) in ParseMembers(GetMembers(assemblies)))
            {
                _logger.LogTrace("Loaded embed for {MemberName}.", embedBuilder.Title);
                members[key] = embedBuilder;
            }

            Members = members.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value).AsReadOnly();
            _logger.LogInformation("Reloaded documentation.");
            return;
        }

        private static ConcurrentQueue<MemberInfo> GetMembers(IEnumerable<Assembly> assemblies)
        {
            ConcurrentQueue<MemberInfo> members = new();
            Parallel.ForEach(assemblies, assembly =>
                Parallel.ForEach(assembly.GetExportedTypes(), type =>
                    Parallel.ForEach(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), member =>
                    {
                        if (member.TryGetPropertyValue("IsSpecialName", false))
                        {
                            return;
                        }

                        // We can't lock a list and do AddRange because we still have to filter out special names.
                        members.Enqueue(member);
                    })));

            return members;
        }

        private static IEnumerable<(string, DiscordEmbedBuilder)> ParseMembers(IEnumerable<MemberInfo> members)
        {
            DiscordEmbedBuilder baseEmbedBuilder = new() { Color = new DiscordColor(0x6b73db) };
            foreach (MemberInfo member in members)
            {
                DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder(baseEmbedBuilder)
                    .WithTitle((member.DeclaringType is null ? $"<runtime generated>.{member.Name}" : $"{member.DeclaringType.FullName}.{member.Name}").TrimLength(256))
                    .WithFooter($"{member.GetMemberType()}: {member.GetFullName()}\nDocumentation provided by {member.DeclaringType?.Assembly.GetName().Name}".TrimLength(2048));

                string summary = member.GetXmlDocsSummary(_defaultXmlDocsOptions).TrimLength(4096);
                string remarks = member.GetXmlDocsRemarks(_defaultXmlDocsOptions).TrimLength(1024);

                // If the summary length and remarks length (and a newline) is less than or equal to 4096, then we can just append the remarks to the summary.
                if (summary.Length + remarks.Length + 1 <= 4096)
                {
                    summary += "\n" + remarks;
                }
                else
                {
                    embedBuilder.AddField("Remarks", remarks);
                }

                embedBuilder.Description = summary;
                embedBuilder.AddField("Declaration", Formatter.BlockCode(member.GetDeclarationSyntax().TrimLength(1024), "cs"), false);
                embedBuilder.AddField("Source", "Unavailable", false);

                yield return (member.GetFullName(), embedBuilder);
            }
        }
    }
}
