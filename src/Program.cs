using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OoLunar.DocBot.AssemblyProviders;
using OoLunar.DocBot.Configuration;
using OoLunar.DocBot.Events.EventHandlers;
using OoLunar.DocBot.GitHub;
using OoLunar.DocBot.Interactivity;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SerilogLoggerConfiguration = Serilog.LoggerConfiguration;

namespace OoLunar.DocBot
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(serviceProvider =>
            {
                ConfigurationBuilder configurationBuilder = new();
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
                // If the program is running in debug mode, add the debug config file
                configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
                configurationBuilder.AddEnvironmentVariables("DOCBOT__");
                configurationBuilder.AddCommandLine(args);

                IConfiguration configuration = configurationBuilder.Build();
                DocBotConfiguration? docBotConfiguration = configuration.Get<DocBotConfiguration>();
                if (docBotConfiguration is null)
                {
                    Console.WriteLine("No configuration found! Please modify the config file, set environment variables or pass command line arguments. Exiting...");
                    Environment.Exit(1);
                }

                return docBotConfiguration;
            });

            serviceCollection.AddLogging(logging =>
            {
                IServiceProvider serviceProvider = logging.Services.BuildServiceProvider();
                DocBotConfiguration docBotConfiguration = serviceProvider.GetRequiredService<DocBotConfiguration>();
                SerilogLoggerConfiguration serilogLoggerConfiguration = new();
                serilogLoggerConfiguration.MinimumLevel.Is(docBotConfiguration.Logger.LogLevel);
                serilogLoggerConfiguration.WriteTo.Console(
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate: docBotConfiguration.Logger.Format,
                    theme: AnsiConsoleTheme.Code
                );

                serilogLoggerConfiguration.WriteTo.File(
                    formatProvider: CultureInfo.InvariantCulture,
                    path: $"{docBotConfiguration.Logger.Path}/{docBotConfiguration.Logger.FileName}.log",
                    rollingInterval: docBotConfiguration.Logger.RollingInterval,
                    outputTemplate: docBotConfiguration.Logger.Format
                );

                // Sometimes the user/dev needs more or less information about a speific part of the bot
                // so we allow them to override the log level for a specific namespace.
                if (docBotConfiguration.Logger.Overrides.Count > 0)
                {
                    foreach ((string key, LogEventLevel value) in docBotConfiguration.Logger.Overrides)
                    {
                        serilogLoggerConfiguration.MinimumLevel.Override(key, value);
                    }
                }

                logging.AddSerilog(serilogLoggerConfiguration.CreateLogger());
            });

            serviceCollection.AddSingleton<HttpClient>((serviceProvider) => new(new GitHubRateLimitMessageHandler(new HttpClientHandler(), serviceProvider.GetRequiredService<ILogger<GitHubRateLimitMessageHandler>>()))
            {
                DefaultRequestHeaders = { { "User-Agent", serviceProvider.GetRequiredService<DocBotConfiguration>().HttpUserAgent } }
            });

            serviceCollection.AddSingleton<GitHubMetadataRetriever>();
            serviceCollection.AddSingleton((serviceProvider) =>
            {
                DocBotConfiguration docBotConfiguration = serviceProvider.GetRequiredService<DocBotConfiguration>();
                if (string.IsNullOrWhiteSpace(docBotConfiguration.AssemblyProviderName))
                {
                    throw new InvalidOperationException("No assembly provider was specified.");
                }

                foreach (Type type in typeof(Program).Assembly.GetTypes())
                {
                    if (type.IsAssignableTo(typeof(IAssemblyProvider)) // If the type implements IAssemblyProvider
                        && !type.IsAbstract // If the type has a concrete implementation
                        && ActivatorUtilities.CreateInstance(serviceProvider, type) is IAssemblyProvider assemblyProvider // If the service provider was able to create an instance of the type
                        && assemblyProvider.Name.Equals(docBotConfiguration.AssemblyProviderName, StringComparison.OrdinalIgnoreCase)) // If the assembly provider name matches the one specified in the configuration
                    {
                        return assemblyProvider;
                    }
                }

                throw new InvalidOperationException($"No assembly provider with the name {docBotConfiguration.AssemblyProviderName} was found.");
            });

            serviceCollection.AddSingleton<AssemblyProviderAsync>((serviceProvider) => serviceProvider.GetRequiredService<IAssemblyProvider>().GetAssembliesAsync);
            serviceCollection.AddSingleton<DocumentationProvider>();
            serviceCollection.AddSingleton<Procrastinator>();

            serviceCollection.AddSingleton(serviceProvider =>
            {
                DocBotConfiguration docBotConfiguration = serviceProvider.GetRequiredService<DocBotConfiguration>();
                if (docBotConfiguration.Discord is null || string.IsNullOrWhiteSpace(docBotConfiguration.Discord.Token))
                {
                    serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("Discord token is not set! Exiting...");
                    Environment.Exit(1);
                }

                DiscordClientBuilder clientBuilder = DiscordClientBuilder
                    .CreateDefault(docBotConfiguration.Discord.Token, TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents | DiscordIntents.MessageContents, serviceCollection)
                    .UseCommands((config, extension) =>
                    {
                        // Add all commands by scanning the current assembly
                        extension.AddCommands(typeof(Program).Assembly);

                        // Add all processors
                        TextCommandProcessor textCommandProcessor = new(new()
                        {
                            PrefixResolver = new DefaultPrefixResolver(true, docBotConfiguration.Discord.Prefix).ResolvePrefixAsync,
                            IgnoreBots = false
                        });

                        extension.AddProcessor(textCommandProcessor);
                    }, new CommandsConfiguration()
                    {
                        DebugGuildId = docBotConfiguration.Discord.DebugGuildId
                    })
                    .ConfigureEventHandlers(events =>
                    {
                        events.AddEventHandlers<LinkIssueEventHandlers>(ServiceLifetime.Singleton);
                        events.AddEventHandlers<Procrastinator>(ServiceLifetime.Singleton);
                    });
                clientBuilder.DisableDefaultLogging();
                return clientBuilder.Build();
            });

            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            DocBotConfiguration docBotConfiguration = serviceProvider.GetRequiredService<DocBotConfiguration>();
            if (docBotConfiguration.Discord.Prefix is null)
            {
                logger.LogCritical("No Discord prefix was set! Exiting...");
                Environment.Exit(1);
            }

            DiscordClient discordClient = serviceProvider.GetRequiredService<DiscordClient>();
            DocumentationProvider documentationProvider = discordClient.ServiceProvider.GetRequiredService<DocumentationProvider>();
            await documentationProvider.ReloadAsync();

            // Start the bot
            await discordClient.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}
