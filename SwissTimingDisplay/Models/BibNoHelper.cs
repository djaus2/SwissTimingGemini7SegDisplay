using System;
using System.Globalization;

namespace SwissTimingDisplay.Models
{
    public static class BibNoHelper
    {
        public static string ToThreeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "   ";
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bib))
            {
                throw new FormatException("Bib No. must be a number.");
            }

            if (bib < 1 || bib > 999)
            {
                throw new FormatException("Bib No. must be between 1 and 999.");
            }

            return bib.ToString("000", CultureInfo.InvariantCulture);
        }
    }
}
