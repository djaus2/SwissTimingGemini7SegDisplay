using System;
using System.Collections.Generic;
using System.Globalization;

namespace SwissTimingDisplay.Models
{
    public static class CharCommandCsvParser
    {
        public static List<CharCommand> ParseCsv(string csv)
        {
            if (!TryParseCsv(csv, out var commands, out var error))
            {
                throw new FormatException(error);
            }

            return commands;
        }

        public static bool TryParseCsv(string csv, out List<CharCommand> commands, out string error)
        {
            commands = new List<CharCommand>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(csv))
            {
                error = "CSV input is empty.";
                return false;
            }

            var parts = csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i];

                if (!TryParseToken(token, out var cmd))
                {
                    error = $"Invalid token '{token}' at position {i + 1}.";
                    commands.Clear();
                    return false;
                }

                commands.Add(cmd);
            }

            return true;
        }

        private static bool TryParseToken(string token, out CharCommand cmd)
        {
            token = token.Trim();

            if (Enum.TryParse<CharCommand>(token, ignoreCase: true, out cmd))
            {
                return true;
            }

            var hex = token;
            if (hex.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[..^1];
            }

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[2..];
            }

            if (byte.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
            {
                if (Enum.IsDefined(typeof(CharCommand), b))
                {
                    cmd = (CharCommand)b;
                    return true;
                }
            }

            cmd = default;
            return false;
        }
    }
}
