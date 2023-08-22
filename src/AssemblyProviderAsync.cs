using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace OoLunar.DocBot
{
    public delegate ValueTask<IEnumerable<Assembly>> AssemblyProviderAsync();
}
