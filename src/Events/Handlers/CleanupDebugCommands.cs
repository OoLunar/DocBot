using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.EventArgs;

namespace OoLunar.DocBot.Events.EventHandlers
{
    public sealed partial class CleanupDebugCommandsEventHandler : IEventHandler<GuildDownloadCompletedEventArgs>
    {
        private readonly CommandsExtension _extension;
        private readonly SlashCommandProcessor _processor;

        public CleanupDebugCommandsEventHandler(CommandsExtension extension, SlashCommandProcessor processor)
        {
            _extension = extension;
            _processor = processor;
        }

        public async Task HandleEventAsync(DiscordClient client, GuildDownloadCompletedEventArgs eventArgs)
        {
#if !DEBUG
            await _processor.ClearDiscordSlashCommandsAsync(true);
#endif
            await _processor.RegisterSlashCommandsAsync(_extension);
        }
    }
}
