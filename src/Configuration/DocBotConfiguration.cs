namespace OoLunar.DocBot.Configuration
{
    public sealed record DocBotConfiguration
    {
        public required DiscordConfiguration Discord { get; init; }
        public required string SelectedSymbolProvider { get; init; }
        public LoggerConfiguration Logger { get; init; } = new();
    }
}
