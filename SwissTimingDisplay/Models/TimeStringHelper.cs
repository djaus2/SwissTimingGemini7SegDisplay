using System;
using System.Linq;

namespace SwissTimingDisplay.Models
{
    public static class TimeStringHelper
    {
        public enum TimeKind
        {
            TimeOfDay,
            RunningTime,
        }

        public readonly record struct ParsedTime(TimeKind Kind, string SixDigits, string Standard);

        public static string ToSixDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException("Time value is empty. Enter 6 digits.");
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length != 6)
            {
                throw new FormatException($"Time value must be exactly 6 digits (got {digits.Length}).");
            }

            return digits;
        }

        public static ParsedTime ParseTimeInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new FormatException("Time value is empty.");
            }

            var trimmed = input.Trim();

            // Running time formats:
            // - H:MM:SS.T  (tenths)
            // - MM:SS.HH   (hundredths)
            if (trimmed.Contains(':') && trimmed.Contains('.'))
            {
                var dotIdx = trimmed.LastIndexOf('.');
                var left = trimmed[..dotIdx];
                var frac = trimmed[(dotIdx + 1)..];

                var parts = left.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    // MM:SS.HH
                    var minutes = ParseInt(parts[0], "minutes");
                    var seconds = ParseInt(parts[1], "seconds");
                    if (seconds < 0 || seconds > 59)
                    {
                        throw new FormatException("Running time seconds must be 00-59.");
                    }

                    var hundredths = ParseInt(frac, "hundredths");
                    if (hundredths < 0 || hundredths > 99)
                    {
                        throw new FormatException("Running time hundredths must be 00-99.");
                    }

                    if (minutes < 0 || minutes > 99)
                    {
                        throw new FormatException("Running time minutes must be 00-99.");
                    }

                    var digits = $"{minutes:00}{seconds:00}{hundredths:00}";
                    return new ParsedTime(TimeKind.RunningTime, digits, ToRunningTimeStandard(digits));
                }

                if (parts.Length == 3)
                {
                    // H:MM:SS.T  (tenths)
                    var hours = ParseInt(parts[0], "hours");
                    var minutesPart = ParseInt(parts[1], "minutes");
                    var seconds = ParseInt(parts[2], "seconds");
                    if (minutesPart < 0 || minutesPart > 59)
                    {
                        throw new FormatException("Running time minutes must be 00-59 when hours are supplied.");
                    }

                    if (seconds < 0 || seconds > 59)
                    {
                        throw new FormatException("Running time seconds must be 00-59.");
                    }

                    var tenths = ParseInt(frac, "tenths");
                    if (tenths < 0 || tenths > 9)
                    {
                        throw new FormatException("Running time tenths must be 0-9.");
                    }

                    if (hours < 0 || hours > 9)
                    {
                        throw new FormatException("Running time hours must be 0-9 for H:MM:SS.T format.");
                    }

                    var digits = $"{hours}{minutesPart:00}{seconds:00}{tenths}";
                    var standard = $"{hours}:{minutesPart:00}:{seconds:00}.{tenths}";
                    return new ParsedTime(TimeKind.RunningTime, digits, standard);
                }

                throw new FormatException("Invalid time format.");
            }

            // Time-of-day formats:
            // - HH:MM:SS
            // - HHMMSS / HHSSMM (6 digits)
            if (trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3)
                {
                    throw new FormatException("Time of day must be in HH:MM:SS format.");
                }

                var hh = ParseInt(parts[0], "hours");
                var mm = ParseInt(parts[1], "minutes");
                var ss = ParseInt(parts[2], "seconds");

                if (hh < 0 || hh > 23) throw new FormatException("Hours must be 00-23.");
                if (mm < 0 || mm > 59) throw new FormatException("Minutes must be 00-59.");
                if (ss < 0 || ss > 59) throw new FormatException("Seconds must be 00-59.");

                var digits = $"{hh:00}{mm:00}{ss:00}";
                return new ParsedTime(TimeKind.TimeOfDay, digits, ToTimeOfDayStandard(digits));
            }

            var digitsOnly = ToSixDigits(trimmed);
            // Assume 6 digits with no separators is time-of-day (HHMMSS / HHSSMM).
            // If the middle pair looks like seconds (>59) but last pair looks like seconds (<=59), treat as HHSSMM.
            var mid = int.Parse(digitsOnly.Substring(2, 2));
            var last = int.Parse(digitsOnly.Substring(4, 2));
            string hhmmss;
            if (mid > 59 && last <= 59)
            {
                // HHSSMM -> HHMMSS
                hhmmss = digitsOnly[..2] + digitsOnly.Substring(4, 2) + digitsOnly.Substring(2, 2);
            }
            else
            {
                hhmmss = digitsOnly;
            }

            // Validate as time-of-day
            var h = int.Parse(hhmmss[..2]);
            var m = int.Parse(hhmmss.Substring(2, 2));
            var s = int.Parse(hhmmss.Substring(4, 2));
            if (h < 0 || h > 23) throw new FormatException("Hours must be 00-23.");
            if (m < 0 || m > 59) throw new FormatException("Minutes must be 00-59.");
            if (s < 0 || s > 59) throw new FormatException("Seconds must be 00-59.");

            return new ParsedTime(TimeKind.TimeOfDay, hhmmss, ToTimeOfDayStandard(hhmmss));
        }

        public static string GetSixDigitsOnly(string? input)
        {
            return ParseTimeInput(input).SixDigits;
        }

        public static string GetTimeOfDaySixDigitsFromAnyInput(bool useWallClock, string? input, DateTime? now = null)
        {
            if (useWallClock)
            {
                return (now ?? DateTime.Now).ToString("HHmmss");
            }

            var parsed = ParseTimeInput(input);
            if (parsed.Kind != TimeKind.TimeOfDay)
            {
                throw new FormatException("Time input must be a Time of Day format.");
            }

            return parsed.SixDigits;
        }

        public static string GetTimeOfDaySixDigits(bool useWallClock, string? manualValue, DateTime? now = null)
        {
            if (useWallClock)
            {
                return (now ?? DateTime.Now).ToString("HHmmss");
            }

            return ToSixDigits(manualValue);
        }

        public static string ToTimeOfDayStandard(string sixDigits)
        {
            var digits = ToSixDigits(sixDigits);
            return $"{digits[..2]}:{digits.Substring(2, 2)}:{digits.Substring(4, 2)}";
        }

        public static string ToRunningTimeStandard(string sixDigits)
        {
            var digits = ToSixDigits(sixDigits);
            return $"{digits[..2]}:{digits.Substring(2, 2)}.{digits.Substring(4, 2)}";
        }

        private static int ParseInt(string value, string label)
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                throw new FormatException($"Missing {label}.");
            }

            if (!int.TryParse(digits, out var i))
            {
                throw new FormatException($"Invalid {label}.");
            }

            return i;
        }
    }
}
