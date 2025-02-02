using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Runtime.CompilerServices;

/*
 * Parts of this code are lifted from dotnet runtime:
 * https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs
 * 
 * That code is licensed as such:
 * The MIT License (MIT)

    Copyright (c) .NET Foundation and Contributors

    All rights reserved.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

namespace barzap.Code {

	public class OneLineLoggerFormatterOptions : ConsoleFormatterOptions {

	}

	/// <summary>
	/// a logger that prints messages on one line, with colors for each type of loggers. 
	/// one line logging makes it easier to grep logs. much of this code is pulled from the open source dotnet
	/// library, and then tweaked a bit to print on one line instead of two
	/// </summary>
    public class OneLineLogger : ConsoleFormatter, IDisposable {

        private readonly IDisposable? _OptionsReloadToken;
        private OneLineLoggerFormatterOptions _Options;

        public OneLineLogger(IOptionsMonitor<OneLineLoggerFormatterOptions> options)
            : base("OneLineLogger") {

            _OptionsReloadToken = options.OnChange((OneLineLoggerFormatterOptions options) => {
                _Options = options;
            });
            _Options = options.CurrentValue;
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) {
            string? msg = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && msg == null) {
                return;
            }

            textWriter.Write("[");

            Microsoft.Extensions.Logging.LogLevel logLevel = logEntry.LogLevel;
            string logLevelStr = GetLogLevelString(logLevel);
            ConsoleColors levelColors = GetLogLevelConsoleColors(logLevel);

            textWriter.WriteColoredMessage(logLevelStr, levelColors.Background, levelColors.Foreground);
            textWriter.Write(" ");

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ");
            textWriter.Write(timestamp);
            textWriter.Write("] ");

            textWriter.Write(logEntry.Category);
            textWriter.Write("[");
            textWriter.Write(logEntry.EventId.ToString());
            textWriter.Write("] ");

            textWriter.Write(msg);

            if (logEntry.Exception != null) {
                textWriter.Write(": ");
                textWriter.Write(logEntry.Exception.ToString());
            }

            textWriter.Write(Environment.NewLine);
        }

        public void Dispose() {
            _OptionsReloadToken?.Dispose();
        }

        private static string GetLogLevelString(Microsoft.Extensions.Logging.LogLevel logLevel) {
            return logLevel switch {
                Microsoft.Extensions.Logging.LogLevel.Trace => "trce",
                Microsoft.Extensions.Logging.LogLevel.Debug => "dbug",
                Microsoft.Extensions.Logging.LogLevel.Information => "info",
                Microsoft.Extensions.Logging.LogLevel.Warning => "warn",
                Microsoft.Extensions.Logging.LogLevel.Error => "fail",
                Microsoft.Extensions.Logging.LogLevel.Critical => "crit",
                _ => "unknown"
            };
        }

        // Taken from dotnet runtime
        private ConsoleColors GetLogLevelConsoleColors(Microsoft.Extensions.Logging.LogLevel logLevel) {
            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch {
                Microsoft.Extensions.Logging.LogLevel.Trace => new ConsoleColors(ConsoleColor.Magenta, ConsoleColor.Black),
                Microsoft.Extensions.Logging.LogLevel.Debug => new ConsoleColors(ConsoleColor.Blue, ConsoleColor.Black),
                Microsoft.Extensions.Logging.LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
                Microsoft.Extensions.Logging.LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
                Microsoft.Extensions.Logging.LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
                Microsoft.Extensions.Logging.LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
                _ => new ConsoleColors(null, null)
            };
        }

        // Taken from dotnet runtime
        private readonly struct ConsoleColors {
            public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background) {
                Foreground = foreground;
                Background = background;
            }

            public ConsoleColor? Foreground { get; }

            public ConsoleColor? Background { get; }
        }

    }

    public static class TextWriterExtensions {
        public static void WriteColoredMessage(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground) {
            // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
            if (background.HasValue) {
                textWriter.Write(AnsiParser.GetBackgroundColorEscapeCode(background.Value));
            }
            if (foreground.HasValue) {
                textWriter.Write(AnsiParser.GetForegroundColorEscapeCode(foreground.Value));
            }
            textWriter.Write(message);
            if (foreground.HasValue) {
                textWriter.Write(AnsiParser.DefaultForegroundColor); // reset to default foreground color
            }
            if (background.HasValue) {
                textWriter.Write(AnsiParser.DefaultBackgroundColor); // reset to the background color
            }
        }

    }

    public class AnsiParser {

        private readonly Action<string, int, int, ConsoleColor?, ConsoleColor?> _onParseWrite;

        public AnsiParser(Action<string, int, int, ConsoleColor?, ConsoleColor?> onParseWrite) {
            _onParseWrite = onParseWrite;
        }

        /// <summary>
        /// Parses a subset of display attributes
        /// Set Display Attributes
        /// Set Attribute Mode [{attr1};...;{attrn}m
        /// Sets multiple display attribute settings. The following lists standard attributes that are getting parsed:
        /// 1 Bright
        /// Foreground Colours
        /// 30 Black
        /// 31 Red
        /// 32 Green
        /// 33 Yellow
        /// 34 Blue
        /// 35 Magenta
        /// 36 Cyan
        /// 37 White
        /// Background Colours
        /// 40 Black
        /// 41 Red
        /// 42 Green
        /// 43 Yellow
        /// 44 Blue
        /// 45 Magenta
        /// 46 Cyan
        /// 47 White
        /// </summary>
        public void Parse(string message) {
            int startIndex = -1;
            int length = 0;
            int escapeCode;
            ConsoleColor? foreground = null;
            ConsoleColor? background = null;
            var span = message.AsSpan();
            const char EscapeChar = '\x1B';
            ConsoleColor? color = null;
            bool isBright = false;
            for (int i = 0; i < span.Length; i++) {
                if (span[i] == EscapeChar && span.Length >= i + 4 && span[i + 1] == '[') {
                    if (span[i + 3] == 'm') {
                        // Example: \x1B[1m
                        if (IsDigit(span[i + 2])) {
                            escapeCode = (int)(span[i + 2] - '0');
                            if (startIndex != -1) {
                                _onParseWrite(message, startIndex, length, background, foreground);
                                startIndex = -1;
                                length = 0;
                            }
                            if (escapeCode == 1)
                                isBright = true;
                            i += 3;
                            continue;
                        }
                    } else if (span.Length >= i + 5 && span[i + 4] == 'm') {
                        // Example: \x1B[40m
                        if (IsDigit(span[i + 2]) && IsDigit(span[i + 3])) {
                            escapeCode = (int)(span[i + 2] - '0') * 10 + (int)(span[i + 3] - '0');
                            if (startIndex != -1) {
                                _onParseWrite(message, startIndex, length, background, foreground);
                                startIndex = -1;
                                length = 0;
                            }
                            if (TryGetForegroundColor(escapeCode, isBright, out color)) {
                                foreground = color;
                                isBright = false;
                            } else if (TryGetBackgroundColor(escapeCode, out color)) {
                                background = color;
                            }
                            i += 4;
                            continue;
                        }
                    }
                }
                if (startIndex == -1) {
                    startIndex = i;
                }
                int nextEscapeIndex = -1;
                if (i < message.Length - 1) {
                    nextEscapeIndex = message.IndexOf(EscapeChar, i + 1);
                }
                if (nextEscapeIndex < 0) {
                    length = message.Length - startIndex;
                    break;
                }
                length = nextEscapeIndex - startIndex;
                i = nextEscapeIndex - 1;
            }
            if (startIndex != -1) {
                _onParseWrite(message, startIndex, length, background, foreground);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDigit(char c) => (uint)(c - '0') <= ('9' - '0');

        internal const string DefaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
        internal const string DefaultBackgroundColor = "\x1B[49m"; // reset to the background color

        internal static string GetForegroundColorEscapeCode(ConsoleColor color) {
            return color switch {
                ConsoleColor.Black => "\x1B[30m",
                ConsoleColor.DarkRed => "\x1B[31m",
                ConsoleColor.DarkGreen => "\x1B[32m",
                ConsoleColor.DarkYellow => "\x1B[33m",
                ConsoleColor.DarkBlue => "\x1B[34m",
                ConsoleColor.DarkMagenta => "\x1B[35m",
                ConsoleColor.DarkCyan => "\x1B[36m",
                ConsoleColor.Gray => "\x1B[37m",
                ConsoleColor.Red => "\x1B[1m\x1B[31m",
                ConsoleColor.Green => "\x1B[1m\x1B[32m",
                ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
                ConsoleColor.Blue => "\x1B[1m\x1B[34m",
                ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
                ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
                ConsoleColor.White => "\x1B[1m\x1B[37m",
                _ => DefaultForegroundColor // default foreground color
            };
        }

        internal static string GetBackgroundColorEscapeCode(ConsoleColor color) {
            return color switch {
                ConsoleColor.Black => "\x1B[40m",
                ConsoleColor.DarkRed => "\x1B[41m",
                ConsoleColor.DarkGreen => "\x1B[42m",
                ConsoleColor.DarkYellow => "\x1B[43m",
                ConsoleColor.DarkBlue => "\x1B[44m",
                ConsoleColor.DarkMagenta => "\x1B[45m",
                ConsoleColor.DarkCyan => "\x1B[46m",
                ConsoleColor.Gray => "\x1B[47m",
                _ => DefaultBackgroundColor // Use default background color
            };
        }

        private static bool TryGetForegroundColor(int number, bool isBright, out ConsoleColor? color) {
            color = number switch {
                30 => ConsoleColor.Black,
                31 => isBright ? ConsoleColor.Red : ConsoleColor.DarkRed,
                32 => isBright ? ConsoleColor.Green : ConsoleColor.DarkGreen,
                33 => isBright ? ConsoleColor.Yellow : ConsoleColor.DarkYellow,
                34 => isBright ? ConsoleColor.Blue : ConsoleColor.DarkBlue,
                35 => isBright ? ConsoleColor.Magenta : ConsoleColor.DarkMagenta,
                36 => isBright ? ConsoleColor.Cyan : ConsoleColor.DarkCyan,
                37 => isBright ? ConsoleColor.White : ConsoleColor.Gray,
                _ => null
            };
            return color != null || number == 39;
        }

        private static bool TryGetBackgroundColor(int number, out ConsoleColor? color) {
            color = number switch {
                40 => ConsoleColor.Black,
                41 => ConsoleColor.DarkRed,
                42 => ConsoleColor.DarkGreen,
                43 => ConsoleColor.DarkYellow,
                44 => ConsoleColor.DarkBlue,
                45 => ConsoleColor.DarkMagenta,
                46 => ConsoleColor.DarkCyan,
                47 => ConsoleColor.Gray,
                _ => null
            };
            return color != null || number == 49;
        }
    }

}
