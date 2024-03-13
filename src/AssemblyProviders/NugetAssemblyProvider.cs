using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OoLunar.DocBot.Configuration;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class NugetAssemblyProvider : IAssemblyProvider
    {

        private const string TARGET_FRAMEWORK = ThisAssembly.Project.TargetFramework;
        private const string CSPROJ_TEMPLATE = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <SuppressNETCoreSdkPreviewMessage>true</SuppressNETCoreSdkPreviewMessage>
    <TargetFramework>{TARGET_FRAMEWORK}</TargetFramework>
  </PropertyGroup>
</Project>
""";

        public string Name { get; init; } = "NuGet";
        private readonly IConfigurationSection _configuration;
        private readonly ILogger<NugetAssemblyProvider> _logger;

        public NugetAssemblyProvider(DocBotConfiguration configuration, ILogger<NugetAssemblyProvider>? logger = null)
        {
            _configuration = configuration.AssemblyProviders[Name];
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<NugetAssemblyProvider>();
        }

        public async ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            Dictionary<string, string?>? packages = _configuration.GetSection("nuget:packages")?.GetChildren()?.ToDictionary(x => x.Key, x => x.Value);
            if (packages is null || packages.Count == 0)
            {
                _logger.LogWarning("No packages were specified.");
                return Enumerable.Empty<Assembly>();
            }

            string assemblyPath = Path.GetFullPath(_configuration.GetValue("nuget:path", "packages")!);
            IReadOnlyList<string> assemblies = GetRequestedAssemblies(assemblyPath, packages.Keys);
            if (assemblies.Count != packages.Count)
            {
                if (Directory.Exists(assemblyPath))
                {
                    Directory.Delete(assemblyPath, true);
                }

                _logger.LogInformation("Restoring packages...");
                Directory.CreateDirectory(assemblyPath);
                File.WriteAllText(Path.Combine(assemblyPath, "OoLunar.DocBot.csproj"), CSPROJ_TEMPLATE);

                foreach ((string packageId, string? packageVersion) in packages)
                {
                    _logger.LogDebug("Restoring {PackageId}...", packageId);
                    await AddPackageReferenceAsync(assemblyPath, null, packageId, packageVersion);
                    _logger.LogInformation("Restored {PackageId}.", packageId);
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = "dotnet",
                    Arguments = $"publish {Path.Combine(assemblyPath, "OoLunar.DocBot.csproj")} --framework {TARGET_FRAMEWORK}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                Process process = Process.Start(startInfo)!;
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Failed to restore packages.");
                    _logger.LogError("{Error}", process.StandardError.ReadToEnd());
                    return Enumerable.Empty<Assembly>();
                }

                _logger.LogInformation("Restored packages.");
                assemblies = GetRequestedAssemblies(assemblyPath, packages.Keys);
            }

            return await new LocalFileAssemblyProvider(assemblies, null).GetAssembliesAsync();
        }

        public async ValueTask AddPackageReferenceAsync(string dir, IEnumerable<string>? sources, string packageId, string? packageVersion = null)
        {
            StringBuilder arguments = new();
            arguments.Append("add ");
            arguments.Append(dir);
            arguments.Append(" package ");
            arguments.Append(packageId);
            arguments.Append(' ');
            if (!string.IsNullOrWhiteSpace(packageVersion))
            {
                arguments.Append("--version ");
                arguments.Append(packageVersion);
                arguments.Append(' ');
            }
            else
            {
                arguments.Append("--prerelease ");
            }

            arguments.Append("--framework ");
            arguments.Append(TARGET_FRAMEWORK);
            arguments.Append(' ');
            if (sources is not null)
            {
                foreach (string source in sources)
                {
                    arguments.Append("--source ");
                    arguments.Append(source);
                    arguments.Append(' ');
                }
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                Arguments = arguments.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            Process process = Process.Start(startInfo)!;
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                _logger.LogError("Failed to add package {PackageId} to {Directory}.", packageId, dir);
                _logger.LogError("{Error}", process.StandardError.ReadToEnd());
            }
        }

        public static IReadOnlyList<string> GetRequestedAssemblies(string assemblyPath, IEnumerable<string> packages)
        {
            string path = Path.Combine(assemblyPath, $"bin/Release/{TARGET_FRAMEWORK}/publish/");
            if (!Directory.Exists(path))
            {
                return new List<string>();
            }

            List<string> assemblies = [];
            foreach (string file in Directory.EnumerateFiles(path, "*.dll"))
            {
                if (packages.Any(package => package.EndsWith(Path.GetFileNameWithoutExtension(file), StringComparison.Ordinal)))
                {
                    assemblies.Add(file);
                }
            }

            return assemblies;
        }
    }
}
