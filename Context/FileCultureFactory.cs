using System.Globalization;
using System.Xml.Linq;

namespace Context;

public static class FileCultureFactory
{
    extension(CultureInfo baseCulture)
    {
        public CultureInfo AsFileCulture
        {
            get
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

                // 2. Normaliseer tijd: behoud locale-formaat (incl. AM/PM), maar voorloopnullen toevoegen
                //    h:mm tt → hh:mm tt, H:mm → HH:mm, etc.
                var timePattern = dateTimeFormat.ShortTimePattern;
                if (timePattern.Contains('h') && !timePattern.Contains("hh"))
                    timePattern = timePattern.Replace("h", "hh");
                if (timePattern.Contains('H') && !timePattern.Contains("HH"))
                    timePattern = timePattern.Replace("H", "HH");
                dateTimeFormat.ShortTimePattern = timePattern;

                return culture;
            }
        }
    }
}
