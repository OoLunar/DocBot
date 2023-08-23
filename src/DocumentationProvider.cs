using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.Configuration;
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

        private readonly IConfiguration _configuration;
        private readonly AssemblyProviderAsync _assemblyProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<DocumentationProvider> _logger;

        public DocumentationProvider(IConfiguration configuration, AssemblyProviderAsync assemblyProvider, HttpClient? httpClient = null, ILogger<DocumentationProvider>? logger = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DocumentationProvider>();
            if (httpClient is null)
            {
                AssemblyInformationalVersionAttribute? assemblyInformationalVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                httpClient = new HttpClient();
#if DEBUG
                httpClient.DefaultRequestHeaders.Add("User-Agent", $"OoLunar.DocBot/{assemblyInformationalVersion?.InformationalVersion ?? "0.1.0"}-dev");
#else
                httpClient.DefaultRequestHeaders.Add("User-Agent", $"OoLunar.DocBot/{assemblyInformationalVersion?.InformationalVersion ?? "0.1.0"}");
#endif
            }

            _httpClient = httpClient;
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
            foreach (DocumentationMember member in await GetMembersAsync(assemblies))
            {
                _logger.LogTrace("Loaded: {MemberName}", member.DisplayName);
                members[member.Id] = member;
            }

            Members = members.ToFrozenDictionary();
            _logger.LogInformation("Reloaded documentation.");
            return;
        }

        private async Task<ConcurrentQueue<DocumentationMember>> GetMembersAsync(IEnumerable<Assembly> assemblies)
        {
            ConcurrentQueue<DocumentationMember> members = new();
            await Parallel.ForEachAsync(assemblies, async (assembly, ct) =>
            {
                string? gitRepository = null;
                foreach (AssemblyMetadataAttribute assemblyMetadata in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (assemblyMetadata.Key == "RepositoryUrl" && assemblyMetadata.Value is not null)
                    {
                        if (Uri.TryCreate(assemblyMetadata.Value, UriKind.Absolute, out Uri? uri))
                        {
                            gitRepository = uri.AbsolutePath[1..];
                            if (gitRepository.EndsWith(".git", StringComparison.Ordinal))
                            {
                                gitRepository = gitRepository[..^4];
                            }
                            break;
                        }
                    }
                }

                string? gitCommit = null;
                AssemblyInformationalVersionAttribute? assemblyInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (assemblyInformationalVersion is not null)
                {
                    string[] parts = assemblyInformationalVersion.InformationalVersion.Split('+');
                    if (parts.Length == 2)
                    {
                        gitCommit = parts[1];
                    }
                    else
                    {
                        // See if there are any releases whose name or tag name match this version
                        // https://api.github.com/repos/:owner/:repo/releases
                        // https://api.github.com/repos/OoLunar/DocBot/releases
                        HttpResponseMessage response = await _httpClient.GetAsync($"https://api.github.com/repos/{gitRepository}/releases", ct);
                        if (response.IsSuccessStatusCode)
                        {
                            JsonDocument? jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                            if (jsonDocument is not null)
                            {
                                foreach (JsonElement jsonElement in jsonDocument.RootElement.EnumerateArray())
                                {
                                    string tagName = jsonElement.GetProperty("tag_name").GetString() ?? string.Empty;
                                    if (tagName.EndsWith(assemblyInformationalVersion.InformationalVersion, StringComparison.Ordinal))
                                    {
                                        gitCommit = tagName;
                                        break;
                                    }
                                }

                                // Gotta make a rest request to GitHub to get the commit hash from the tag name
                                // https://api.github.com/repos/:owner/:repo/git/ref/tags/:tag
                                // https://api.github.com/repos/OoLunar/DocBot/git/refs/tags/v0.1.0
                                response = await _httpClient.GetAsync($"https://api.github.com/repos/{gitRepository}/git/refs/tags/{gitCommit ?? assemblyInformationalVersion.InformationalVersion}", ct);
                                if (response.IsSuccessStatusCode)
                                {
                                    jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                                    if (jsonDocument is not null)
                                    {
                                        gitCommit = jsonDocument.RootElement.GetProperty("object").GetProperty("sha").GetString();
                                    }
                                    else
                                    {
                                        _logger.LogError("Failed to parse JSON response from GitHub API for {AssemblyName}.", assembly.FullName);
                                    }
                                }
                                else
                                {
                                    _logger.LogError("Failed to fetch GitHub commit hash for {AssemblyName}: {StatusCode} {ReasonPhrase} {Content}", assembly.FullName, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(ct));
                                }
                            }
                            else
                            {
                                _logger.LogError("Failed to parse JSON response from GitHub API for {AssemblyName}.", assembly.FullName);
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to fetch GitHub releases for {AssemblyName}: {StatusCode} {ReasonPhrase} {Content}", assembly.FullName, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(ct));
                        }

                        response.Dispose();
                    }
                }

                Uri? apiUrl = null;
                Uri? sourceUrl = null;
                if (gitRepository is null || gitCommit is null || !Uri.TryCreate($"https://github.com/{gitRepository}/blob/{gitCommit}", UriKind.Absolute, out sourceUrl) || !Uri.TryCreate($"https://api.github.com/search/code?q={{Query}}+language:csharp+repo:{gitRepository}", UriKind.Absolute, out apiUrl))
                {
                    _logger.LogWarning("Unable to determine GitHub API URL for {AssemblyName}.", assembly.FullName);
                }

                Parallel.ForEach(assembly.GetExportedTypes(), type =>
                    Parallel.ForEach(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Append(type), member =>
                    {
                        if (member.TryGetPropertyValue("IsSpecialName", false))
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
                        stringBuilder.Append(name);
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

                        members.Enqueue(new DocumentationMember(Ulid.NewUlid(),
                            name,
                            member.GetFullName(),
                            stringBuilder.ToString().TrimLength(2048),
                            new Lazy<Task<Uri?>>(() => FetchGitHubUrlAsync(member, apiUrl, sourceUrl), false)));
                    }));
            });
            return members;
        }

        private async Task<Uri?> FetchGitHubUrlAsync(MemberInfo member, Uri? apiUrl, Uri? sourceUrl)
        {
            string? githubToken = _configuration.GetValue<string>("github:token");
            if (githubToken is null)
            {
                _logger.LogError("GitHub token is not configured. Unable to use the GitHub Search Code endpoint.");
                return null;
            }
            else if (apiUrl is null)
            {
                _logger.LogError("Unable to determine GitHub API URL for member: {MemberName}", member.GetFullName());
                return null;
            }
            else if (sourceUrl is null)
            {
                _logger.LogError("Unable to determine GitHub source URL for member: {MemberName}", member.GetFullName());
                return null;
            }

            string query = member.Name;
            apiUrl = new Uri(apiUrl.OriginalString.Replace("{Query}", query));
            _logger.LogTrace("Fetching GitHub URL for member: {MemberName}", member.GetFullName());

            using HttpRequestMessage request = new(HttpMethod.Get, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {githubToken}");
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("HTTP {StatusCode} Failed to fetch GitHub URL for member: {MemberName}", response.StatusCode, member.GetFullName());
                return null;
            }

            JsonDocument? jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (jsonDocument is null)
            {
                _logger.LogError("Failed to parse JSON response from GitHub API for member: {MemberName}", member.GetFullName());
                return null;
            }

            if (member is Type)
            {
                foreach (JsonElement jsonElement in jsonDocument.RootElement.GetProperty("items").EnumerateArray())
                {
                    string name = jsonElement.GetProperty("name").GetString() ?? string.Empty;
                    if (member is Type && name.Equals($"{member.Name}.cs", StringComparison.InvariantCulture))
                    {
                        return new Uri(jsonElement.GetProperty("html_url").GetString() ?? string.Empty);
                    }
                }
            }
            else
            {
                JsonElement? jsonElement = jsonDocument.RootElement.GetProperty("items");
                if (jsonElement is null || jsonElement.Value.GetArrayLength() < 0)
                {
                    return null;
                }

                jsonElement = jsonElement.Value[0];
                string? url = jsonElement.Value.GetProperty("html_url").GetString();
                if (url is not null)
                {
                    return new Uri(url);
                }
            }

            return null;
        }
    }
}
