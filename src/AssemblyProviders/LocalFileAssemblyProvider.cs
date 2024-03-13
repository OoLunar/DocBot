using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class LocalFileAssemblyProvider : IAssemblyProvider
    {
        public string Name { get; init; } = "LocalFile";

        private readonly ILogger<LocalFileAssemblyProvider> _logger;
        private readonly IReadOnlyList<string> _assemblyPaths;

        public LocalFileAssemblyProvider(ILogger<LocalFileAssemblyProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalFileAssemblyProvider>();

            string? currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(currentDirectory) || !Directory.Exists(currentDirectory))
            {
                currentDirectory = AppContext.BaseDirectory;
            }

            _assemblyPaths = Directory.EnumerateFiles(currentDirectory, "*.dll").ToArray();
        }

        public LocalFileAssemblyProvider(IReadOnlyList<string> assemblyPaths, ILogger<LocalFileAssemblyProvider>? logger = null)
        {
            if (assemblyPaths is null)
            {
                throw new ArgumentNullException(nameof(assemblyPaths));
            }
            else if (assemblyPaths.Count == 0)
            {
                throw new ArgumentException("Assembly paths cannot be empty.", nameof(assemblyPaths));
            }

            foreach (string assemblyPath in assemblyPaths)
            {
                if (!File.Exists(assemblyPath))
                {
                    throw new ArgumentException($"Assembly path {assemblyPath} does not exist.", assemblyPath);
                }
            }

            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalFileAssemblyProvider>();
            _assemblyPaths = assemblyPaths;
        }

        public ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            List<Assembly> assemblies = [];
            foreach (string file in _assemblyPaths)
            {
                try
                {
                    DocumentationLoadContext loadContext = new(Path.GetFullPath(file));
                    assemblies.Add(loadContext.LoadFromAssemblyPath(Path.GetFullPath(file)));
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "Failed to load assembly {AssemblyName}.", file);
                }
            }

            return ValueTask.FromResult<IEnumerable<Assembly>>(assemblies);
        }
    }
}
