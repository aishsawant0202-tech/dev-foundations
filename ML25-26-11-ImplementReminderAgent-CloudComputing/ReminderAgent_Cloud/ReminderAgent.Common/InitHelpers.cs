using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ReminderAgent.Common;

/// <summary>Shared startup helpers for configuration and logging, used by ReminderAgent.Cloud.</summary>
public static class InitHelpers
{
    #region Configuration

    /// <summary>
    /// Loads appsettings.json (required) + appsettings.dev.json (optional),
    /// then layers in command-line args and environment variables.
    /// Same file convention already used in ReminderAgent.Cloud/Program.cs.
    /// </summary>
    public static IConfigurationRoot InitConfiguration(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

        if (args is not null)
            builder.AddCommandLine(args);

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    #endregion

    #region Logging

    /// <summary>Creates debug-only logging, levels driven by the "Logging" config section.</summary>
    /// <remarks>No console provider — the console is the chat REPL; logs go to Debug Output instead.</remarks>
    public static ILoggerFactory InitLogging(IConfigurationRoot configRoot)
    {
        return LoggerFactory.Create(logBuilder =>
        {
            logBuilder.AddConfiguration(configRoot.GetSection("Logging"));
            logBuilder.AddDebug();
        });
    }

    #endregion
}
