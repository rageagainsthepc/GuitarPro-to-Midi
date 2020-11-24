﻿using NLog;
using NLog.Config;
using NLog.Targets;

namespace GuitarProToMidi
{
    class Program
    {
        public static void Main(string[] args)
        {
            ConfigureLogging(LogLevel.Info);

            var gpFile = new GpFileParser(args[0]);

            gpFile.CreateMidiFile();
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
