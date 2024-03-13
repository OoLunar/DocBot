using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using OoLunar.DocBot.Configuration;

namespace OoLunar.DocBot.Events.EventHandlers
{
    public sealed partial class LinkIssueEventHandlers
    {
        [GeneratedRegex("##(\\d+)")]
        private static partial Regex IssueRegex();
        private readonly string _issuesUrl;
        private readonly HttpClient _httpClient;

        public LinkIssueEventHandlers(DocBotConfiguration configuration, HttpClient httpClient)
        {
            _issuesUrl = $"https://api.github.com/repos/{configuration.Discord.Repository}/issues";
            _httpClient = httpClient;
        }

        [DiscordEvent]
        public async Task LinkIssueAsync(DiscordClient client, MessageCreateEventArgs eventArgs)
        {
            if (eventArgs.Author.IsBot)
            {
                return;
            }

            List<string> issueLinks = [];
            foreach (Match match in IssueRegex().Matches(eventArgs.Message.Content))
            {
                if (!int.TryParse(match.Groups[1].Value, out int issueNumber))
                {
                    continue;
                }

                string issueUrl = $"{_issuesUrl}/{issueNumber.ToString(CultureInfo.InvariantCulture)}";
                HttpResponseMessage responseMessage = await _httpClient.GetAsync(issueUrl);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    continue;
                }

                JsonDocument? json = await responseMessage.Content.ReadFromJsonAsync<JsonDocument>();
                if (json is null || !json.RootElement.TryGetProperty("title", out JsonElement title) || !json.RootElement.TryGetProperty("user", out JsonElement user) || !user.TryGetProperty("login", out JsonElement login))
                {
                    continue;
                }

                if (json.RootElement.TryGetProperty("pull_request", out _))
                {
                    issueLinks.Add($"Pull Request #{issueNumber}: [{title.GetString()}](<{issueUrl}>) - {login.GetString()}");
                }
                else
                {
                    issueLinks.Add($"Issue #{issueNumber}: [{title.GetString()}](<{issueUrl}>) - {login.GetString()}");
                }
            }

            issueLinks.Sort();
            if (issueLinks.Count == 0)
            {
                return;
            }
            else if (issueLinks.Count == 1)
            {
                await eventArgs.Message.RespondAsync(issueLinks[0]);
            }
            else
            {
                StringBuilder builder = new();
                foreach (string issueLink in issueLinks)
                {
                    builder.AppendLine($"\\- {issueLink}");
                }

                await eventArgs.Message.RespondAsync(builder.ToString());
            }
        }
    }
}
