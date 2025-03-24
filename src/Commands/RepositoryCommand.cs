using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using OoLunar.DocBot.Configuration;

namespace OoLunar.DocBot.Commands
{
    public sealed class RepositoryCommand
    {
        private readonly DocBotConfiguration _configuration;

        public RepositoryCommand(DocBotConfiguration configuration) => _configuration = configuration;

        [Command("repository"), Description("Get the repository link."), TextAlias("repo", "source", "code")]
        public ValueTask ExecuteAsync(CommandContext context) => context.RespondAsync($"https://github.com/{_configuration.Discord.Repository}");
    }
}