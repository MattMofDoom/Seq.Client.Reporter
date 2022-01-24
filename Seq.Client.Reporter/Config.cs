using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Lurgle.Dates;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Seq.Client.Reporter
{
    public static class Config
    {
        public static string ConfigPath { get; private set; }
        public static string AppName { get; private set; }
        public static string AppVersion { get; private set; }
        public static bool ValidateTls { get; private set; }
        public static bool IsDebug { get; private set; }
        public static string Query { get; private set; }
        public static int QueryTimeout { get; private set; }
        public static DateTime? TimeFrom { get; private set; }
        public static DateTime? TimeTo { get; private set; }
        public static ReportDestination Destination { get; private set; }
        public static string JiraUrl { get; private set; }
        public static string JiraUsername { get; private set; }
        public static string JiraPassword { get; private set; }
        public static string JiraProject { get; private set; }
        public static string JiraIssueType { get; private set; }
        public static string JiraPriority { get; private set; }
        public static string JiraAssignee { get; private set; }
        public static string JiraLabels { get; private set; }
        public static string JiraInitialEstimate { get; private set; }
        public static string JiraRemainingEstimate { get; private set; }
        public static string JiraDueDate { get; private set; }
        public static bool UseProxy { get; private set; }
        public static string ProxyServer { get; private set; }
        public static bool BypassProxyOnLocal { get; private set; }
        public static string ProxyBypass { get; private set; }
        public static string ProxyUser { get; private set; }
        public static string ProxyPassword { get; private set; }


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
            ValidateTls = GetBool(ConfigurationManager.AppSettings["ValidateTls"], true);
            IsDebug = GetBool(ConfigurationManager.AppSettings["IsDebug"]);
            Query = ConfigurationManager.AppSettings["Query"].Replace("\\r", "").Replace("\\n", "");
            QueryTimeout = GetInt(ConfigurationManager.AppSettings["QueryTimeout"]);
            TimeFrom = DateParse.GetDateTimeUtc(ConfigurationManager.AppSettings["TimeFrom"]);
            TimeTo = DateParse.GetDateTimeUtc(ConfigurationManager.AppSettings["TimeTo"]);
            //Ensure TimeFrom is always in the past
            if (TimeFrom != null && TimeTo != null && ((DateTime) TimeTo - (DateTime) TimeFrom).TotalSeconds <= 0)
                TimeFrom = ((DateTime) TimeFrom).AddDays(-1);
            Signal = GetSignals(ConfigurationManager.AppSettings["Signal"]);

            Destination = GetReportDestination(ConfigurationManager.AppSettings["ReportDestination"]);

            JiraUrl = ConfigurationManager.AppSettings["JiraUrl"];
            JiraUsername = ConfigurationManager.AppSettings["JiraUsername"];
            JiraPassword = ConfigurationManager.AppSettings["JiraPassword"];
            JiraProject = ConfigurationManager.AppSettings["JiraProject"];
            JiraAssignee = ConfigurationManager.AppSettings["JiraAssignee"];
            JiraIssueType = ConfigurationManager.AppSettings["JiraIssueType"];
            JiraPriority = ConfigurationManager.AppSettings["JiraPriority"];
            JiraLabels = ConfigurationManager.AppSettings["JiraLabels"];

            var date = ConfigurationManager.AppSettings["JiraInitialEstimate"];
            if (!string.IsNullOrEmpty(date) && DateTokens.ValidDateExpression(date))
                JiraInitialEstimate = DateTokens.SetValidExpression(date);
            date = ConfigurationManager.AppSettings["JiraRemainingEstimate"];
            if (!string.IsNullOrEmpty(date) && DateTokens.ValidDateExpression(date))
                JiraRemainingEstimate = DateTokens.SetValidExpression(date);
            date = ConfigurationManager.AppSettings["JiraDueDate"];
            if (!string.IsNullOrEmpty(date) && DateTokens.ValidDateExpression(date))
                JiraDueDate = DateTokens.SetValidExpression(date);

            UseProxy = GetBool(ConfigurationManager.AppSettings["UseProxy"]);
            ProxyServer = ConfigurationManager.AppSettings["ProxyServer"];
            BypassProxyOnLocal = GetBool(ConfigurationManager.AppSettings["BypassProxyOnLocal"]);
            ProxyBypass = ConfigurationManager.AppSettings["ProxyBypass"];
            ProxyUser = ConfigurationManager.AppSettings["ProxyUser"];
            ProxyPassword = ConfigurationManager.AppSettings["ProxyPassword"];

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
                return Array.Empty<string>();

            return configValue.Split(',').Select(s => s.Trim()).ToList();
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

        /// <summary>
        ///     Convert the supplied <see cref="object" /> to a <see cref="bool" />
        ///     <para />
        ///     This will filter out nulls that could otherwise cause exceptions
        /// </summary>
        /// <param name="sourceObject">An object that can be converted to a bool</param>
        /// <param name="trueIfEmpty"></param>
        /// <returns></returns>
        public static bool GetBool(object sourceObject, bool trueIfEmpty = false)
        {
            var sourceString = string.Empty;

            if (!Convert.IsDBNull(sourceObject)) sourceString = (string) sourceObject;

            return bool.TryParse(sourceString, out var destBool) ? destBool : trueIfEmpty;
        }

        /// <summary>
        ///     Return the configured report destination. Defaults to email if not matched.
        /// </summary>
        /// <param name="configValue"></param>
        /// <returns></returns>
        private static ReportDestination GetReportDestination(string configValue)
        {
            if (string.IsNullOrEmpty(configValue)) return ReportDestination.Email;
            return Enum.TryParse(configValue, true, out ReportDestination destination)
                ? destination
                : ReportDestination.Email;
        }
    }
}