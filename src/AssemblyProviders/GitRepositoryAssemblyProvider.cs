using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.AssemblyProviders
{
    public sealed class GitRepositoryAssemblyProvider : IAssemblyProvider
    {
        public string Name { get; init; } = "git";

        private readonly ILogger<GitRepositoryAssemblyProvider> _logger;
        private readonly string _repositoryPath;
        private readonly string _repositoryUrl;
        private readonly LocalProjectAssemblyProvider _localProjectAssemblyProvider;

        public GitRepositoryAssemblyProvider(IConfiguration configuration, ILogger<GitRepositoryAssemblyProvider>? gitAssemblyProviderLogger = null, ILogger<LocalProjectAssemblyProvider>? localProjectAssemblyProviderLogger = null)
        {
            _logger = gitAssemblyProviderLogger ?? NullLoggerFactory.Instance.CreateLogger<GitRepositoryAssemblyProvider>();
            _repositoryPath = configuration.GetValue("repository:path", "src")!;
            _localProjectAssemblyProvider = new LocalProjectAssemblyProvider(_repositoryPath, localProjectAssemblyProviderLogger);
            _repositoryUrl = configuration.GetValue<string>("repository:url")!;
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
            ProcessStartInfo startInfo = new("git", $"clone {_repositoryUrl} {_repositoryPath}")
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
                throw new InvalidOperationException("Failed to pull repository.");
            }
        }
    }
}
