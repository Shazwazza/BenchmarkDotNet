using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Detectors;
using BenchmarkDotNet.Helpers;
using BenchmarkDotNet.Portability;
using JetBrains.Annotations;

namespace BenchmarkDotNet.Loggers
{
    /// <summary>
    /// A logger implementation that uses ANSI escape codes for rich console output with colors, formatting,
    /// and in-place updates for progress. This logger provides enhanced visual experience for benchmark results
    /// using ANSI escape codes directly without external dependencies.
    ///
    /// Features:
    /// - Colorized output based on LogKind (Error = Red, Warning = Yellow, etc.)
    /// - Support for in-place updates to avoid line duplication during progress reporting
    /// - Graceful fallback to standard console output when ANSI codes are unavailable
    /// - Unicode support detection and handling
    /// - No external dependencies beyond standard .NET libraries
    ///
    /// Usage:
    /// var logger = new AnsiConsoleLogger();
    /// // Or with custom options:
    /// var logger = new AnsiConsoleLogger(unicodeSupport: true, enableLiveUpdates: true);
    /// </summary>
    [PublicAPI]
    public sealed class AnsiConsoleLogger : ILogger
    {
        private readonly bool unicodeSupport;
        private readonly bool enableLiveUpdates;
        private readonly Dictionary<LogKind, AnsiStyle> styleScheme;
        private readonly object lockObject = new object();

        // Track if we're in a live update context to avoid conflicts
        private bool isInLiveUpdate = false;
        private string lastProgressMessage = string.Empty;

        private static readonly bool AnsiSupported = CheckAnsiSupport();

        /// <summary>
        /// Creates a new AnsiConsoleLogger instance with default settings.
        /// </summary>
        public AnsiConsoleLogger() : this(unicodeSupport: false, enableLiveUpdates: true, styleScheme: null)
        {
        }

        /// <summary>
        /// Creates a new AnsiConsoleLogger instance with custom settings.
        /// </summary>
        /// <param name="unicodeSupport">Enable unicode character support</param>
        /// <param name="enableLiveUpdates">Enable in-place updates for progress messages</param>
        /// <param name="styleScheme">Custom style scheme mapping LogKind to ANSI styles</param>
        [PublicAPI]
        public AnsiConsoleLogger(bool unicodeSupport = false, bool enableLiveUpdates = true, Dictionary<LogKind, AnsiStyle>? styleScheme = null)
        {
            this.unicodeSupport = unicodeSupport;
            this.enableLiveUpdates = enableLiveUpdates && AnsiSupported;
            this.styleScheme = styleScheme ?? CreateDefaultStyleScheme();
        }

        /// <summary>
        /// Unique identifier for this logger implementation.
        /// </summary>
        public string Id => nameof(AnsiConsoleLogger);

        /// <summary>
        /// Priority of this logger. Higher priority loggers are preferred when multiple loggers with the same Id exist.
        /// Unicode-enabled instances have higher priority.
        /// </summary>
        public int Priority => unicodeSupport ? 2 : 1;

        /// <summary>
        /// Writes text with the specified log kind without adding a newline.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Write(LogKind logKind, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (lockObject)
            {
                WriteInternal(logKind, text, addNewLine: false);
            }
        }

        /// <summary>
        /// Writes an empty line.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void WriteLine()
        {
            lock (lockObject)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Writes text with the specified log kind and adds a newline.
        /// Supports in-place updates for progress-like messages when enabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void WriteLine(LogKind logKind, string text)
        {
            lock (lockObject)
            {
                // Handle in-place updates for progress messages
                if (enableLiveUpdates && ShouldUpdateInPlace(logKind, text))
                {
                    WriteProgressUpdate(logKind, text);
                }
                else
                {
                    WriteInternal(logKind, text, addNewLine: true);
                }
            }
        }

        /// <summary>
        /// Flushes any buffered output.
        /// </summary>
        public void Flush()
        {
            // Console typically auto-flushes, but we'll ensure completion of live updates
            lock (lockObject)
            {
                if (isInLiveUpdate)
                {
                    // Complete the current line
                    Console.WriteLine();
                    isInLiveUpdate = false;
                    lastProgressMessage = string.Empty;
                }
            }
        }

        private void WriteInternal(LogKind logKind, string text, bool addNewLine)
        {
            if (!unicodeSupport)
            {
                text = text.ToAscii();
            }

            if (!AnsiSupported)
            {
                // Fallback to console output without ANSI codes
                if (addNewLine)
                    Console.WriteLine(text);
                else
                    Console.Write(text);
                return;
            }

            try
            {
                var style = GetStyle(logKind);
                var styledText = style.Apply(text);

                if (addNewLine)
                {
                    Console.WriteLine(styledText);
                }
                else
                {
                    Console.Write(styledText);
                }
            }
            catch (Exception)
            {
                // Fallback to plain text if styling fails
                if (addNewLine)
                    Console.WriteLine(text);
                else
                    Console.Write(text);
            }
        }

        private void WriteProgressUpdate(LogKind logKind, string text)
        {
            try
            {
                if (AnsiSupported)
                {
                    // Clear the previous progress line if it exists
                    if (isInLiveUpdate && !string.IsNullOrEmpty(lastProgressMessage))
                    {
                        // Move cursor to beginning of line and clear it
                        Console.Write("\r" + new string(' ', Math.Min(lastProgressMessage.Length, Console.WindowWidth - 1)) + "\r");
                    }

                    var style = GetStyle(logKind);
                    var styledText = style.Apply(text);
                    Console.Write(styledText);

                    isInLiveUpdate = true;
                    lastProgressMessage = text;
                }
                else
                {
                    // Fallback to regular line output
                    WriteInternal(logKind, text, addNewLine: true);
                }
            }
            catch (Exception)
            {
                // Fallback to regular output if live update fails
                WriteInternal(logKind, text, addNewLine: true);
            }
        }

        private bool ShouldUpdateInPlace(LogKind logKind, string text)
        {
            // Update in-place for statistics and progress-like messages
            return logKind == LogKind.Statistic ||
                   text.Contains("...") ||
                   text.Contains("Progress") ||
                   text.Contains("Running") ||
                   text.Contains("%");
        }

        private AnsiStyle GetStyle(LogKind logKind)
        {
            return styleScheme.TryGetValue(logKind, out var style) ? style : AnsiStyle.Default;
        }

        private static Dictionary<LogKind, AnsiStyle> CreateDefaultStyleScheme()
        {
            return new Dictionary<LogKind, AnsiStyle>
            {
                { LogKind.Default, new AnsiStyle(AnsiColor.Silver) },
                { LogKind.Help, new AnsiStyle(AnsiColor.Green) },
                { LogKind.Header, new AnsiStyle(AnsiColor.Magenta, bold: true) },
                { LogKind.Result, new AnsiStyle(AnsiColor.DarkCyan, bold: true) },
                { LogKind.Statistic, new AnsiStyle(AnsiColor.Cyan) },
                { LogKind.Info, new AnsiStyle(AnsiColor.Yellow) },
                { LogKind.Error, new AnsiStyle(AnsiColor.Red, bold: true) },
                { LogKind.Warning, new AnsiStyle(AnsiColor.Yellow, bold: true) },
                { LogKind.Hint, new AnsiStyle(AnsiColor.DarkCyan) }
            };
        }

        [PublicAPI]
        public static Dictionary<LogKind, AnsiStyle> CreateGrayScheme()
        {
            var styleScheme = new Dictionary<LogKind, AnsiStyle>();
            foreach (LogKind logKind in Enum.GetValues(typeof(LogKind)))
            {
                styleScheme[logKind] = new AnsiStyle(AnsiColor.Silver);
            }
            return styleScheme;
        }

        private static bool CheckAnsiSupport()
        {
            try
            {
                // Check if we can use ANSI codes
                // Avoid using on platforms that don't support them well
                if (OsDetector.IsAndroid() || OsDetector.IsIOS() || RuntimeInformation.IsWasm || OsDetector.IsTvOS())
                {
                    return false;
                }

                // Check if console supports colors (similar to ConsoleLogger logic)
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Represents ANSI escape sequence colors.
    /// </summary>
    public enum AnsiColor
    {
        Black = 30,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        Silver = 37,
        DarkGray = 90,
        BrightRed = 91,
        BrightGreen = 92,
        BrightYellow = 93,
        BrightBlue = 94,
        BrightMagenta = 95,
        BrightCyan = 96,
        White = 97,
        DarkCyan = 36  // Alias for better compatibility with ConsoleLogger colors
    }

    /// <summary>
    /// Represents an ANSI style with color and formatting options.
    /// </summary>
    public readonly struct AnsiStyle
    {
        public static readonly AnsiStyle Default = new AnsiStyle(AnsiColor.Silver);

        private const string AnsiReset = "\u001b[0m";
        private const string AnsiBold = "\u001b[1m";

        public AnsiColor Color { get; }
        public bool Bold { get; }

        public AnsiStyle(AnsiColor color, bool bold = false)
        {
            Color = color;
            Bold = bold;
        }

        public string Apply(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var colorCode = $"\u001b[{(int)Color}m";
            var boldCode = Bold ? AnsiBold : string.Empty;

            return $"{colorCode}{boldCode}{text}{AnsiReset}";
        }
    }
}