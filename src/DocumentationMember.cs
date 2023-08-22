using System;
using DSharpPlus.Entities;

namespace OoLunar.DocBot
{
    public sealed record DocumentationMember(Ulid Id, string DisplayName, DiscordEmbedBuilder EmbedBuilder);
}
