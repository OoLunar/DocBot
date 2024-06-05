using System;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OoLunar.DocBot.Configuration;
using OoLunar.DocBot.SymbolProviders;
using OoLunar.DocBot.SymbolProviders.Projects;
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
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(serviceProvider =>
            {
                ConfigurationBuilder configurationBuilder = new();
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
                configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
                configurationBuilder.AddEnvironmentVariables("DocBot__");
                configurationBuilder.AddCommandLine(args);

                return configurationBuilder.Build();
            });

            services.AddSingleton(serviceProvider =>
            {
                IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
                DocBotConfiguration? docBot = configuration.Get<DocBotConfiguration>();
                if (docBot is null)
                {
                    Console.WriteLine("No configuration found! Please modify the config file, set environment variables or pass command line arguments. Exiting...");
                    Environment.Exit(1);
                }

                return docBot;
            });

            services.AddLogging(logging =>
            {
                IServiceProvider serviceProvider = logging.Services.BuildServiceProvider();
                DocBotConfiguration docBot = serviceProvider.GetRequiredService<DocBotConfiguration>();
                SerilogLoggerConfiguration serilogLoggerConfiguration = new();
                serilogLoggerConfiguration.MinimumLevel.Is(docBot.Logger.LogLevel);
                serilogLoggerConfiguration.WriteTo.Console(
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate: docBot.Logger.Format,
                    theme: AnsiConsoleTheme.Code
                );

                serilogLoggerConfiguration.WriteTo.File(
                    formatProvider: CultureInfo.InvariantCulture,
                    path: $"{docBot.Logger.Path}/{docBot.Logger.FileName}.log",
                    rollingInterval: docBot.Logger.RollingInterval,
                    outputTemplate: docBot.Logger.Format
                );

                // Sometimes the user/dev needs more or less information about a speific part of the bot
                // so we allow them to override the log level for a specific namespace.
                if (docBot.Logger.Overrides.Count > 0)
                {
                    foreach ((string key, LogEventLevel value) in docBot.Logger.Overrides)
                    {
                        serilogLoggerConfiguration.MinimumLevel.Override(key, value);
                    }
                }

                logging.AddSerilog(serilogLoggerConfiguration.CreateLogger());
            });

            services.AddSingleton<ISymbolProvider>((serviceProvider) =>
            {
                DocBotConfiguration docBot = serviceProvider.GetRequiredService<DocBotConfiguration>();
                if (string.IsNullOrWhiteSpace(docBot.SelectedSymbolProvider))
                {
                    serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("No symbol provider selected! Exiting...");
                    Environment.Exit(1);
                }

                return new ProjectSymbolProvider(serviceProvider.GetRequiredService<IConfiguration>().GetSection(docBot.SelectedSymbolProvider));
            });

            services.AddSingleton(serviceProvider =>
            {
                DocBotConfiguration docBot = serviceProvider.GetRequiredService<DocBotConfiguration>();
                if (docBot.Discord is null || string.IsNullOrWhiteSpace(docBot.Discord.Token))
                {
                    serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("Discord token is not set! Exiting...");
                    Environment.Exit(1);
                }

                DiscordClientBuilder clientBuilder = DiscordClientBuilder.CreateDefault(docBot.Discord.Token, TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents | DiscordIntents.MessageContents, services);
                clientBuilder.ConfigureLogging(loggerBuilder => loggerBuilder.AddSerilog());
                return clientBuilder.Build();
            });

            // Almost start the program
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            DocBotConfiguration docBot = serviceProvider.GetRequiredService<DocBotConfiguration>();
            DiscordClient discordClient = serviceProvider.GetRequiredService<DiscordClient>();
            ISymbolProvider symbolProvider = discordClient.ServiceProvider.GetRequiredService<ISymbolProvider>();
            await symbolProvider.LoadAsync();

            // Register extensions here since these involve asynchronous operations
            CommandsExtension commandsExtension = discordClient.UseCommands(new CommandsConfiguration()
            {
                DebugGuildId = docBot.Discord.GuildId
            });

            // Add all commands by scanning the current assembly
            commandsExtension.AddCommands(typeof(Program).Assembly);

            // Add text commands (h!ping) with a custom prefix, keeping all the other processors in their default state
            await commandsExtension.AddProcessorsAsync(new TextCommandProcessor(new()
            {
                PrefixResolver = new DefaultPrefixResolver(true, [docBot.Discord.Prefix]).ResolvePrefixAsync
            }));

            // Connect to Discord
            await discordClient.ConnectAsync();

            // Wait for commands
            await Task.Delay(-1);
        }
    }
}
