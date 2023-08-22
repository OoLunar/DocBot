using System;

namespace OoLunar.DocBot
{
    public sealed record DocumentationMember(Ulid Id, string DisplayName, string Content);
}
