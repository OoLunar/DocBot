namespace OoLunar.DocBot.Configuration
{
    public sealed record DiscordConfiguration
    {
        public required string? Token { get; init; }
        public string Prefix { get; init; } = ">>";
        public ulong DebugGuildId { get; init; }
    }
}
