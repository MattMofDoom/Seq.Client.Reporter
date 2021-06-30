using System;
using System.Configuration;
using System.Linq;
using System.Reflection;

namespace Seq.Client.Reporter
{
    /// <summary>
    ///     App config file handling
    /// </summary>
    public abstract class AppConfig : IDisposable
    {
        /// <summary>
        ///     Dispose
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        ///     Load the specified config file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public static AppConfig Change(string path)
        {
            return new ChangeAppConfig(path);
        }

        /// <summary>
        ///     App config handling
        /// </summary>
        private class ChangeAppConfig : AppConfig
        {
            private readonly string _oldConfig =
                AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

            private bool _disposedValue;

            /// <summary>
            ///     Load and switch to a new config file
            /// </summary>
            /// <param name="path"></param>
            public ChangeAppConfig(string path)
            {
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path);
                ResetConfigMechanism();
            }

            /// <summary>
            ///     Dispose
            /// </summary>
            public override void Dispose()
            {
                if (!_disposedValue)
                {
                    AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", _oldConfig);
                    ResetConfigMechanism();


                    _disposedValue = true;
                }

                GC.SuppressFinalize(this);
            }

            /// <summary>
            ///     Reset the config mechanism
            /// </summary>
            private static void ResetConfigMechanism()
            {
                typeof(ConfigurationManager)
                    .GetField("s_initState", BindingFlags.NonPublic |
                                             BindingFlags.Static)
                    ?.SetValue(null, 0);

                typeof(ConfigurationManager)
                    .GetField("s_configSystem", BindingFlags.NonPublic |
                                                BindingFlags.Static)
                    ?.SetValue(null, null);

                typeof(ConfigurationManager)
                    .Assembly
                    .GetTypes()
                    .First(x => x.FullName ==
                                "System.Configuration.ClientConfigPaths")
                    .GetField("s_current", BindingFlags.NonPublic |
                                           BindingFlags.Static)
                    ?.SetValue(null, null);
            }
        }
    }
}