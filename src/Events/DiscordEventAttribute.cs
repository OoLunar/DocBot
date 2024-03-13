using System;

namespace OoLunar.DocBot.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class DiscordEventAttribute : Attribute;
}
