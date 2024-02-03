using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace OoLunar.DocBot.AssemblyProviders
{
    public interface IAssemblyProvider
    {
        internal static readonly PropertyInfo _nameProperty = typeof(IAssemblyProvider).GetProperty(nameof(Name))!;

        public string Name { get; init; }
        public ValueTask<IEnumerable<Assembly>> GetAssembliesAsync();
    }
}
