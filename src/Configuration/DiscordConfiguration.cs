namespace OoLunar.DocBot.Configuration
{
    public sealed record DiscordConfiguration
    {
        public required string? Token { get; init; }
        public string Prefix { get; init; } = ">>";
        public string Repository { get; init; } = "OoLunar/DocBot";
        public ulong DebugGuildId { get; init; }
    }
}
