using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class LocalProjectAssemblyProvider : IAssemblyProvider
    {
        public string Name { get; init; } = "local_project";

        private readonly ILogger<LocalProjectAssemblyProvider> _logger;
        private readonly string _projectPath;

        public LocalProjectAssemblyProvider(string projectPath, ILogger<LocalProjectAssemblyProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalProjectAssemblyProvider>();
            _projectPath = projectPath;
        }

        public LocalProjectAssemblyProvider(IConfiguration configuration, ILogger<LocalProjectAssemblyProvider>? logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalProjectAssemblyProvider>();
            _projectPath = configuration.GetValue("local_project:path", "src")!;
        }

        public async ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            List<Assembly> assemblies = [];
            foreach (string file in Directory.EnumerateFiles(_projectPath, "*.csproj", SearchOption.AllDirectories).OrderBy(x => x.Count(character => character == '.')).ThenBy(x => x))
            {
                if (File.ReadAllText(file).Contains("<OutputType>Exe</OutputType>"))
                {
                    _logger.LogDebug("Skipping project {ProjectName} because it is an executable.", file);
                    continue;
                }

                _logger.LogDebug("Building project {ProjectName}.", file);
                string? assemblyPath = await BuildProjectAsync(file);
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    _logger.LogError("Failed to build project {ProjectName}.", file);
                    continue;
                }

                try
                {
                    DocumentationLoadContext loadContext = new(assemblyPath);
                    Assembly assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(file)));
                    assemblies.Add(assembly);
                }
                catch (ReflectionTypeLoadException error)
                {
                    _logger.LogError(error, "Failed to load assembly {AssemblyName}.", file);
                }
            }

            return assemblies;
        }

        public async ValueTask<string?> BuildProjectAsync(string projectFile)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                Arguments = $"publish --framework {ThisAssembly.Project.TargetFramework} -c Debug -p:GenerateDocumentationFile=true",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectFile)!,
            };

            Process process = Process.Start(startInfo)!;
            try
            {
                CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Failed to build project {ProjectName} within 10 seconds.", projectFile);
                process.Kill();
                return await BuildProjectAsync(projectFile);
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("Failed to build project {ProjectName}.", projectFile);
                _logger.LogError("{Error}", process.StandardError.ReadToEnd());
                return null;
            }

            string? assemblyPath = Directory.EnumerateFiles($"{Path.GetDirectoryName(projectFile)}/bin/Debug/{ThisAssembly.Project.TargetFramework}/publish/"!, $"{Path.GetFileNameWithoutExtension(projectFile)}.dll", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                _logger.LogError("Failed to find assembly for project {ProjectName}.", projectFile);
                return null;
            }

            _logger.LogInformation("Successfully built project {ProjectName}.", projectFile);
            return assemblyPath;
        }
    }
}
