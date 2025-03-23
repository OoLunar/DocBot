using System;
using System.Collections.Generic;
using System.Linq;
using DSharpPlus.Entities;

namespace OoLunar.DocBot.Interactivity.Moments.Pick
{
    public class PickDefaultComponentCreator : IPickComponentCreator
    {
        public DiscordSelectComponent CreatePickDropdown(string question, IReadOnlyList<string> options, Ulid id)
           => new(id.ToString(), "Answer here!", options.Select(option => new DiscordSelectComponentOption(option, option)));
    }
}
