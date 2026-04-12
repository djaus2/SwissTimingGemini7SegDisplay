using System.Collections.Generic;
using System;
using System.Linq;

namespace SwissTimingDisplay.Models
{
    public static class TcpCommandDefinitions
    {
        public static readonly IReadOnlyDictionary<TcpCommand, IReadOnlyList<CharCommand>> Commands =
            new Dictionary<TcpCommand, IReadOnlyList<CharCommand>>
            {
                // { TcpCommand.GeneralClear, new[] { CharCommand.SOH,CharCommand.Rxx,CharCommand.HOME, CharCommand.STX, CharCommand.ETX, CharCommand.TTT,CharCommand.EOT } },
                // { TcpCommand.TimeOfDayOrRunningTime, new[] { CharCommand.SOH, CharCommand.STX, CharCommand.SPC, CharCommand.ETX, CharCommand.EOT } },
                // { TcpCommand.RunningTimeDifference, new[] { CharCommand.SOH, CharCommand.DC4, CharCommand.Rxx, CharCommand.STX, CharCommand.HOME,CharCommand.SPC, CharCommand.SPC ,CharCommand.SPC,CharCommand.STX,CharCommand.TTT,CharCommand.EOT } },
                // { TcpCommand.NumberAndNetTime, new[] {  CharCommand.SOH, CharCommand.DC4, CharCommand.Sxx, CharCommand.STX, CharCommand.HOME, CharCommand.NNN,CharCommand.TTT,CharCommand.EOT }  },
                { TcpCommand.RollerTimeModeClear, new[] {  CharCommand.STX, CharCommand.B, CharCommand.ETX, } },
                { TcpCommand.RollerTimeofDayorRunningTime, new[] {  CharCommand.STX, CharCommand.I, CharCommand.TIME, CharCommand.NNN,CharCommand.SPC,CharCommand.SPC, CharCommand.ETX } },

            };//

        public static Dictionary<TcpCommand, IReadOnlyList<CharCommand>> CreateDictionaryFromCsvLines(
            IEnumerable<string> lines)
        {
            if (lines is null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var dict = new Dictionary<TcpCommand, IReadOnlyList<CharCommand>>();

            var lineNumber = 0;
            foreach (var line in lines)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!TryAddOrUpdateFromCsv(dict, line, out var error))
                {
                    throw new FormatException($"Invalid CSV at line {lineNumber}: {error} Line='{line}'");
                }
            }

            return dict;
        }

        public static List<byte> GetPayloadBytesFromCsv(
            string csv,
            bool useWallClockTimeOfDay,
            string? manualTimeOfDay,
            string? bibNo)
        {
            if (!TryGetPayloadBytesFromCsv(csv, useWallClockTimeOfDay, manualTimeOfDay, bibNo, out var payload, out var error))
            {
                throw new FormatException(error);
            }

            return payload;
        }

        public static bool TryGetPayloadBytesFromCsv(
            string csv,
            bool useWallClockTimeOfDay,
            string? manualTimeOfDay,
            string? bibNo,
            out List<byte> payload,
            out string error)
        {
            payload = new List<byte>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(csv))
            {
                error = "CSV input is empty.";
                return false;
            }

            var firstComma = csv.IndexOf(',');
            if (firstComma < 0)
            {
                error = "CSV must contain at least a TCP command and one CharCommand.";
                return false;
            }

            var tcpToken = csv[..firstComma].Trim();
            var remainder = csv[(firstComma + 1)..];

            if (!Enum.TryParse<TcpCommand>(tcpToken, ignoreCase: true, out var tcpCommand))
            {
                error = $"Invalid TCP command '{tcpToken}'.";
                return false;
            }

            if (!CharCommandCsvParser.TryParseCsv(remainder, out var charCommands, out var parseError))
            {
                error = parseError;
                return false;
            }

            if (charCommands.Count == 0)
            {
                error = "No CharCommands provided.";
                return false;
            }

            if (tcpCommand == TcpCommand.RollerTimeofDayorRunningTime)
            {
                // Per requirement: replace the 3rd byte (index 2) when it is TIME with HHMMSS ASCII digits.
                if (charCommands.Count > 2 && charCommands[2] == CharCommand.TIME)
                {
                    var timeDigits = useWallClockTimeOfDay
                        ? DateTime.Now.ToString("HHmmss")
                        : TimeStringHelper.GetSixDigitsOnly(manualTimeOfDay);
                    var bibDigits = BibNoHelper.ToThreeDigits(bibNo);

                    for (var i = 0; i < charCommands.Count; i++)
                    {
                        if (i == 2)
                        {
                            foreach (var ch in timeDigits)
                            {
                                payload.Add((byte)ch);
                            }

                            continue;
                        }

                        if (i == 3 && charCommands[i] == CharCommand.NNN)
                        {
                            foreach (var ch in bibDigits)
                            {
                                payload.Add((byte)ch);
                            }

                            continue;
                        }

                        payload.Add((byte)charCommands[i]);
                    }

                    return true;
                }
            }

            payload = charCommands.Select(c => (byte)c).ToList();
            return true;
        }

        public static List<byte> GetPayloadBytes(TcpCommand tcpCommand) =>
            GetPayloadBytes(Commands, tcpCommand);

        public static List<byte> GetPayloadBytes(
            IReadOnlyDictionary<TcpCommand, IReadOnlyList<CharCommand>> dictionary,
            TcpCommand tcpCommand)
        {
            if (!TryGetPayloadBytes(dictionary, tcpCommand, out var payload, out var error))
            {
                throw new KeyNotFoundException(error);
            }

            return payload;
        }

        public static bool TryGetPayloadBytes(
            IReadOnlyDictionary<TcpCommand, IReadOnlyList<CharCommand>> dictionary,
            TcpCommand tcpCommand,
            out List<byte> payload,
            out string error)
        {
            payload = new List<byte>();
            error = string.Empty;

            if (dictionary is null)
            {
                error = "Dictionary is null.";
                return false;
            }

            if (!dictionary.TryGetValue(tcpCommand, out var charCommands) || charCommands is null)
            {
                error = $"TCP command '{tcpCommand}' not found in dictionary.";
                return false;
            }

            payload = charCommands.Select(c => (byte)c).ToList();
            return true;
        }

        public static void AddOrUpdateFromCsv(
            IDictionary<TcpCommand, IReadOnlyList<CharCommand>> dictionary,
            string csv)
        {
            if (!TryAddOrUpdateFromCsv(dictionary, csv, out var error))
            {
                throw new FormatException(error);
            }
        }

        public static bool TryAddOrUpdateFromCsv(
            IDictionary<TcpCommand, IReadOnlyList<CharCommand>> dictionary,
            string csv,
            out string error)
        {
            error = string.Empty;

            if (dictionary is null)
            {
                error = "Dictionary is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(csv))
            {
                error = "CSV input is empty.";
                return false;
            }

            // Expected format: TcpCommand,CharCommand,CharCommand...
            // Example: GeneralClear,SOH,STX,ETX,EOT
            var firstComma = csv.IndexOf(',');
            if (firstComma < 0)
            {
                error = "CSV must contain at least a TCP command and one CharCommand.";
                return false;
            }

            var tcpToken = csv[..firstComma].Trim();
            var remainder = csv[(firstComma + 1)..];

            if (!Enum.TryParse<TcpCommand>(tcpToken, ignoreCase: true, out var tcpCommand))
            {
                error = $"Invalid TCP command '{tcpToken}'.";
                return false;
            }

            if (!CharCommandCsvParser.TryParseCsv(remainder, out var charCommands, out var parseError))
            {
                error = parseError;
                return false;
            }

            if (charCommands.Count == 0)
            {
                error = "No CharCommands provided.";
                return false;
            }

            dictionary[tcpCommand] = charCommands;
            return true;
        }
    }
}
