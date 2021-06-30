using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using CsvHelper;
using Lurgle.Alerting;
using Lurgle.Logging;
using Seq.Api;
using Seq.Api.Model.Data;
using Seq.Api.Model.Signals;

namespace Seq.Client.Reporter
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            //Disable remote certificate validation
            ServicePointManager.ServerCertificateValidationCallback += ValidateCertificate;

            var isConfig = false;
            Logging.SetConfig();
            Alerting.SetConfig();

            foreach (var arg in args)
                if (arg.StartsWith("-config", StringComparison.CurrentCultureIgnoreCase))
                {
                    var configPath = arg.Split('=');
                    if (string.IsNullOrEmpty(configPath[1]) || !File.Exists(configPath[1])) continue;
                    isConfig = true;
                    Config.GetConfig(configPath[1]);
                    if (string.IsNullOrEmpty(Config.Query))
                        ExitApp(ExitCodes.NoQuery);
                    if (Config.TimeFrom == null)
                        ExitApp(ExitCodes.TimeFromInvalid);
                    if (Config.TimeTo == null)
                        ExitApp(ExitCodes.TimeToInvalid);
                    GetAlertConfigOverrides();
                    GetLoggingConfigOverrides();
                }

            Log.Level().Add("{AppName:l} v{AppVersion:l} starting", Config.AppName, Config.AppVersion);
            Log.Level().Add("Seq Server: {SeqServer:l}, Api Key: {IsApiKey}", Logging.Config.LogSeqServer,
                !string.IsNullOrEmpty(Logging.Config.LogSeqApiKey));

            if (!isConfig)
            {
                Log.Add("You must specify a valid config file with -config=<pathtoconfig>");
                Logging.Close();
                Environment.Exit(1);
            }

            Log.Level().Add("Query: {Query}", Config.Query);
            // ReSharper disable once PossibleInvalidOperationException
            Log.Level().Add("Output query from {Start:F} to {End:F}", ((DateTime) Config.TimeFrom).ToLocalTime(),
                // ReSharper disable once PossibleInvalidOperationException
                ((DateTime) Config.TimeTo).ToLocalTime());
            var connection = new SeqConnection(Logging.Config.LogSeqServer, Logging.Config.LogSeqApiKey);
            SignalEntity signal;
            try
            {
                signal = connection.Signals.FindAsync(Config.Signal).Result;
                Log.Level().Add("Signal '{Signal}' will be used to filter query", signal.Title);
            }
            catch (Exception)
            {
                signal = new SignalEntity();
            }

            Log.Level().Add("Executing query ...");

            try
            {
                QueryResultPart data;
                if (signal.Id != null)
                    data = connection.Data.QueryAsync(Config.Query, Config.TimeFrom, Config.TimeTo,
                        SignalExpressionPart.Signal(signal.Id)).Result;
                else
                    data = connection.Data.QueryAsync(Config.Query, Config.TimeFrom, Config.TimeTo).Result;

                if (!string.IsNullOrEmpty(data.Error))
                {
                    Log.Level(LurgLevel.Error).Add("Error returned from Seq: {Error}", data.Error);
                    ExitApp(ExitCodes.QueryError);
                }

                if (data.Rows.Length > 0)
                {
                    var filePath = Path.GetTempFileName();
                    if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                        }
                        catch (Exception)
                        {
                            ExitApp(ExitCodes.CsvFolderNotFound);
                        }

                    Log.Level().Add("Outputting results to {FilePath:l}", filePath);
                    try
                    {
                        var recordCount = 0;
                        var writer = new StreamWriter(filePath);
                        var csv = new CsvWriter(writer, CultureInfo.CurrentCulture, true);
                        Log.Level().Add("Adding header ...");
                        foreach (var col in data.Columns)
                            csv.WriteField(col);

                        csv.NextRecord();
                        Log.Level().Add("Parsing and adding rows ...");
                        foreach (var logRow in data.Rows)
                        {
                            recordCount++;
                            foreach (var logCol in logRow)
                                csv.WriteField(logCol);
                            csv.NextRecord();
                        }

                        csv.Dispose();
                        writer.Flush();
                        writer.Close();


                        try
                        {
                            Log.Level().Add("Sending via email to {MailTo:l} ...", Alerting.Config.MailTo);
                            Alert.To().Subject("{0} for {1:D}", Config.AppName, DateTime.Today)
                                .Attach(new MemoryStream(File.ReadAllBytes(filePath)),
                                    $"{Config.AppName}-{DateTime.Today:yyyy-M-d}.csv")
                                .SendTemplateFile("Report", new
                                {
                                    ReportName = Config.AppName,
                                    Date = DateTime.Today.ToLongDateString(),
                                    From = ((DateTime) Config.TimeFrom).ToLocalTime().ToString("F"),
                                    To = ((DateTime) Config.TimeTo).ToLocalTime().ToString("F"),
                                    RecordCount = recordCount.ToString()
                                });
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex).Add("Error sending email: {Message:l}", ex.Message);
                            ExitApp(ExitCodes.MailError);
                        }

                        ExitApp(ExitCodes.Success);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex).Add("Error writing CSV: {Error:l}", ex.Message);
                        ExitApp(ExitCodes.ErrorWritingCsv);
                    }
                }
                else
                {
                    ExitApp(ExitCodes.NoDataReturned);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Error executing query: {Error:l}", ex.Message);
                ExitApp(ExitCodes.QueryError);
            }
        }

        private static void GetAlertConfigOverrides()
        {
            var currentAlertConfig = Alerting.Config;
            var alertConfig = AlertConfig.GetConfig();
            currentAlertConfig = new AlertConfig(currentAlertConfig, alertConfig.AppName);

            //If MailHost is overridden, we treat these properties as an override group
            if (!string.IsNullOrEmpty(alertConfig.MailHost))
                currentAlertConfig = new AlertConfig(currentAlertConfig, mailRenderer: alertConfig.MailRenderer,
                    mailSender: alertConfig.MailSender,
                    mailHost: alertConfig.MailHost, mailPort: alertConfig.MailPort,
                    mailTestTimeout: alertConfig.MailTestTimeout,
                    mailUseAuthentication: alertConfig.MailUseAuthentication,
                    mailUsername: alertConfig.MailUsername, mailPassword: alertConfig.MailPassword,
                    mailUseTls: alertConfig.MailUseTls, mailTimeout: alertConfig.MailTimeout,
                    mailTemplatePath: alertConfig.MailTemplatePath);

            if (!string.IsNullOrEmpty(alertConfig.MailFrom))
                currentAlertConfig = new AlertConfig(currentAlertConfig, mailFrom: alertConfig.MailFrom);

            if (!string.IsNullOrEmpty(alertConfig.MailTo))
                currentAlertConfig = new AlertConfig(currentAlertConfig, mailTo: alertConfig.MailTo);

            Alerting.SetConfig(currentAlertConfig);
        }

        private static void GetLoggingConfigOverrides()
        {
            var currentLogConfig = Logging.Config;
            var logConfig = LoggingConfig.GetConfig();
            currentLogConfig = new LoggingConfig(currentLogConfig, appName: logConfig.AppName);

            //Configure logging overrides as needed
            if (!logConfig.LogMaskPolicy.Equals(MaskPolicy.None))
                currentLogConfig = new LoggingConfig(currentLogConfig, logMaskPolicy: logConfig.LogMaskPolicy,
                    logMaskPattern: logConfig.LogMaskPattern,
                    logMaskCharacter: logConfig.LogMaskCharacter, logMaskDigit: logConfig.LogMaskDigit);

            if (logConfig.LogType.Count > 0)
            {
                currentLogConfig = new LoggingConfig(currentLogConfig, logType: logConfig.LogType,
                    logLevel: logConfig.LogLevel);

                if (logConfig.LogType.Contains(LogType.Console))
                    currentLogConfig = new LoggingConfig(currentLogConfig,
                        logConsoleTheme: LoggingConfig.GetConsoleThemeType(logConfig.LogConsoleTheme),
                        logLevelConsole: logConfig.LogLevelConsole, logFormatConsole: logConfig.LogFormatConsole);

                if (logConfig.LogType.Contains(LogType.File))
                    if (!string.IsNullOrEmpty(logConfig.LogFolder))
                        currentLogConfig = new LoggingConfig(currentLogConfig, logFolder: logConfig.LogFolder,
                            logName: logConfig.LogName,
                            logExtension: logConfig.LogExtension, logFileType: logConfig.LogFileType,
                            logDays: logConfig.LogDays,
                            logFlush: logConfig.LogFlush, logBuffered: logConfig.LogBuffered,
                            logShared: logConfig.LogShared,
                            logLevelFile: logConfig.LogLevelFile, logFormatFile: logConfig.LogFormatFile);

                if (logConfig.LogType.Contains(LogType.EventLog))
                {
                    if (!string.IsNullOrEmpty(logConfig.LogEventSource))
                        currentLogConfig =
                            new LoggingConfig(currentLogConfig, logEventSource: logConfig.LogEventSource);

                    if (!string.IsNullOrEmpty(logConfig.LogEventName))
                        currentLogConfig = new LoggingConfig(currentLogConfig, logEventName: logConfig.LogEventName);

                    currentLogConfig = new LoggingConfig(currentLogConfig, logLevelEvent: logConfig.LogLevelEvent,
                        logFormatEvent: logConfig.LogFormatEvent);
                }

                if (logConfig.LogType.Contains(LogType.Seq))
                {
                    if (!string.IsNullOrEmpty(logConfig.LogSeqServer))
                        currentLogConfig = new LoggingConfig(currentLogConfig, logSeqServer: logConfig.LogSeqServer);

                    if (!string.IsNullOrEmpty(logConfig.LogSeqApiKey))
                        currentLogConfig = new LoggingConfig(currentLogConfig, logSeqApiKey: logConfig.LogSeqApiKey);

                    currentLogConfig = new LoggingConfig(currentLogConfig, logLevelSeq: logConfig.LogLevelSeq);
                }
            }

            //Push any overrides to the logging config
            Logging.SetConfig(currentLogConfig);
        }

        private static void ExitApp(ExitCodes exitCode)
        {
            LurgLevel logLevel;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (exitCode)
            {
                case ExitCodes.Success:
                case ExitCodes.NoConfig:
                    logLevel = LurgLevel.Information;
                    break;
                default:
                    logLevel = LurgLevel.Error;
                    break;
            }

            Log.Level(logLevel).Add("{AppName:l} v{AppVersion:l} exiting with result {ExitCode}", Config.AppName,
                Config.AppVersion,
                exitCode);

            Logging.Close();

            Environment.Exit((int) exitCode);
        }

        /// <summary>
        ///     Disable certificate validation to avoid SSL errors for TLS over SMTP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors errors)
        {
            return true;
        }
    }
}