
using Opc.Ua;
using System;

namespace OpcClient
{
    using System.Threading.Tasks;
    using static Program;

    /// <summary>
    /// Class for OPC Application configuration.
    /// </summary>
    public partial class OpcApplicationConfiguration
    {
        /// <summary>
        /// Configuration info for the OPC application.
        /// </summary>
        public static ApplicationConfiguration ApplicationConfiguration { get; private set; }
        public static string Hostname
        {
            get => _hostname;
            set => _hostname = value.ToLowerInvariant();
        }

        public static string HostnameLabel => (_hostname.Contains(".") ? _hostname.Substring(0, _hostname.IndexOf('.')) : _hostname);
        public static string ApplicationName => ProgramName;
        public static string ApplicationUri => $"urn:{ProgramName}:{HostnameLabel}";
        public static string ProductUri => $"https://github.com/azure-samples/iot-edge-opc-client";

        /// <summary>
        /// Default endpoint security of the application.
        /// </summary>
        public static string ServerSecurityPolicy { get; set; } = SecurityPolicies.Basic128Rsa15;

        /// <summary>
        /// Enables unsecure endpoint access to the application.
        /// </summary>
        public static bool EnableUnsecureTransport { get; set; } = false;

        /// <summary>
        /// Max timeout when creating a new session to a server.
        /// </summary>
        public static uint OpcSessionCreationTimeout { get; set; } = 10;

        /// <summary>
        /// Keep alive interval.
        /// </summary>
        public static int OpcKeepAliveInterval { get; set; } = 2;

        /// <summary>
        /// Backoff for session creation.
        /// </summary>
        public static uint OpcSessionCreationBackoffMax { get; set; } = 5;

        /// <summary>
        /// Number of missed keep alives allowed, before disconnecting the session.
        /// </summary>
        public static uint OpcKeepAliveDisconnectThreshold { get; set; } = 5;

        /// <summary>
        /// Set the max string length the OPC stack supports.
        /// </summary>
        public static int OpcMaxStringLength { get; set; } = 4 * 1024 * 1024;

        /// <summary>
        /// Operation timeout for OPC UA communication.
        /// </summary>
        public static int OpcOperationTimeout { get; set; } = 120000;

        /// <summary>
        /// Mapping of the application logging levels to OPC stack logging levels.
        /// </summary>
        public static int OpcTraceToLoggerVerbose = 0;
        public static int OpcTraceToLoggerDebug = 0;
        public static int OpcTraceToLoggerInformation = 0;
        public static int OpcTraceToLoggerWarning = 0;
        public static int OpcTraceToLoggerError = 0;
        public static int OpcTraceToLoggerFatal = 0;

        /// <summary>
        /// Set the OPC stack log level.
        /// </summary>
        public static int OpcStackTraceMask { get; set; } = 0;

        /// <summary>
        /// Ctor of the OPC application configuration.
        /// </summary>
        public OpcApplicationConfiguration()
        {
        }

        /// <summary>
        /// Configures all OPC stack settings.
        /// </summary>
        public async Task<ApplicationConfiguration> ConfigureAsync()
        {
            // instead of using a configuration XML file, we configure everything programmatically

            // passed in as command line argument
            ApplicationConfiguration = new ApplicationConfiguration();
            ApplicationConfiguration.ApplicationName = ApplicationName;
            ApplicationConfiguration.ApplicationUri = ApplicationUri;
            ApplicationConfiguration.ProductUri = ProductUri;
            ApplicationConfiguration.ApplicationType = ApplicationType.Client;

            // configure OPC stack tracing
            ApplicationConfiguration.TraceConfiguration = new TraceConfiguration();
            ApplicationConfiguration.TraceConfiguration.TraceMasks = OpcStackTraceMask;
            ApplicationConfiguration.TraceConfiguration.ApplySettings();
            Utils.Tracing.TraceEventHandler += new EventHandler<TraceEventArgs>(LoggerOpcUaTraceHandler);
            Logger.Information($"opcstacktracemask set to: 0x{OpcStackTraceMask:X}");

            // add default client configuration
            ApplicationConfiguration.ClientConfiguration = new ClientConfiguration();

            // configure transport settings
            ApplicationConfiguration.TransportQuotas = new TransportQuotas();
            ApplicationConfiguration.TransportQuotas.MaxStringLength = OpcMaxStringLength;
            ApplicationConfiguration.TransportQuotas.MaxMessageSize = 4 * 1024 * 1024;

            // security configuration
            await InitApplicationSecurityAsync();

            // show certificate store information
            await ShowCertificateStoreInformationAsync();

            // validate the configuration now
            await ApplicationConfiguration.Validate(ApplicationConfiguration.ApplicationType);

            return ApplicationConfiguration;
        }

        /// <summary>
        /// Event handler to log OPC UA stack trace messages into own logger.
        /// </summary>
        private static void LoggerOpcUaTraceHandler(object sender, TraceEventArgs e)
        {
            // return fast if no trace needed
            if ((e.TraceMask & OpcStackTraceMask) == 0)
            {
                return;
            }

            // e.Exception and e.Message are always null

            // format the trace message
            string message = string.Empty;
            message = string.Format(e.Format, e.Arguments).Trim();
            message = "OPC: " + message;

            // map logging level
            if ((e.TraceMask & OpcTraceToLoggerVerbose) != 0)
            {
                Logger.Verbose(message);
                return;
            }
            if ((e.TraceMask & OpcTraceToLoggerDebug) != 0)
            {
                Logger.Debug(message);
                return;
            }
            if ((e.TraceMask & OpcTraceToLoggerInformation) != 0)
            {
                Logger.Information(message);
                return;
            }
            if ((e.TraceMask & OpcTraceToLoggerWarning) != 0)
            {
                Logger.Warning(message);
                return;
            }
            if ((e.TraceMask & OpcTraceToLoggerError) != 0)
            {
                Logger.Error(message);
                return;
            }
            if ((e.TraceMask & OpcTraceToLoggerFatal) != 0)
            {
                Logger.Fatal(message);
                return;
            }
            return;
        }

        private static string _hostname = $"{Utils.GetHostName().ToLowerInvariant()}";
    }
}
