using System;
using System.Configuration;
using System.Globalization;
using System.Reflection;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Seq.Client.Reporter
{
    public static class Config
    {
        public static string ConfigPath { get; private set; }
        public static string AppName { get; private set; }
        public static string AppVersion { get; private set; }
        public static TimeType TimeType { get; private set; }
        public static string Query { get; private set; }
        public static DateTime? TimeFrom { get; private set; }
        public static DateTime? TimeTo { get; private set; }

        public static string Signal { get; private set; }

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
            TimeType = GetTimeType(ConfigurationManager.AppSettings["TimeType"]);
            TimeFrom = GetStart(ConfigurationManager.AppSettings["TimeFrom"]);
            TimeTo = GetEnd(ConfigurationManager.AppSettings["TimeTo"]);
            Signal = ConfigurationManager.AppSettings["Signal"];

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
        }

        private static TimeType GetTimeType(string configValue)
        {
            return Enum.TryParse(configValue, true, out TimeType timeType)
                ? timeType
                : TimeType.Hours;
        }

        private static DateTime? GetStart(string configValue)
        {
            switch (TimeType)
            {
                case TimeType.Hours:
                    return ParseDateTime(configValue);
                default:
                    return null;
            }
        }

        private static DateTime? GetEnd(string configValue)
        {
            switch (TimeType)
            {
                case TimeType.Hours:
                    return ParseDateTime(configValue);
                default:
                    return null;
            }
        }

        private static DateTime? ParseDateTime(string configValue)
        {
            try
            {
                var timeFormat = "H:mm:ss";
                if (DateTime.TryParseExact(configValue, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out _))
                    return DateTime.ParseExact(configValue, timeFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None).ToUniversalTime();
                timeFormat = "H:mm";
                if (!DateTime.TryParseExact(configValue, timeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                    return null;
                return DateTime.ParseExact(configValue, timeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None).ToUniversalTime();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}