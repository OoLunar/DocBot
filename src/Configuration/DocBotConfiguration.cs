using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace OoLunar.DocBot.Configuration
{
    public sealed record DocBotConfiguration
    {
        public required DiscordConfiguration Discord { get; init; }
        public required LoggerConfiguration Logger { get; init; }
        public required string HttpUserAgent { get; init; } = $"OoLunar.DocBot/{ThisAssembly.Project.Version} ({ThisAssembly.Project.RepositoryUrl})";
        public required string AssemblyProviderName { get; init; }
        public required Dictionary<string, IConfigurationSection> AssemblyProviders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
