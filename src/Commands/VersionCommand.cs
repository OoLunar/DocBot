using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus.Commands;

namespace OoLunar.DocBot.Commands
{
    public static class VersionCommand
    {
        private static readonly string _assemblyVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        [Command("version"), Description("Get the version of the bot.")]
        public static ValueTask ExecuteAsync(CommandContext context) => context.RespondAsync($"Version: {_assemblyVersion}");
    }
}