using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot
{
    public sealed class GitHubMetadataRetriever
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubMetadataRetriever> _logger;

        public GitHubMetadataRetriever(IConfiguration configuration, HttpClient httpClient, ILogger<GitHubMetadataRetriever>? logger = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? NullLogger<GitHubMetadataRetriever>.Instance;
        }

        public async ValueTask<string?> MatchTagToReleaseAsync(string repository, string tagName)
        {
            JsonDocument? release = await _httpClient.GetFromJsonAsync<JsonDocument>($"https://api.github.com/repos/{repository}/releases/latest");
            if (release is null)
            {
                _logger.LogError("Failed to get latest release for {Repository}", repository);
                return null;
            }
            else if (release.RootElement.GetArrayLength() == 0)
            {
                _logger.LogError("No releases found for {Repository}", repository);
                return null;
            }

            foreach (JsonElement jsonElement in release.RootElement.EnumerateArray())
            {
                string? commit = jsonElement.GetProperty("tag_name").GetString();
                if (commit is not null && commit.EndsWith(tagName, StringComparison.Ordinal))
                {
                    _logger.LogDebug("Got latest release {Release} for {Repository}", release, repository);
                    return commit;
                }
            }

            _logger.LogWarning("Failed to find tag {Tag} for {Repository}. Returning the latest release instead.", tagName, repository);
            return release.RootElement[0].GetProperty("tag_name").GetString();
        }

        public async ValueTask<string> GetCommitFromTagAsync(string repository, string tagName)
        {
            JsonDocument? tag = await _httpClient.GetFromJsonAsync<JsonDocument>($"https://api.github.com/repos/{repository}/git/refs/tags/{tagName}");
            if (tag is null)
            {
                _logger.LogError("Failed to get tag {Tag} for {Repository}", tagName, repository);
                return string.Empty;
            }

            string? commit = tag.RootElement.GetProperty("object").GetProperty("sha").GetString();
            if (commit is null)
            {
                _logger.LogError("Failed to get commit from tag {Tag} for {Repository}", tagName, repository);
                return string.Empty;
            }

            _logger.LogDebug("Got commit {Commit} for tag {Tag} for {Repository}", commit, tagName, repository);
            return commit;
        }

        public async ValueTask<Uri?> TryParseRepositoryInformationAsync(Assembly assembly)
        {
            string? gitRepository = null;
            string? gitCommit = null;
            AssemblyMetadataAttribute? assemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "RepositoryUrl");
            AssemblyInformationalVersionAttribute? assemblyInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (assemblyMetadata is not null && assemblyMetadata.Value is not null && Uri.TryCreate(assemblyMetadata.Value, UriKind.Absolute, out Uri? uri))
            {
                gitRepository = uri.AbsolutePath[1..];
                if (gitRepository.EndsWith(".git", StringComparison.Ordinal))
                {
                    gitRepository = gitRepository[..^4];
                }
            }

            if (assemblyInformationalVersion is not null)
            {
                string[] parts = assemblyInformationalVersion.InformationalVersion.Split('+');
                if (parts.Length == 2)
                {
                    gitCommit = parts[1];
                }
                else if (gitRepository is not null)
                {
                    // See if there are any releases whose name or tag name match this version
                    gitCommit = await MatchTagToReleaseAsync(gitRepository, assemblyInformationalVersion.InformationalVersion);
                    if (string.IsNullOrWhiteSpace(gitCommit))
                    {
                        gitCommit = await GetCommitFromTagAsync(gitRepository, assemblyInformationalVersion.InformationalVersion);
                    }
                }
            }

            if (gitRepository is null || gitCommit is null || !Uri.TryCreate($"https://api.github.com/search/code?q={{Query}}+language:csharp+repo:{gitRepository}", UriKind.Absolute, out Uri? apiUrl))
            {
                _logger.LogWarning("Unable to determine GitHub API URL for {AssemblyName}.", assembly.FullName);
                return null;
            }

            return apiUrl;
        }

        public async Task<Uri?> SearchCodeForMemberAsync(MemberInfo memberInfo, Uri? apiUrl = null)
        {
            if (apiUrl is null)
            {
                _logger.LogError("GitHub API URL is not configured. Unable to use the GitHub Search Code endpoint.");
                return null;
            }

            string? githubToken = _configuration.GetValue<string>("github:token");
            if (githubToken is null)
            {
                _logger.LogError("GitHub token is not configured. Unable to use the GitHub Search Code endpoint.");
                return null;
            }

            apiUrl = new Uri(apiUrl.OriginalString.Replace("{Query}", memberInfo.Name));
            _logger.LogTrace("Fetching GitHub URL for member: {MemberName}", memberInfo.GetFullName());

            using HttpRequestMessage request = new(HttpMethod.Get, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {githubToken}");
            JsonDocument? jsonDocument = await _httpClient.GetFromJsonAsync<JsonDocument>(apiUrl);
            if (jsonDocument is null)
            {
                _logger.LogError("Failed to parse JSON response from GitHub API for member: {MemberName}", memberInfo.GetFullName());
                return null;
            }

            Uri? uri = null;
            foreach (JsonElement jsonElement in jsonDocument.RootElement.GetProperty("items").EnumerateArray())
            {
                string name = jsonElement.GetProperty("name").GetString() ?? string.Empty;
                if (memberInfo is not Type typeInfo || typeInfo.IsNested)
                {
                    // Return the first result because we're not going to (reasonably) be able to do any better
                    uri = new Uri(jsonElement.GetProperty("html_url").GetString() ?? string.Empty);
                    break;
                }
                // If it is a type, we need to find the file that matches the type name
                else if (name.Equals($"{memberInfo.Name}.cs", StringComparison.Ordinal))
                {
                    uri = new Uri(jsonElement.GetProperty("html_url").GetString() ?? string.Empty);
                    break;
                }
            }

            return uri;
        }
    }
}
