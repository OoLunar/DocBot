using System;
using System.Collections.Generic;
using System.Globalization;
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
            IEnumerable<IConfigurationSection> packages = _configuration.GetSection("nuget:packages").GetChildren();
            if (!packages.Any())
            {
                _logger.LogWarning("No packages specified in configuration");
                return Enumerable.Empty<Assembly>();
            }

            string path = _configuration.GetValue("Nuget:Path", "packages")!;
            List<string> installedPackages = new();
            FindPackageByIdResource resource = await _repository.GetResourceAsync<FindPackageByIdResource>();
            foreach (IConfigurationSection package in packages)
            {
                // Ensure the package has a valid version and exists
                if (!NuGetVersion.TryParse(package.Value, out NuGetVersion? packageVersion) || packageVersion is null)
                {
                    _logger.LogError("Package \"{Package}\" has an invalid version: {Version}", package.Key, package.Value);
                    continue;
                }
                else if (!await resource.DoesPackageExistAsync(package.Key, packageVersion, _repositoryCache, NugetNullLogger.Instance, default))
                {
                    _logger.LogError("Package \"{Package}\" with version \"{Version}\" does not exist.", package.Key, packageVersion);
                    continue;
                }

                MemoryStream packageStream = new();
                await resource.CopyNupkgToStreamAsync(package.Key, packageVersion, packageStream, _repositoryCache, NugetNullLogger.Instance, default);
                _logger.LogInformation("Downloaded package \"{Package}\" with version \"{Version}\"", package.Key, packageVersion);

                using PackageArchiveReader packageReader = new(packageStream);
                IEnumerable<FrameworkSpecificGroup> frameworkGroups = packageReader.GetReferenceItems();
                if (!frameworkGroups.Any())
                {
                    _logger.LogError("Package \"{Package}\" with version \"{Version}\" has no reference items", package, packageVersion);
                    continue;
                }

                FrameworkSpecificGroup? frameworkGroup = frameworkGroups.FirstOrDefault(framework => !framework.HasEmptyFolder);
                if (frameworkGroup is null)
                {
                    _logger.LogError("Package \"{Package}\" with version \"{Version}\" has no non-empty framework groups", package, packageVersion);
                    continue;
                }

                // Search for main dll and xml files
                IEnumerable<string> selectedItems = frameworkGroup.Items.Where(item => item.EndsWith(".dll", true, CultureInfo.InvariantCulture) || item.EndsWith(".xml", true, CultureInfo.InvariantCulture));
                if (!selectedItems.Any())
                {
                    _logger.LogError("Package \"{Package}\" with version \"{Version}\" has no dll or xml files", package, packageVersion);
                    continue;
                }

                foreach (string item in selectedItems)
                {
                    string packagePath = Path.Combine(path, item);
                    packageReader.ExtractFile(item, packagePath, NugetNullLogger.Instance);

                    if (packagePath.EndsWith(".dll", true, CultureInfo.InvariantCulture))
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
