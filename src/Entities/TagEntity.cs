using System;
using System.Collections.Generic;

namespace OoLunar.DocBot.Entities
{
    public sealed class TagEntity
    {
        public string Name { get; init; }
        public string Content { get; init; }
        public IReadOnlyList<string> Aliases { get; init; }
        public IReadOnlyList<TagHistory> History { get; init; }

        public TagEntity(string name, string content, IReadOnlyList<string> aliases, IReadOnlyList<TagHistory> history)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentException.ThrowIfNullOrEmpty(content, nameof(content));
            ArgumentNullException.ThrowIfNull(aliases, nameof(aliases));
            ArgumentNullException.ThrowIfNull(history, nameof(history));

            Name = name;
            Content = content;
            Aliases = aliases;
            History = history;
        }
    }
}
