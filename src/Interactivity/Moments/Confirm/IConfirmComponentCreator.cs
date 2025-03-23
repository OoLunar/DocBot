using System;
using DSharpPlus.Entities;

namespace OoLunar.DocBot.Interactivity.Moments.Confirm
{
    public interface IConfirmComponentCreator : IComponentCreator
    {
        public DiscordButtonComponent CreateConfirmButton(string question, Ulid id, bool isYesButton);
    }
}
