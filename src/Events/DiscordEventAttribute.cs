using System;
using DSharpPlus;

namespace OoLunar.DocBot.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class DiscordEventAttribute : Attribute
    {
        public DiscordIntents Intents { get; init; }
        public DiscordEventAttribute(DiscordIntents intents) => Intents = intents;
    }
}
