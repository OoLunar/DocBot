using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using NugetNullLogger = NuGet.Common.NullLogger;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class NugetAssemblyProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NugetAssemblyProvider> _logger;
        private readonly ISettings _settings;
        private readonly SourceRepositoryProvider _sourceRepositoryProvider;
        private readonly NuGetFramework _targettedFramework;

        public NugetAssemblyProvider(IConfiguration configuration, ILogger<NugetAssemblyProvider>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            _configuration = configuration;
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<NugetAssemblyProvider>();
            _settings = Settings.LoadDefaultSettings(null);
            _sourceRepositoryProvider = new(new PackageSourceProvider(_settings), Repository.Provider.GetCoreV3());
            _targettedFramework = NuGetFramework.ParseFolder("net8.0");
        }

        public async ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            Dictionary<string, NuGetVersion> requestedAssemblies = _configuration.GetSection("nuget:packages").GetChildren().ToDictionary(section => section.Key, section => NuGetVersion.Parse(section.Value!));
            if (requestedAssemblies.Count == 0)
            {
                _logger.LogWarning("No packages specified in configuration");
                return Enumerable.Empty<Assembly>();
            }

            using SourceCacheContext cacheContext = new();
            HashSet<SourcePackageDependencyInfo> availablePackages = new(PackageIdentityComparer.Default);
            foreach (KeyValuePair<string, NuGetVersion> requestedAssembly in requestedAssemblies)
            {
                await GetPackageDependenciesAsync(new PackageIdentity(requestedAssembly.Key, requestedAssembly.Value), _targettedFramework, cacheContext, _sourceRepositoryProvider.GetRepositories(), availablePackages);
            }

            // I'm not going to pretend like I know what's going on here.
            // Checkout https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries
            PackageResolver resolver = new();
            FrameworkReducer frameworkReducer = new();
            PackagePathResolver packagePathResolver = new(Path.GetFullPath(_configuration.GetValue("nuget:path", "packages")!));
            PackageResolverContext resolverContext = new(DependencyBehavior.Highest, requestedAssemblies.Keys, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>(), Enumerable.Empty<PackageIdentity>(), availablePackages, _sourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource), NugetNullLogger.Instance);
            PackageExtractionContext packageExtractionContext = new(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(_settings, NugetNullLogger.Instance), NugetNullLogger.Instance) { CopySatelliteFiles = false };
            foreach (SourcePackageDependencyInfo? packageToInstall in resolver.Resolve(resolverContext, CancellationToken.None)
                                                                              .Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p))))
            {
                PackageReaderBase packageReader;
                string installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
                if (installedPath == null)
                {
                    DownloadResource downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
                    DownloadResourceResult downloadResult = await downloadResource.GetDownloadResourceResultAsync(packageToInstall, new PackageDownloadContext(cacheContext), SettingsUtility.GetGlobalPackagesFolder(_settings), NugetNullLogger.Instance, CancellationToken.None);
                    await PackageExtractor.ExtractPackageAsync(downloadResult.PackageSource, downloadResult.PackageStream, packagePathResolver, packageExtractionContext, CancellationToken.None);
                    packageReader = downloadResult.PackageReader;
                }
                else
                {
                    packageReader = new PackageFolderReader(installedPath);
                }

                IEnumerable<FrameworkSpecificGroup> libItems = packageReader.GetLibItems();
                NuGetFramework? nearest = frameworkReducer.GetNearest(_targettedFramework, libItems.Select(x => x.TargetFramework));
                IEnumerable<FrameworkSpecificGroup> frameworkItems = packageReader.GetFrameworkItems();
                nearest = frameworkReducer.GetNearest(_targettedFramework, frameworkItems.Select(x => x.TargetFramework));
            }

            List<string> assemblyPaths = new();
            string packagePath = Path.GetFullPath(_configuration.GetValue("nuget:path", "packages")!);
            foreach (string file in Directory.EnumerateFiles(packagePath, "*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                string fileExtension = Path.GetExtension(file);
                if (fileExtension == ".dll" && fileExtension == ".xml")
                {
                    string fileName = Path.GetFileName(file);
                    File.Move(file, Path.Combine(packagePath, Path.GetFileName(file)));
                    if (requestedAssemblies.Keys.Any(x => $"{x}.dll" == fileName))
                    {
                        assemblyPaths.Add(Path.Combine(packagePath, fileName));
                    }
                }
            }

            //foreach (string directory in Directory.EnumerateDirectories(packagePath))
            //{
            //    Directory.Delete(directory, true);
            //}

            return assemblyPaths.Count == 0
                ? Enumerable.Empty<Assembly>()
                : await new LocalFileAssemblyProvider(assemblyPaths).GetAssembliesAsync();
        }

        private static async Task GetPackageDependenciesAsync(PackageIdentity package, NuGetFramework framework, SourceCacheContext cacheContext, IEnumerable<SourceRepository> repositories, ISet<SourcePackageDependencyInfo> availablePackages)
        {
            if (availablePackages.Contains(package))
            {
                return;
            }

            foreach (SourceRepository sourceRepository in repositories)
            {
                DependencyInfoResource dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                SourcePackageDependencyInfo dependencyInfo = await dependencyInfoResource.ResolvePackage(package, framework, cacheContext, NugetNullLogger.Instance, CancellationToken.None);
                if (dependencyInfo == null)
                {
                    continue;
                }

                availablePackages.Add(dependencyInfo);
                foreach (PackageDependency? dependency in dependencyInfo.Dependencies)
                {
                    await GetPackageDependenciesAsync(new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion), framework, cacheContext, repositories, availablePackages);
                }
            }
        }
    }
}
