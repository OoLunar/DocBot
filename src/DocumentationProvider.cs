using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Namotion.Reflection;
using OoLunar.DocBot.GitHub;

namespace OoLunar.DocBot
{
    public sealed class DocumentationProvider
    {
        private static readonly XmlDocsOptions _defaultXmlDocsOptions = new()
        {
            FormattingMode = XmlDocsFormattingMode.Markdown,
            ResolveExternalXmlDocs = true
        };

        public IReadOnlyDictionary<int, DocumentationMember> Members { get; private set; }

        private readonly AssemblyProviderAsync _assemblyProvider;
        private readonly GitHubMetadataRetriever _github;
        private readonly ILogger<DocumentationProvider> _logger;

        public DocumentationProvider(AssemblyProviderAsync assemblyProvider, GitHubMetadataRetriever github, ILogger<DocumentationProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationProvider>();
            _assemblyProvider = assemblyProvider ?? throw new ArgumentNullException(nameof(assemblyProvider));
            _github = github ?? throw new ArgumentNullException(nameof(github));
            Members = new Dictionary<int, DocumentationMember>().ToFrozenDictionary();
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

            Dictionary<int, DocumentationMember> members = [];
            foreach (DocumentationMember member in await GetMembersAsync(assemblies))
            {
                _logger.LogTrace("Loaded: {MemberName}", member.DisplayName);
                members[member.GetHashCode()] = member;
            }

            Members = members.OrderBy(x => x.Value.FullName).ToDictionary(x => x.Key, x => x.Value);
            _logger.LogInformation("Reloaded documentation. Found {MemberCount} members.", Members.Count);
            return;
        }

        public IEnumerable<DocumentationMember> FindMatchingDocs(string query)
        {
            List<DocumentationMember> foundDocs = [];

            if (int.TryParse(query, out int id)
                && Members.TryGetValue(id, out DocumentationMember? foundDockMember))
            {
                foundDocs.Add(foundDockMember);
            }
            else
            {
                foreach (DocumentationMember member in Members.Values)
                {
                    if (member.FullName.Equals(query, StringComparison.OrdinalIgnoreCase)
                        || member.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase))
                    {
                        foundDocs.Clear();
                        foundDocs.Add(member);
                        break;
                    }
                    else if (member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        foundDocs.Add(member);
                    }
                }
            }

            return foundDocs;
        }

        private async Task<ConcurrentQueue<DocumentationMember>> GetMembersAsync(IEnumerable<Assembly> assemblies)
        {
            ConcurrentQueue<DocumentationMember> members = new();
            await Parallel.ForEachAsync(assemblies, async (assembly, ct) =>
            {
                try
                {
                    Uri? apiUrl = await _github.TryParseRepositoryInformationAsync(assembly);

                    Parallel.ForEach(assembly.GetExportedTypes(), type =>
                        Parallel.ForEach(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Append(type), member =>
                        {
                            if (member.TryGetPropertyValue("IsSpecialName", false) || member.GetCustomAttribute<CompilerGeneratedAttribute>(true) is not null)
                            {
                                return;
                            }

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

                            string? name = null;
                            if (member.DeclaringType is null)
                            {
                                if (member is Type memberType)
                                {
                                    name = memberType.FullName;
                                }

                                name ??= member.Name;
                            }
                            else
                            {
                                name = $"{member.DeclaringType.FullName}.{member.Name}";
                            }

                            StringBuilder stringBuilder = new("## ");
                            stringBuilder.Append(Formatter.Sanitize(name));
                            stringBuilder.AppendLine();
                            stringBuilder.AppendLine("### Summary");
                            stringBuilder.AppendLine(Formatter.Sanitize(summary));
                            if (remarks is not null)
                            {
                                stringBuilder.AppendLine("### Remarks");
                                stringBuilder.AppendLine(Formatter.Sanitize(remarks));
                            }

                            stringBuilder.AppendLine("### Declaration");
                            stringBuilder.AppendLine(Formatter.BlockCode(member.GetDeclarationSyntax(), "cs"));

                            members.Enqueue(new DocumentationMember(
                                name,
                                member.GetFullName(),
                                stringBuilder.ToString().TrimLength(2048),
                                new Lazy<Task<Uri?>>(() => _github.SearchCodeForMemberAsync(member, apiUrl), false)));
                        }));
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "Failed to load documentation for assembly: {AssemblyName}", assembly.FullName);
                }
            });

            return members;
        }
    }
}
