using System;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace GuitarProToMidi
{
    class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = AppDomain.CurrentDomain.FriendlyName,
                Description = "Converts GuitarPro files to MIDI files."
            };
            app.HelpOption("-h|--help");
            app.VersionOption("--version",
                () =>
                    $@"Version {(Assembly.GetEntryAssembly() ?? throw new NullReferenceException("Unable to get entry assembly"))
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
            var inputFile = app.Argument("input_file", "Path to GuitarPro file");
            var verbose = app.Option("-v|--verbose", "Enable debug logs", CommandOptionType.NoValue);
            var outputFile = app.Option("-o|--output <output_file>", "Optional output file path",
                CommandOptionType.SingleValue);
            var force = app.Option("-f|--force", "Overwrite output file", CommandOptionType.NoValue);
            app.OnExecute(() =>
            {
                if (string.IsNullOrEmpty(inputFile.Value))
                {
                    app.ShowHint();
                    return 1;
                }

                ConfigureLogging(verbose.HasValue() ? LogLevel.Debug : LogLevel.Info);

                var gpFile = new GpFileParser(inputFile.Value);
                gpFile.CreateMidiFile(outputFile.Value(), force.HasValue());

                return 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException e)
            {
                Console.Error.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error: {e.Message}");
            }

            return 1;
        }

        private static void ConfigureLogging(LogLevel logLevel)
        {
            var config = new LoggingConfiguration();
            var logconsole = new ConsoleTarget
            {
                Name = "console",
                Layout = "${message}"
            };
            config.AddRule(logLevel, LogLevel.Fatal, logconsole);
            LogManager.Configuration = config;
        }
    }
}
