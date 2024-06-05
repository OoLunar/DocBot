using System.Collections.Generic;

namespace OoLunar.DocBot.MemberDefinitions
{
    public sealed class TypeDefinition : MemberDefinition
    {
        public IReadOnlyDictionary<string, MemberDefinition> Members => _members;
        private readonly Dictionary<string, MemberDefinition> _members = [];

        public TypeDefinition(string name, string fullspan) : base(name, fullspan) { }

        public void SetMember(MemberDefinition member) => _members[member.Name] = member;
    }
}
