using System.Collections.Generic;

namespace OoLunar.DocBot.MemberDefinitions
{
    public sealed class NamespaceDefinition : MemberDefinition
    {
        public IReadOnlyDictionary<string, MemberDefinition> Members => _members;
        private readonly Dictionary<string, MemberDefinition> _members = [];

        public NamespaceDefinition(string name, string fullspan) : base(name, fullspan) { }

        public void SetMember(MemberDefinition member) => _members[member.Name] = member;
    }
}
