using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NugetNullLogger = NuGet.Common.NullLogger;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class NugetAssemblyProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NugetAssemblyProvider> _logger;
        private readonly SourceRepository _repository;
        private readonly SourceCacheContext _repositoryCache;

        public NugetAssemblyProvider(IConfiguration configuration, ILogger<NugetAssemblyProvider>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            _configuration = configuration;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<NugetAssemblyProvider>();
            _repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            _repositoryCache = new();
        }

        public async ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            string[]? packages = _configuration.GetSection("Nuget:Packages").Get<string[]>();
            if (packages is null || packages.Length == 0)
            {
                return Array.Empty<Assembly>();
            }

            string path = _configuration.GetValue("Nuget:Path", "packages")!;
            List<string> installedPackages = new();
            FindPackageByIdResource resource = await _repository.GetResourceAsync<FindPackageByIdResource>();
            foreach (string package in packages)
            {
                IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(package, _repositoryCache, NugetNullLogger.Instance, default);
                if (!versions.Any())
                {
                    _logger.LogWarning("Package {Package} has no versions", package);
                    continue;
                }

                NuGetVersion version = versions.LastOrDefault(version => !version.IsLegacyVersion) ?? versions.LastOrDefault(version => version.IsPrerelease) ?? versions.Last();
                MemoryStream packageStream = new();
                await resource.CopyNupkgToStreamAsync(package, version, packageStream, _repositoryCache, NugetNullLogger.Instance, default);
                _logger.LogInformation("Downloaded package {Package} {Version}", package, version);

                using PackageArchiveReader packageReader = new(packageStream);
                IEnumerable<FrameworkSpecificGroup> frameworkGroups = packageReader.GetReferenceItems();
                if (!frameworkGroups.Any())
                {
                    _logger.LogWarning("Package {Package} {Version} has no reference items", package, version);
                    continue;
                }

                FrameworkSpecificGroup? frameworkGroup = frameworkGroups.FirstOrDefault(framework => !framework.HasEmptyFolder);
                if (frameworkGroup is null)
                {
                    _logger.LogWarning("Package {Package} {Version} has no non-empty framework groups", package, version);
                    continue;
                }

                IEnumerable<string> selectedItems = frameworkGroup.Items.Where(item => item.EndsWith(".dll") || item.EndsWith(".xml"));
                if (!selectedItems.Any())
                {
                    _logger.LogWarning("Package {Package} {Version} has no dll or xml files", package, version);
                    continue;
                }

                foreach (string item in selectedItems)
                {
                    string packagePath = Path.Combine(path, item);
                    packageReader.ExtractFile(item, packagePath, NugetNullLogger.Instance);

                    if (packagePath.EndsWith(".dll"))
                    {
                        installedPackages.Add(packagePath);
                    }
                }

            }

            return installedPackages.Count == 0
                ? Array.Empty<Assembly>()
                : await new LocalFileAssemblyProvider(installedPackages.ToArray()).GetAssembliesAsync();
        }
    }
}
