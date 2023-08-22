using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        public FrozenDictionary<Ulid, DocumentationMember> Members { get; private set; }

        private readonly AssemblyProviderAsync _assemblyProvider;
        private readonly ILogger<DocumentationProvider> _logger;

        public DocumentationProvider(AssemblyProviderAsync assemblyProvider, ILogger<DocumentationProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationProvider>();
            _assemblyProvider = assemblyProvider;
            Members = new Dictionary<Ulid, DocumentationMember>().ToFrozenDictionary();
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

            Dictionary<Ulid, DocumentationMember> members = new();
            foreach (DocumentationMember member in ParseMembers(GetMembers(assemblies)))
            {
                _logger.LogTrace("Loaded: {MemberName}", member.DisplayName);
                members[member.Id] = member;
            }

            Members = members.ToFrozenDictionary();
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

        private static IEnumerable<DocumentationMember> ParseMembers(IEnumerable<MemberInfo> members)
        {
            DiscordEmbedBuilder baseEmbedBuilder = new() { Color = new DiscordColor(0x6b73db) };
            foreach (MemberInfo member in members)
            {
                StringBuilder stringBuilder = new("## ");
                string summary = member.GetXmlDocsSummary(_defaultXmlDocsOptions);
                string? remarks = member.GetXmlDocsRemarks(_defaultXmlDocsOptions);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = "No summary provided.";
                }

                if (string.IsNullOrWhiteSpace(remarks))
                {
                    remarks = null;
                }

                Uri? source = null;
                if (source is null)
                {
                    stringBuilder.Append(member.DeclaringType is null ? "<runtime generated>" : member.DeclaringType.FullName);
                    stringBuilder.Append('.');
                    stringBuilder.Append(member.Name);
                }
                else
                {
                    stringBuilder.Append('[');
                    stringBuilder.Append(member.DeclaringType is null ? "<runtime generated>" : member.DeclaringType.FullName);
                    stringBuilder.Append('.');
                    stringBuilder.Append(member.Name);
                    stringBuilder.Append("](");
                    stringBuilder.Append(source);
                    stringBuilder.Append(')');
                }

                stringBuilder.AppendLine();
                stringBuilder.AppendLine("### Summary");
                stringBuilder.AppendLine(summary);
                if (remarks is not null)
                {
                    stringBuilder.AppendLine("### Remarks");
                    stringBuilder.AppendLine(remarks);
                }

                stringBuilder.AppendLine("### Declaration");
                stringBuilder.AppendLine(Formatter.BlockCode(member.GetDeclarationSyntax(), "cs"));

                yield return new DocumentationMember(Ulid.NewUlid(), member.GetFullName(), stringBuilder.ToString().TrimLength(2048));
            }
        }
    }
}
