using System;
using System.Threading.Tasks;

namespace OoLunar.DocBot
{
    public sealed record DocumentationMember
    {
        public Ulid Id { get; init; }
        public string FullName { get; init; }
        public string DisplayName { get; init; }
        public string Content { get; internal set; }
        public Lazy<Task<Uri?>> SourceUri { get; init; }

        public DocumentationMember(Ulid id, string fullName, string displayName, string content, Lazy<Task<Uri?>> sourceUri)
        {
            Id = id;
            FullName = fullName;
            DisplayName = displayName;
            Content = content;
            SourceUri = sourceUri;
        }
    }
}
