using System;
using System.Collections.Generic;

namespace OoLunar.DocBot.MemberDefinitions
{
    public sealed class ClassDefinition : MemberDefinition
    {
        public ClassDefinitionMetadata Metadata { get; private set; }
        public IReadOnlyDictionary<string, MemberDefinition> Members => _members;
        private readonly Dictionary<string, MemberDefinition> _members = [];

        public ClassDefinition(string name, string fullspan) : base(name, fullspan) => UpdateMetadata(fullspan);

        public void UpdateMetadata(string fullspan)
        {
            foreach (ClassDefinitionMetadata metadata in Enum.GetValues<ClassDefinitionMetadata>())
            {
                if (fullspan.Contains(metadata.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Metadata |= metadata;
                }
            }
        }

        public void SetMember(MemberDefinition member) => _members[member.Name] = member;
    }
}
