using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Infrastructure;
using System;
using System.IO;
using System.Linq;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Contains unit tests for validating the functionality of the FileLogger class.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FileLoggerTests
    {
        /// <summary>
        /// Directory where log files are expected to be created.
        /// </summary>
        private static readonly string LogDirectory =
            Path.Combine(AppContext.BaseDirectory, "Logs");
        /// <summary>
        /// Initializes test setup by writing a warmup log entry.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            FileLogger.Info("FileLoggerTests warmup");
        }
        /// <summary>
        /// Cleans up resources after each test execution.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
           
        }

        /// <summary>
        // /// Verifies that the Logs directory is created on first use of FileLogger.
        // /// </summary> 
        [TestMethod]
        public void FileLogger_CreatesLogsDirectory_OnFirstUse()
        {
            FileLogger.Info($"dir-test-{Guid.NewGuid()}");

            Assert.IsTrue(Directory.Exists(LogDirectory),
                "Logs directory must exist after first use");
        }
        /// <summary>
        /// Verifies that the log file is created with the correct date-based naming format.
        /// </summary>
        [TestMethod]
        public void FileLogger_CreatesDateBasedLogFile()
        {
            FileLogger.Info($"file-name-test-{Guid.NewGuid()}");

            var today    = DateTime.Now.ToString("yyyy-MM-dd");
            var expected = Path.Combine(LogDirectory, $"app-{today}.log");

            Assert.IsTrue(File.Exists(expected),
                $"Log file must be named app-{today}.log");
        }
        /// <summary>
        /// Verifies that an INFO message is written to the log file.
        /// </summary>
        
        [TestMethod]
        public void Info_WritesMessage_ToLogFile()
        {
            var unique = $"INFO-MSG-{Guid.NewGuid()}";
            FileLogger.Info(unique);

            Assert.IsTrue(ReadTodaysLog().Contains(unique),
                $"Log must contain the unique message: {unique}");
        }
        /// <summary>
        /// Verifies that a WARNING message is written to the log file.
        /// </summary>
        [TestMethod]
        public void Warning_WritesMessage_ToLogFile()
        {
            var unique = $"WARN-MSG-{Guid.NewGuid()}";
            FileLogger.Warning(unique);

            Assert.IsTrue(ReadTodaysLog().Contains(unique),
                $"Log must contain the unique message: {unique}");
        }
        /// <summary>
        /// Verifies that an ERROR message is written to the log file.
        /// </summary>
        [TestMethod]
        public void Error_WritesMessage_ToLogFile()
        {
            var unique = $"ERR-MSG-{Guid.NewGuid()}";
            FileLogger.Error(unique);

            Assert.IsTrue(ReadTodaysLog().Contains(unique),
                $"Log must contain the unique message: {unique}");
        }

        /// <summary>
        // /// Verifies that INFO log entries contain the correct level label.
        // /// </summary>
        [TestMethod]
        public void Info_WritesINFO_LevelLabel()
        {
            var unique = $"info-label-{Guid.NewGuid()}";
            FileLogger.Info(unique);

            var line = GetLineContaining(unique);
            Assert.IsNotNull(line, "Could not find the log line for this test");
            StringAssert.Contains(line, "INFO");
        }
        /// <summary>
        /// Verifies that WARNING log entries contain the correct level label.
        /// </summary>
        [TestMethod]
        public void Warning_WritesWARNING_LevelLabel()
        {
            var unique = $"warn-label-{Guid.NewGuid()}";
            FileLogger.Warning(unique);

            var line = GetLineContaining(unique);
            Assert.IsNotNull(line, "Could not find the log line for this test");
            StringAssert.Contains(line, "WARNING");
        }
        /// <summary>
        /// Verifies that ERROR log entries contain the correct level label.
        /// </summary>
        [TestMethod]
        public void Error_WritesERROR_LevelLabel()
        {
            var unique = $"err-label-{Guid.NewGuid()}";
            FileLogger.Error(unique);

            var line = GetLineContaining(unique);
            Assert.IsNotNull(line, "Could not find the log line for this test");
            StringAssert.Contains(line, "ERROR");
        }

        /// <summary>
        /// Verifies that each log entry contains today's date as a timestamp.
        /// </summary>
        [TestMethod]
        public void Write_LogEntry_ContainsTodaysDate()
        {
            var unique = $"timestamp-{Guid.NewGuid()}";
            FileLogger.Info(unique);

            var line  = GetLineContaining(unique);
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            Assert.IsNotNull(line, "Could not find the log line for this test");
            StringAssert.Contains(line, today);
        }

        /// <summary>
        /// Verifies that multiple log entries are appended in the correct order.
        /// </summary>
        [TestMethod]
        public void Write_MultipleEntries_AppendedInOrder()
        {
            var first  = $"FIRST-{Guid.NewGuid()}";
            var second = $"SECOND-{Guid.NewGuid()}";
            var third  = $"THIRD-{Guid.NewGuid()}";

            FileLogger.Info(first);
            FileLogger.Warning(second);
            FileLogger.Error(third);

            var content   = ReadTodaysLog();
            int firstIdx  = content.IndexOf(first,  StringComparison.Ordinal);
            int secondIdx = content.IndexOf(second, StringComparison.Ordinal);
            int thirdIdx  = content.IndexOf(third,  StringComparison.Ordinal);

            Assert.IsTrue(firstIdx  >= 0, "First entry must be in the log");
            Assert.IsTrue(secondIdx >= 0, "Second entry must be in the log");
            Assert.IsTrue(thirdIdx  >= 0, "Third entry must be in the log");
            Assert.IsTrue(firstIdx  < secondIdx, "First must appear before second");
            Assert.IsTrue(secondIdx < thirdIdx,  "Second must appear before third");
        }

        /// <summary>
        // /// Resolves the full file path of today's log file from possible locations.
        // /// </summary>
        // /// <returns>Full path to the log file if found; otherwise, an empty string.</returns>
        private static string ResolveLogFilePath()
        {
            var today    = DateTime.Now.ToString("yyyy-MM-dd");
            var fileName = $"app-{today}.log";

            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Logs", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Logs", fileName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Logs", fileName),
            };

            foreach (var candidate in candidates)
            {
                var full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return string.Empty;
        }
        /// <summary>
        /// Reads the content of today's log file.
        /// </summary>
        /// <returns>Log file content as a string, or empty if not found.</returns> 
        private static string ReadTodaysLog()
        {
            var path = ResolveLogFilePath();
            return string.IsNullOrEmpty(path) ? string.Empty : File.ReadAllText(path);
        }
        /// <summary>
        /// Retrieves the first log line containing the specified unique string.
        /// </summary>
        /// <param name="unique">Unique identifier to search for in the log.</param>
        /// <returns>The matching log line, or null if not found.</returns>
        private static string? GetLineContaining(string unique)
        {
            var path = ResolveLogFilePath();
            if (string.IsNullOrEmpty(path)) return null;
            return File.ReadAllLines(path)
                .FirstOrDefault(line => line.Contains(unique));
        }
    }
}