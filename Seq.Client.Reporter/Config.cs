using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Seq.Client.Reporter
{
    public static class Config
    {
        public static string ConfigPath { get; private set; }
        public static string AppName { get; private set; }
        public static string AppVersion { get; private set; }
        public static string Query { get; private set; }
        public static int QueryTimeout { get; private set; }
        public static DateTime? TimeFrom { get; private set; }
        public static DateTime? TimeTo { get; private set; }

        public static IEnumerable<string> Signal { get; private set; }

        /// <summary>
        ///     Load a config file
        /// </summary>
        /// <param name="path"></param>
        public static void GetConfig(string path)
        {
            AppConfig.Change(path);
            LoadConfigFile();
        }

        /// <summary>
        ///     Load the currently configured config file
        /// </summary>
        private static void LoadConfigFile()
        {
            ConfigPath = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();
            AppName = ConfigurationManager.AppSettings["AppName"];
            Query = ConfigurationManager.AppSettings["Query"].Replace("\\r", "").Replace("\\n", "");
            QueryTimeout = GetInt(ConfigurationManager.AppSettings["Query"]);
            TimeFrom = GetStart(ConfigurationManager.AppSettings["TimeFrom"]);
            TimeTo = GetEnd(ConfigurationManager.AppSettings["TimeTo"]);
            //Ensure TimeFrom is always in the past
            if (TimeFrom != null && TimeTo != null && ((DateTime) TimeTo - (DateTime) TimeFrom).TotalSeconds <= 0)
                TimeFrom = ((DateTime) TimeFrom).AddDays(-1);
            Signal = GetSignals(ConfigurationManager.AppSettings["Signal"]);

            var isSuccess = true;
            try
            {
                if (string.IsNullOrEmpty(AppName))
                    AppName = Assembly.GetEntryAssembly()?.GetName().Name;
                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
            }
            catch
            {
                isSuccess = false;
            }

            if (isSuccess) return;
            try
            {
                if (string.IsNullOrEmpty(AppName))
                    AppName = Assembly.GetExecutingAssembly().GetName().Name;
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch
            {
                //We surrender ...
                if (string.IsNullOrEmpty(AppName))
                    AppName = string.Empty;
                AppVersion = string.Empty;
            }

            if (QueryTimeout < 1)
                QueryTimeout = 1;
        }

        private static IEnumerable<string> GetSignals(string configValue)
        {
            if (string.IsNullOrEmpty(configValue))
                return new string[0];

            return configValue.Split(',').Select(s => s.Trim()).ToList();
        }

        private static DateTime? GetStart(string configValue)
        {
            return ParseDateTime(configValue);
        }

        private static DateTime? GetEnd(string configValue)
        {
            return ParseDateTime(configValue);
        }

        private static DateTime? ParseDateTime(string configValue)
        {
            if (string.IsNullOrEmpty(configValue))
                return null;
            try
            {
                var now = DateTime.Now;

                if (configValue.Equals("now", StringComparison.CurrentCultureIgnoreCase))
                    return now.ToUniversalTime();

                //Parse a time string if specified
                const string timeExpression = "^((?:[0-1]?[0-9]|2[0-3])\\:(?:[0-5][0-9])(?:\\:[0-5][0-9])?)$";
                if (Regex.IsMatch(configValue, timeExpression))
                {
                    var match = Regex.Match(configValue, timeExpression);
                    return ParseTimeString(match.Groups[1].Value);
                }

                //Parse a date expression if specified
                const string dateExpression = "^(\\d+)(s|m|h|d|w|M)$";
                if (Regex.IsMatch(configValue, dateExpression))
                {
                    var match = Regex.Match(configValue, dateExpression);
                    return ParseDateExpression(now, int.Parse(match.Groups[1].Value), match.Groups[2].Value);
                }

                //Parse a hybrid date expression with time if specified
                const string hybridExpression =
                    "^(\\d+)(s|m|h|d|w|M)\\s+((?:[0-1]?[0-9]|2[0-3])\\:(?:[0-5][0-9])(?:\\:[0-5][0-9])?)$";
                if (Regex.IsMatch(configValue, hybridExpression))
                {
                    var match = Regex.Match(configValue, hybridExpression);
                    var relativeTime = ParseTimeString(match.Groups[3].Value);
                    return relativeTime != null
                        ? ParseDateExpression(((DateTime) relativeTime).ToLocalTime(), int.Parse(match.Groups[1].Value),
                            match.Groups[2].Value)
                        : null;
                }

                //Attempt to parse using culture formatting rules
                if (DateTime.TryParse(configValue, out var date))
                    return date.ToUniversalTime();

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTime? ParseDateExpression(DateTime startTime, int time, string expression)
        {
            switch (expression)
            {
                case "s":
                    return startTime.AddSeconds(-time).ToUniversalTime();
                case "m":
                    return startTime.AddMinutes(-time).ToUniversalTime();
                case "h":
                    return startTime.AddHours(-time).ToUniversalTime();
                case "d":
                    return startTime.AddDays(-time).ToUniversalTime();
                case "w":
                    return startTime.AddDays(-(7 * time)).ToUniversalTime();
                case "M":
                    return startTime.AddMonths(-time).ToUniversalTime();
                default:
                    return null;
            }
        }

        private static DateTime? ParseTimeString(string time)
        {
            var timeFormat = "H:mm:ss";
            if (DateTime.TryParseExact(time, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
                return DateTime.ParseExact(time, timeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None).ToUniversalTime();
            timeFormat = "H:mm";
            if (!DateTime.TryParseExact(time, timeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
                return null;

            return DateTime.ParseExact(time, timeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None).ToUniversalTime();
        }

        /// <summary>
        ///     Convert the supplied <see cref="object" /> to an <see cref="int" />
        ///     <para />
        ///     This will filter out nulls that could otherwise cause exceptions
        /// </summary>
        /// <param name="sourceObject">An object that can be converted to an int</param>
        /// <returns></returns>
        public static int GetInt(object sourceObject)
        {
            var sourceString = string.Empty;

            if (!Convert.IsDBNull(sourceObject)) sourceString = (string) sourceObject;

            if (int.TryParse(sourceString, out var destInt)) return destInt;

            return -1;
        }
    }
}