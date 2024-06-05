using System;

namespace OoLunar.DocBot.MemberDefinitions
{
    public abstract class MemberDefinition
    {
        public string Name { get; init; }
        public string Declaration { get; init; }

        protected MemberDefinition(string name, string fullspan)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ArgumentException.ThrowIfNullOrWhiteSpace(fullspan, nameof(fullspan));

            Name = name;
            Declaration = fullspan;
        }
    }
}
