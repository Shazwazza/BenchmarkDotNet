using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Detectors;
using BenchmarkDotNet.Helpers;
using BenchmarkDotNet.Portability;
using JetBrains.Annotations;
using Spectre.Console;

namespace BenchmarkDotNet.Loggers
{
    /// <summary>
    /// A logger implementation that uses Spectre.Console for rich console output with colors, formatting,
    /// and in-place updates for progress. This logger provides enhanced visual experience for benchmark results.
    ///
    /// Features:
    /// - Colorized output based on LogKind (Error = Red, Warning = Yellow, etc.)
    /// - Support for in-place updates to avoid line duplication during progress reporting
    /// - Graceful fallback to standard console output when Spectre.Console features are unavailable
    /// - Unicode support detection and handling
    ///
    /// Usage:
    /// var logger = new SpectreConsoleLogger();
    /// // Or with custom options:
    /// var logger = new SpectreConsoleLogger(unicodeSupport: true, enableLiveUpdates: true);
    /// </summary>
    [PublicAPI]
    public sealed class SpectreConsoleLogger : ILogger
    {
        private readonly bool unicodeSupport;
        private readonly bool enableLiveUpdates;
        private readonly Dictionary<LogKind, Style> styleScheme;
        private readonly object lockObject = new object();

        // Track if we're in a live update context to avoid conflicts
        private bool isInLiveUpdate = false;
        private string lastProgressMessage = string.Empty;

        private static readonly bool SpectreConsoleSupported = CheckSpectreConsoleSupport();

        /// <summary>
        /// Creates a new SpectreConsoleLogger instance with default settings.
        /// </summary>
        public SpectreConsoleLogger() : this(unicodeSupport: false, enableLiveUpdates: true, styleScheme: null)
        {
        }

        /// <summary>
        /// Creates a new SpectreConsoleLogger instance with custom settings.
        /// </summary>
        /// <param name="unicodeSupport">Enable unicode character support</param>
        /// <param name="enableLiveUpdates">Enable in-place updates for progress messages</param>
        /// <param name="styleScheme">Custom style scheme mapping LogKind to Spectre.Console styles</param>
        [PublicAPI]
        public SpectreConsoleLogger(bool unicodeSupport = false, bool enableLiveUpdates = true, Dictionary<LogKind, Style>? styleScheme = null)
        {
            this.unicodeSupport = unicodeSupport;
            this.enableLiveUpdates = enableLiveUpdates && SpectreConsoleSupported;
            this.styleScheme = styleScheme ?? CreateDefaultStyleScheme();
        }

        /// <summary>
        /// Unique identifier for this logger implementation.
        /// </summary>
        public string Id => nameof(SpectreConsoleLogger);

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
                if (SpectreConsoleSupported)
                {
                    AnsiConsole.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }
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
            // Spectre.Console and Console.Out typically auto-flush, but we'll ensure completion
            lock (lockObject)
            {
                if (isInLiveUpdate)
                {
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

            if (!SpectreConsoleSupported)
            {
                // Fallback to console output without Spectre.Console
                if (addNewLine)
                    Console.WriteLine(text);
                else
                    Console.Write(text);
                return;
            }

            try
            {
                var style = GetStyle(logKind);
                var escapedText = EscapeMarkup(text);

                if (addNewLine)
                {
                    AnsiConsole.WriteLine(escapedText, style);
                }
                else
                {
                    AnsiConsole.Write(escapedText, style);
                }
            }
            catch (Exception)
            {
                // Fallback to plain text if markup processing fails
                if (addNewLine)
                    AnsiConsole.WriteLine(text);
                else
                    AnsiConsole.Write(text);
            }
        }

        private void WriteProgressUpdate(LogKind logKind, string text)
        {
            try
            {
                if (SpectreConsoleSupported)
                {
                    // Clear the previous progress line if it exists
                    if (isInLiveUpdate && !string.IsNullOrEmpty(lastProgressMessage))
                    {
                        // Move cursor up and clear the line
                        AnsiConsole.Write("\r");
                        AnsiConsole.Write(new string(' ', Math.Min(lastProgressMessage.Length, Console.WindowWidth - 1)));
                        AnsiConsole.Write("\r");
                    }

                    var style = GetStyle(logKind);
                    var escapedText = EscapeMarkup(text);
                    AnsiConsole.Write(escapedText, style);

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

        private Style GetStyle(LogKind logKind)
        {
            return styleScheme.TryGetValue(logKind, out var style) ? style : Style.Plain;
        }

        private static string EscapeMarkup(string text)
        {
            // Escape Spectre.Console markup characters to prevent interpretation
            return text.Replace("[", "[[").Replace("]", "]]");
        }

        private static Dictionary<LogKind, Style> CreateDefaultStyleScheme()
        {
            return new Dictionary<LogKind, Style>
            {
                { LogKind.Default, new Style(Color.Silver) },
                { LogKind.Help, new Style(Color.Green) },
                { LogKind.Header, new Style(Color.Magenta1, decoration: Decoration.Bold) },
                { LogKind.Result, new Style(Color.DarkCyan, decoration: Decoration.Bold) },
                { LogKind.Statistic, new Style(Color.Cyan1) },
                { LogKind.Info, new Style(Color.Yellow3) },
                { LogKind.Error, new Style(Color.Red, decoration: Decoration.Bold) },
                { LogKind.Warning, new Style(Color.Yellow, decoration: Decoration.Bold) },
                { LogKind.Hint, new Style(Color.DarkCyan) }
            };
        }

        [PublicAPI]
        [CLSCompliant(false)]
        public static Dictionary<LogKind, Style> CreateGrayScheme()
        {
            var styleScheme = new Dictionary<LogKind, Style>();
            foreach (LogKind logKind in Enum.GetValues(typeof(LogKind)))
            {
                styleScheme[logKind] = new Style(Color.Silver);
            }
            return styleScheme;
        }

        private static bool CheckSpectreConsoleSupport()
        {
            try
            {
                // Check if we can use Spectre.Console features
                // Avoid using on platforms that don't support it well
                if (OsDetector.IsAndroid() || OsDetector.IsIOS() || RuntimeInformation.IsWasm || OsDetector.IsTvOS())
                {
                    return false;
                }

                // Test basic Spectre.Console functionality
                _ = AnsiConsole.Profile;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}