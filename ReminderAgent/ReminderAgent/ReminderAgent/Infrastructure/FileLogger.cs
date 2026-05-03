using System;
using System.IO;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// Provides a simple file-based logging mechanism that writes
    /// timestamped log entries to a daily log file.
    /// </summary>
    public static class FileLogger
    {
        private static readonly string LogDirectory = "Logs";
        private static readonly string LogFilePath;

        /// <summary>
        /// Initializes the <see cref="FileLogger"/> class.
        /// Ensures that the log directory exists and prepares the daily log file path.
        /// </summary>
        static FileLogger()
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            LogFilePath = Path.Combine(
                LogDirectory,
                $"app-{DateTime.Now:yyyy-MM-dd}.log"
            );
        }

        /// <summary>
        /// Writes a formatted log entry to the daily log file.
        /// </summary>
        /// <param name="level">The severity level of the log (e.g., INFO, WARNING, ERROR).</param>
        /// <param name="message">The message to log.</param>
        public static void Write(string level, string message)
        {
            try
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {level,-7} | {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOGGER FAILED: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes an informational log entry.
        /// </summary>
        /// <param name="message">The informational message.</param>
        public static void Info(string message)    => Write("INFO",    message);

        /// <summary>
        /// Writes a warning log entry.
        /// </summary>
        /// <param name="message">The warning message.</param>
        public static void Warning(string message) => Write("WARNING", message);

        /// <summary>
        /// Writes an error log entry.
        /// </summary>
        /// <param name="message">The error message.</param>
        public static void Error(string message)   => Write("ERROR",   message);
    }
}