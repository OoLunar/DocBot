using System;
using System.Collections.Generic;
using DSharpPlus.Entities;

namespace OoLunar.DocBot.Interactivity.Moments.Pick
{
    public interface IPickComponentCreator : IComponentCreator
    {
        public DiscordSelectComponent CreatePickDropdown(string question, IReadOnlyList<string> options, Ulid id);
    }
}
