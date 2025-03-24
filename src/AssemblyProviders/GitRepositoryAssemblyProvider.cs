using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OoLunar.DocBot.Configuration;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class GitRepositoryAssemblyProvider : IAssemblyProvider
    {
        public string Name { get; init; } = "Git";

        private readonly ILogger<GitRepositoryAssemblyProvider> _logger;
        private readonly string _repositoryPath;
        private readonly string _repositoryUrl;
        private readonly LocalProjectAssemblyProvider _localProjectAssemblyProvider;

        public GitRepositoryAssemblyProvider(DocBotConfiguration configuration, ILogger<GitRepositoryAssemblyProvider>? gitAssemblyProviderLogger = null, ILogger<LocalProjectAssemblyProvider>? localProjectAssemblyProviderLogger = null)
        {
            _logger = gitAssemblyProviderLogger ?? NullLoggerFactory.Instance.CreateLogger<GitRepositoryAssemblyProvider>();
            _repositoryPath = configuration.AssemblyProviders[Name].GetValue("path", "src")!;
            _repositoryUrl = configuration.AssemblyProviders[Name].GetValue<string>("url")!;

            Matcher ignoreProjectGlobs = new();
            foreach (string glob in configuration.AssemblyProviders[Name].GetSection("IgnoreProjectGlobs")?.Get<string[]>() ?? [])
            {
                ignoreProjectGlobs.AddInclude(glob);
            }

            _localProjectAssemblyProvider = new LocalProjectAssemblyProvider(_repositoryPath, ignoreProjectGlobs, localProjectAssemblyProviderLogger);
            if (string.IsNullOrWhiteSpace(_repositoryUrl))
            {
                throw new InvalidOperationException("Repository URL is required.");
            }
        }

        public async ValueTask<IEnumerable<Assembly>> GetAssembliesAsync()
        {
            if (!Directory.Exists(_repositoryPath))
            {
                _logger.LogInformation("Cloning repository {RepositoryUrl} to {RepositoryPath}.", _repositoryUrl, _repositoryPath);
                await CloneRepositoryAsync();
            }
            else
            {
                _logger.LogInformation("Pulling repository {RepositoryUrl} to {RepositoryPath}.", _repositoryUrl, _repositoryPath);
                await PullRepositoryAsync();
            }

            return await _localProjectAssemblyProvider.GetAssembliesAsync();
        }

        private async ValueTask CloneRepositoryAsync()
        {
            ProcessStartInfo startInfo = new("git", $"clone \"{_repositoryUrl}\" \"{_repositoryPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git process.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                _logger.LogCritical("Failed to clone repository: {Error}", await process.StandardError.ReadToEndAsync());
                throw new InvalidOperationException("Failed to clone repository.");
            }
        }

        private async ValueTask PullRepositoryAsync()
        {
            ProcessStartInfo startInfo = new("git", "pull")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _repositoryPath
            };

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git process.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                _logger.LogCritical("Failed to pull repository: {Error}", await process.StandardError.ReadToEndAsync());
                throw new InvalidOperationException("Failed to pull repository.");
            }
        }
    }
}
