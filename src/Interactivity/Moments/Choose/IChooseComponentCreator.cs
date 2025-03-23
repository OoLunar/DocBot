using System;
using System.Collections.Generic;
using DSharpPlus.Entities;

namespace OoLunar.DocBot.Interactivity.Moments.Choose
{
    public interface IChooseComponentCreator : IComponentCreator
    {
        public DiscordSelectComponent CreateChooseDropdown(string question, IReadOnlyList<string> options, Ulid id);
    }
}
