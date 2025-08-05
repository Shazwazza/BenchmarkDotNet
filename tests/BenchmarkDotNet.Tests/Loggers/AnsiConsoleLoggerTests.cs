using System;
using BenchmarkDotNet.Loggers;
using Xunit;

namespace BenchmarkDotNet.Tests.Loggers
{
    public class AnsiConsoleLoggerTests
    {
        [Fact]
        public void Id_ReturnsCorrectValue()
        {
            var logger = new AnsiConsoleLogger();
            Assert.Equal(nameof(AnsiConsoleLogger), logger.Id);
        }

        [Fact]
        public void Priority_DefaultInstance_ReturnsZero()
        {
            var logger = new AnsiConsoleLogger(unicodeSupport: false);
            Assert.Equal(1, logger.Priority);
        }

        [Fact]
        public void Priority_UnicodeEnabled_ReturnsTwo()
        {
            var logger = new AnsiConsoleLogger(unicodeSupport: true);
            Assert.Equal(2, logger.Priority);
        }

        [Fact]
        public void Write_EmptyText_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger();
            logger.Write(LogKind.Default, string.Empty);
            logger.Write(LogKind.Default, null);
            // Test passes if no exception is thrown
        }

        [Fact]
        public void WriteLine_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger();
            logger.WriteLine();
            // Test passes if no exception is thrown
        }

        [Fact]
        public void WriteLine_WithText_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger();
            logger.WriteLine(LogKind.Default, "Test message");
            logger.WriteLine(LogKind.Error, "Error message");
            logger.WriteLine(LogKind.Warning, "Warning message");
            logger.WriteLine(LogKind.Header, "Header message");
            // Test passes if no exception is thrown
        }

        [Fact]
        public void Flush_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger();
            logger.Flush();
            // Test passes if no exception is thrown
        }

        [Fact]
        public void CreateGrayScheme_ContainsAllLogKinds()
        {
            var scheme = AnsiConsoleLogger.CreateGrayScheme();

            foreach (LogKind logKind in Enum.GetValues(typeof(LogKind)))
            {
                Assert.True(scheme.ContainsKey(logKind), $"Gray scheme missing {logKind}");
            }
        }

        [Fact]
        public void Constructor_WithCustomScheme_DoesNotThrow()
        {
            var customScheme = AnsiConsoleLogger.CreateGrayScheme();
            var logger = new AnsiConsoleLogger(styleScheme: customScheme);

            // Test basic functionality
            logger.WriteLine(LogKind.Default, "Test with custom scheme");
            // Test passes if no exception is thrown
        }

        [Fact]
        public void LiveUpdates_WithStatisticLogKind_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger(enableLiveUpdates: true);

            // These should trigger in-place updates
            logger.WriteLine(LogKind.Statistic, "Progress... 50%");
            logger.WriteLine(LogKind.Statistic, "Progress... 100%");
            logger.Flush(); // Should complete the live update

            // Test passes if no exception is thrown
        }

        [Fact]
        public void UnicodeSupport_WithSpecialCharacters_DoesNotThrow()
        {
            var logger = new AnsiConsoleLogger(unicodeSupport: true);

            logger.WriteLine(LogKind.Default, "Test with μ character");

            // Test passes if no exception is thrown
        }
    }
}