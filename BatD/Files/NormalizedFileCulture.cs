using System.Globalization;

namespace BatD.Files;

public static class NormalizedFileCulture
{
    public static CultureInfo Create(this CultureInfo baseCulture)
    {
        var culture = (CultureInfo)baseCulture.Clone();

        var dateTimeFormat = culture.DateTimeFormat;

        var datePattern = dateTimeFormat.ShortDatePattern;
        if (datePattern.Contains('d') && !datePattern.Contains("dd"))
            datePattern = datePattern.Replace("d", "dd");
        if (datePattern.Contains('M') && !datePattern.Contains("MM"))
            datePattern = datePattern.Replace("M", "MM");
        dateTimeFormat.ShortDatePattern = datePattern;

        var timePattern = dateTimeFormat.ShortTimePattern;
        if (timePattern.Contains('h') && !timePattern.Contains("hh"))
            timePattern = timePattern.Replace("h", "hh");
        if (timePattern.Contains('H') && !timePattern.Contains("HH"))
            timePattern = timePattern.Replace("H", "HH");
        dateTimeFormat.ShortTimePattern = timePattern;

        return culture;
    }
}
