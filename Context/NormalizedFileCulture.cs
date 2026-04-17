using System.Globalization;

namespace Context;

public static class NormalizedFileCulture
{
    public static CultureInfo Create(CultureInfo baseCulture)
    {
        var culture = (CultureInfo)baseCulture.Clone();
        
        var dateTimeFormat = culture.DateTimeFormat;

        // 1. Normaliseer datum: d -> dd, M -> MM voor voorloopnullen
        var datePattern = dateTimeFormat.ShortDatePattern;
        if (datePattern.Contains('d') && !datePattern.Contains("dd"))
            datePattern = datePattern.Replace("d", "dd");
        if (datePattern.Contains('M') && !datePattern.Contains("MM"))
            datePattern = datePattern.Replace("M", "MM");
        dateTimeFormat.ShortDatePattern = datePattern;

        // 2. Normaliseer tijd: Altijd HH:mm (24-uurs met voorloopnullen) voor consistente lengte
        dateTimeFormat.ShortTimePattern = "HH:mm";
        
        return culture;
    }
}
