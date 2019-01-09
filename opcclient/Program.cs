
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpcClient
{
    using Opc.Ua;
    using Opc.Ua.Server;
    using Serilog;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using static Diagnostics;
    using static OpcApplicationConfiguration;
    using static OpcConfiguration;
    using static OpcSession;

    public class Program
    {
        /// <summary>
        /// name of the application
        /// </summary>
        public const string ProgramName = "OpcClient";

        /// <summary>
        /// shutdown token
        /// </summary>
        public static CancellationTokenSource ShutdownTokenSource;

        /// <summary>
        /// retry count when trying to shutdown sessions
        /// </summary>
        public static uint ShutdownRetryCount { get; } = 10;

        /// <summary>
        /// logging object
        /// </summary>
        public static Serilog.Core.Logger Logger { get; set; } = null;

        /// <summary>
        /// signals to do a connectivity test to the default server on a secure endpoint
        /// </summary>
        public static bool TestConnectivity { get; set; } = true;

        /// <summary>
        /// signals to do a connectivity test to the default server on n unsecure endpoint
        /// </summary>
        public static bool TestUnsecureConnectivity { get; set; } = false;

        /// <summary>
        /// default target server
        /// </summary>
        public static string DefaultEndpointUrl { get; set; } = "opc.tcp://opcplc:50000";

        /// <summary>
        /// time of application start
        /// </summary>
        public static DateTime ProgramStartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Synchronous main method of the app.
        /// </summary>
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public async static Task MainAsync(string[] args)
        {
            try
            {
                var shouldShowHelp = false;

                // shutdown token sources
                ShutdownTokenSource = new CancellationTokenSource();

                // command line options
                Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                        { "cf|configfile=", $"the filename containing action configuration.\nDefault: '{OpcActionConfigurationFilename}'", (string p) => OpcActionConfigurationFilename = p },

                        { "tc|testconnectivity", $"tests connectivity with the default server '{TestConnectivity}'.\nDefault: {TestConnectivity}", b => TestConnectivity = b != null },
                        { "tu|testunsecureconnectivity", $"tests connectivity with the default server '{TestUnsecureConnectivity}' using an unsecured endpoint.\nDefault: {TestUnsecureConnectivity}", b => TestUnsecureConnectivity = b != null },

                        { "de|defaultendpointurl=", $"endpoint OPC UA server used as default.\nDefault: {DefaultEndpointUrl}", (string s) => DefaultEndpointUrl = s },

                        { "sw|sessionconnectwait=", $"specify the wait time in seconds we try to connect to disconnected endpoints and starts monitoring unmonitored items\nMin: 10\nDefault: {SessionConnectWait}", (int i) => {
                                if (i > 10)
                                {
                                    SessionConnectWait = i;
                                }
                                else
                                {
                                    throw new OptionException("The sessionconnectwait must be greater than 10 sec", "sessionconnectwait");
                                }
                            }
                        },
                        { "di|diagnosticsinterval=", $"shows diagnostic info at the specified interval in seconds (need log level info). 0 disables diagnostic output.\nDefault: {DiagnosticsInterval}", (uint u) => DiagnosticsInterval = u },

                        { "lf|logfile=", $"the filename of the logfile to use.\nDefault: './{_logFileName}'", (string l) => _logFileName = l },
                        { "lt|logflushtimespan=", $"the timespan in seconds when the logfile should be flushed.\nDefault: {_logFileFlushTimeSpanSec} sec", (int s) => {
                                if (s > 0)
                                {
                                    _logFileFlushTimeSpanSec = TimeSpan.FromSeconds(s);
                                }
                                else
                                {
                                    throw new Mono.Options.OptionException("The logflushtimespan must be a positive number.", "logflushtimespan");
                                }
                            }
                        },
                        { "ll|loglevel=", $"the loglevel to use (allowed: fatal, error, warn, info, debug, verbose).\nDefault: info", (string l) => {
                                List<string> logLevels = new List<string> {"fatal", "error", "warn", "info", "debug", "verbose"};
                                if (logLevels.Contains(l.ToLowerInvariant()))
                                {
                                    _logLevel = l.ToLowerInvariant();
                                }
                                else
                                {
                                    throw new Mono.Options.OptionException("The loglevel must be one of: fatal, error, warn, info, debug, verbose", "loglevel");
                                }
                            }
                        },

                        // opc configuration options
                        { "ol|opcmaxstringlen=", $"the max length of a string opc can transmit/receive.\nDefault: {OpcMaxStringLength}", (int i) => {
                                if (i > 0)
                                {
                                    OpcMaxStringLength = i;
                                }
                                else
                                {
                                    throw new OptionException("The max opc string length must be larger than 0.", "opcmaxstringlen");
                                }
                            }
                        },
                        { "ot|operationtimeout=", $"the operation timeout of the OPC UA client in ms.\nDefault: {OpcOperationTimeout}", (int i) => {
                                if (i >= 0)
                                {
                                    OpcOperationTimeout = i;
                                }
                                else
                                {
                                    throw new OptionException("The operation timeout must be larger or equal 0.", "operationtimeout");
                                }
                            }
                        },
                        { "ct|createsessiontimeout=", $"specify the timeout in seconds used when creating a session to an endpoint. On unsuccessful connection attemps a backoff up to {OpcSessionCreationBackoffMax} times the specified timeout value is used.\nMin: 1\nDefault: {OpcSessionCreationTimeout}", (uint u) => {
                                if (u > 1)
                                {
                                    OpcSessionCreationTimeout = u;
                                }
                                else
                                {
                                    throw new OptionException("The createsessiontimeout must be greater than 1 sec", "createsessiontimeout");
                                }
                            }
                        },
                        { "ki|keepaliveinterval=", $"specify the interval in seconds se send keep alive messages to the OPC servers on the endpoints it is connected to.\nMin: 2\nDefault: {OpcKeepAliveInterval}", (int i) => {
                                if (i >= 2)
                                {
                                    OpcKeepAliveInterval = i;
                                }
                                else
                                {
                                    throw new OptionException("The keepaliveinterval must be greater or equal 2", "keepalivethreshold");
                                }
                            }
                        },
                        { "kt|keepalivethreshold=", $"specify the number of keep alive packets a server can miss, before the session is disconneced\nMin: 1\nDefault: {OpcKeepAliveDisconnectThreshold}", (uint u) => {
                                if (u > 1)
                                {
                                    OpcKeepAliveDisconnectThreshold = u;
                                }
                                else
                                {
                                    throw new OptionException("The keepalivethreshold must be greater than 1", "keepalivethreshold");
                                }
                            }
                        },

                        { "aa|autoaccept", $"trusts all servers we establish a connection to.\nDefault: {AutoAcceptCerts}", b => AutoAcceptCerts = b != null },

                        { "to|trustowncert", $"our own certificate is put into the trusted certificate store automatically.\nDefault: {TrustMyself}", t => TrustMyself = t != null  },

                        // cert store options
                        { "at|appcertstoretype=", $"the own application cert store type. \n(allowed values: Directory, X509Store)\nDefault: '{OpcOwnCertStoreType}'", (string s) => {
                                if (s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(CertificateStoreType.Directory, StringComparison.OrdinalIgnoreCase))
                                {
                                    OpcOwnCertStoreType = s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) ? CertificateStoreType.X509Store : CertificateStoreType.Directory;
                                    OpcOwnCertStorePath = s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) ? OpcOwnCertX509StorePathDefault : OpcOwnCertDirectoryStorePathDefault;
                                }
                                else
                                {
                                    throw new OptionException();
                                }
                            }
                        },
                        { "ap|appcertstorepath=", $"the path where the own application cert should be stored\nDefault (depends on store type):\n" +
                                $"X509Store: '{OpcOwnCertX509StorePathDefault}'\n" +
                                $"Directory: '{OpcOwnCertDirectoryStorePathDefault}'", (string s) => OpcOwnCertStorePath = s
                        },

                        { "tp|trustedcertstorepath=", $"the path of the trusted cert store\nDefault '{OpcTrustedCertDirectoryStorePathDefault}'", (string s) => OpcTrustedCertStorePath = s
                        },

                        { "rp|rejectedcertstorepath=", $"the path of the rejected cert store\nDefault '{OpcRejectedCertDirectoryStorePathDefault}'", (string s) => OpcRejectedCertStorePath = s
                        },

                        { "ip|issuercertstorepath=", $"the path of the trusted issuer cert store\nDefault '{OpcIssuerCertDirectoryStorePathDefault}'", (string s) => OpcIssuerCertStorePath = s
                        },

                        { "csr", $"show data to create a certificate signing request\nDefault '{ShowCreateSigningRequestInfo}'", c => ShowCreateSigningRequestInfo = c != null
                        },

                        { "ab|applicationcertbase64=", $"update/set this applications certificate with the certificate passed in as bas64 string", (string s) =>
                            {
                                NewCertificateBase64String = s;
                            }
                        },
                        { "af|applicationcertfile=", $"update/set this applications certificate with the certificate file specified", (string s) =>
                            {
                                if (File.Exists(s))
                                {
                                    NewCertificateFileName = s;
                                }
                                else
                                {
                                    throw new OptionException($"The file '{s}' does not exist.", "applicationcertfile");
                                }
                            }
                        },

                        { "pb|privatekeybase64=", $"initial provisioning of the application certificate (with a PEM or PFX fomat) requires a private key passed in as base64 string", (string s) =>
                            {
                                PrivateKeyBase64String = s;
                            }
                        },
                        { "pk|privatekeyfile=", $"initial provisioning of the application certificate (with a PEM or PFX fomat) requires a private key passed in as file", (string s) =>
                            {
                                if (File.Exists(s))
                                {
                                    PrivateKeyFileName = s;
                                }
                                else
                                {
                                    throw new OptionException($"The file '{s}' does not exist.", "privatekeyfile");
                                }
                            }
                        },

                        { "cp|certpassword=", $"the optional password for the PEM or PFX or the installed application certificate", (string s) =>
                            {
                                CertificatePassword = s;
                            }
                        },

                        { "tb|addtrustedcertbase64=", $"adds the certificate to the applications trusted cert store passed in as base64 string (multiple strings supported)", (string s) =>
                            {
                                TrustedCertificateBase64Strings = ParseListOfStrings(s);
                            }
                        },
                        { "tf|addtrustedcertfile=", $"adds the certificate file(s) to the applications trusted cert store passed in as base64 string (multiple filenames supported)", (string s) =>
                            {
                                TrustedCertificateFileNames = ParseListOfFileNames(s, "addtrustedcertfile");
                            }
                        },

                        { "ib|addissuercertbase64=", $"adds the specified issuer certificate to the applications trusted issuer cert store passed in as base64 string (multiple strings supported)", (string s) =>
                            {
                                IssuerCertificateBase64Strings = ParseListOfStrings(s);
                            }
                        },
                        { "if|addissuercertfile=", $"adds the specified issuer certificate file(s) to the applications trusted issuer cert store (multiple filenames supported)", (string s) =>
                            {
                                IssuerCertificateFileNames = ParseListOfFileNames(s, "addissuercertfile");
                            }
                        },

                        { "rb|updatecrlbase64=", $"update the CRL passed in as base64 string to the corresponding cert store (trusted or trusted issuer)", (string s) =>
                            {
                                CrlBase64String = s;
                            }
                        },
                        { "uc|updatecrlfile=", $"update the CRL passed in as file to the corresponding cert store (trusted or trusted issuer)", (string s) =>
                            {
                                if (File.Exists(s))
                                {
                                    CrlFileName = s;
                                }
                                else
                                {
                                    throw new OptionException($"The file '{s}' does not exist.", "updatecrlfile");
                                }
                            }
                        },

                        { "rc|removecert=", $"remove cert(s) with the given thumbprint(s) (multiple thumbprints supported)", (string s) =>
                            {
                                ThumbprintsToRemove = ParseListOfStrings(s);
                            }
                        },

                        // misc
                        { "h|help", "show this message and exit", h => shouldShowHelp = h != null },
                    };

            List<string> extraArgs = new List<string>();
            try
            {
                // parse the command line
                extraArgs = options.Parse(args);
            }
            catch (OptionException e)
            {
                // initialize logging
                InitLogging();

                    // show message
                    Logger.Fatal(e, "Error in command line options");
                    Logger.Error($"Command line arguments: {String.Join(" ", args)}");

                    // show usage
                    Usage(options);
                    return;
                }

                // initialize logging
                InitLogging();

                // show usage if requested
                if (shouldShowHelp)
                {
                    Usage(options);
                    return;
                }

                // validate and parse extra arguments
                if (extraArgs.Count > 0)
                {
                    Logger.Error("Error in command line options");
                    Logger.Error($"Command line arguments: {String.Join(" ", args)}");
                    Usage(options);
                    return;
                }

                //show version
                Logger.Information($"{ProgramName} V{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion} starting up...");
                Logger.Debug($"Informational version: V{(Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute).InformationalVersion}");

                // allow canceling the application
                var quitEvent = new ManualResetEvent(false);
                try
                {
                    Console.CancelKeyPress += (sender, eArgs) =>
                    {
                        quitEvent.Set();
                        eArgs.Cancel = true;
                        ShutdownTokenSource.Cancel();
                    };
                }
                catch
                {
                }

                // init OPC configuration and tracing
                OpcApplicationConfiguration applicationStackConfiguration = new OpcApplicationConfiguration();
                await applicationStackConfiguration.ConfigureAsync();

                // read OPC action configuration
                OpcConfiguration.Init();
                if (!await ReadOpcConfigurationAsync())
                {
                    return;
                }

                // add well known actions
                if (!await CreateOpcActionDataAsync())
                {
                    return;
                }

                // kick off OPC client activities
                await SessionStartAsync();

                // initialize diagnostics
                Diagnostics.Init();

                // stop on user request
                Logger.Information("");
                Logger.Information("");
                Logger.Information($"{ProgramName} is running. Press CTRL-C to quit.");

                // wait for Ctrl-C
                quitEvent.WaitOne(Timeout.Infinite);

                Logger.Information("");
                Logger.Information("");
                ShutdownTokenSource.Cancel();
                Logger.Information($"{ProgramName} is shutting down...");

                // shutdown all OPC sessions
                await SessionShutdownAsync();

                // shutdown diagnostics
                await ShutdownAsync();

                ShutdownTokenSource = null;
            }
            catch (Exception e)
            {
                Logger.Fatal(e, e.StackTrace);
                e = e.InnerException ?? null;
                while (e != null)
                {
                    Logger.Fatal(e, e.StackTrace);
                    e = e.InnerException ?? null;
                }
                Logger.Fatal($"{ProgramName} exiting... ");
            }
        }

        /// <summary>
        /// Start all sessions.
        /// </summary>
        public async static Task SessionStartAsync()
        {
            try
            {
                await OpcSessionsListSemaphore.WaitAsync();
                OpcSessions.ForEach(s => s.ConnectAndMonitorSession.Set());
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Failed to start all sessions.");
            }
            finally
            {
                OpcSessionsListSemaphore.Release();
            }
        }

        /// <summary>
        /// Shutdown all sessions.
        /// </summary>
        public async static Task SessionShutdownAsync()
        {
            try
            {
                while (OpcSessions.Count > 0)
                {
                    OpcSession opcSession = null;
                    try
                    {
                        await OpcSessionsListSemaphore.WaitAsync();
                        opcSession = OpcSessions.ElementAt(0);
                        OpcSessions.RemoveAt(0);
                    }
                    finally
                    {
                        OpcSessionsListSemaphore.Release();
                    }
                    await opcSession?.ShutdownAsync();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Failed to shutdown all sessions.");
            }

            // wait and continue after a while
            uint maxTries = ShutdownRetryCount;
            while (true)
            {
                int sessionCount = OpcSessions.Count;
                if (sessionCount == 0)
                {
                    return;
                }
                if (maxTries-- == 0)
                {
                    Logger.Information($"There are still {sessionCount} sessions alive. Ignore and continue shutdown.");
                    return;
                }
                Logger.Information($"{ProgramName} is shutting down. Wait {SessionConnectWait} seconds, since there are stil {sessionCount} sessions alive...");
                await Task.Delay(SessionConnectWait * 1000);
            }
        }

        /// <summary>
        /// Usage message.
        /// </summary>
        private static void Usage(Mono.Options.OptionSet options)
        {
            // show usage
            Logger.Information("");
            Logger.Information($"{ProgramName} V{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}");
            Logger.Information($"Informational version: V{(Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute).InformationalVersion}");
            Logger.Information("");
            Logger.Information("Usage: {0}.exe [<options>]", Assembly.GetEntryAssembly().GetName().Name);
            Logger.Information("");
            Logger.Information($"{ProgramName} to run actions against OPC UA servers.");
            Logger.Information("To exit the application, just press CTRL-C while it is running.");
            Logger.Information("");
            Logger.Information("To specify a list of strings, please use the following format:");
            Logger.Information("\"<string 1>,<string 2>,...,<string n>\"");
            Logger.Information("or if one string contains commas:");
            Logger.Information("\"\"<string 1>\",\"<string 2>\",...,\"<string n>\"\"");
            Logger.Information("");

            // output the options
            Logger.Information("Options:");
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);
            options.WriteOptionDescriptions(stringWriter);
            string[] helpLines = stringBuilder.ToString().Split("\n");
            foreach (var line in helpLines)
            {
                Logger.Information(line);
            }
        }

        /// <summary>
        /// Handler for server status changes.
        /// </summary>
        private static void ServerEventStatus(Session session, SessionEventReason reason)
        {
            PrintSessionStatus(session, reason.ToString());
        }

        /// <summary>
        /// Shows the session status.
        /// </summary>
        private static void PrintSessionStatus(Session session, string reason)
        {
            lock (session.DiagnosticsLock)
            {
                string item = String.Format("{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (session.Identity != null)
                {
                    item += String.Format(":{0,20}", session.Identity.DisplayName);
                }
                item += String.Format(":{0}", session.Id);
                Logger.Information(item);
            }
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        private static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // set the log level
            switch (_logLevel)
            {
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    OpcTraceToLoggerFatal = 0;
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    OpcStackTraceMask = OpcTraceToLoggerError = Utils.TraceMasks.Error;
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    OpcTraceToLoggerWarning = 0;
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    OpcStackTraceMask = OpcTraceToLoggerInformation = 0;
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    OpcStackTraceMask = OpcTraceToLoggerDebug = Utils.TraceMasks.StackTrace | Utils.TraceMasks.Operation |
                        Utils.TraceMasks.StartStop | Utils.TraceMasks.ExternalSystem | Utils.TraceMasks.Security;
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    OpcStackTraceMask = OpcTraceToLoggerVerbose = Utils.TraceMasks.All;
                    break;
            }

            // set logging sinks
            loggerConfiguration.WriteTo.Console();

            if (!string.IsNullOrEmpty(_logFileName))
            {
                // configure rolling file sink
                const int MAX_LOGFILE_SIZE = 1024 * 1024;
                const int MAX_RETAINED_LOGFILES = 2;
                loggerConfiguration.WriteTo.File(_logFileName, fileSizeLimitBytes: MAX_LOGFILE_SIZE, flushToDiskInterval: _logFileFlushTimeSpanSec, rollOnFileSizeLimit: true, retainedFileCountLimit: MAX_RETAINED_LOGFILES);
            }

            Logger = loggerConfiguration.CreateLogger();
            Logger.Information($"Current directory is: {System.IO.Directory.GetCurrentDirectory()}");
            Logger.Information($"Log file is: {_logFileName}");
            Logger.Information($"Log level is: {_logLevel}");
            return;
        }

        /// <summary>
        /// Helper to build a list of byte arrays out of a comma separated list of base64 strings (optional in double quotes).
        /// </summary>
        private static List<string> ParseListOfStrings(string s)
        {
            List<string> strings = new List<string>();
            if (s[0] == '"' && (s.Count(c => c.Equals('"')) % 2 == 0))
            {
                while (s.Contains('"'))
                {
                    int first = 0;
                    int next = 0;
                    first = s.IndexOf('"', next);
                    next = s.IndexOf('"', ++first);
                    strings.Add(s.Substring(first, next - first));
                    s = s.Substring(++next);
                }
            }
            else if (s.Contains(','))
            {
                strings = s.Split(',').ToList();
                strings.ForEach(st => st.Trim());
                strings = strings.Select(st => st.Trim()).ToList();
            }
            else
            {
                strings.Add(s);
            }
            return strings;
        }

        /// <summary>
        /// Helper to build a list of filenames out of a comma separated list of filenames (optional in double quotes).
        /// </summary>
        private static List<string> ParseListOfFileNames(string s, string option)
        {
            List<string> fileNames = new List<string>();
            if (s[0] == '"' && (s.Count(c => c.Equals('"')) % 2 == 0))
            {
                while (s.Contains('"'))
                {
                    int first = 0;
                    int next = 0;
                    first = s.IndexOf('"', next);
                    next = s.IndexOf('"', ++first);
                    var fileName = s.Substring(first, next - first);
                    if (File.Exists(fileName))
                    {
                        fileNames.Add(fileName);
                    }
                    else
                    {
                        throw new OptionException($"The file '{fileName}' does not exist.", option);
                    }
                    s = s.Substring(++next);
                }
            }
            else if (s.Contains(','))
            {
                List<string> parsedFileNames = s.Split(',').ToList();
                parsedFileNames = parsedFileNames.Select(st => st.Trim()).ToList();
                foreach (var fileName in parsedFileNames)
                {
                    if (File.Exists(fileName))
                    {
                        fileNames.Add(fileName);
                    }
                    else
                    {
                        throw new OptionException($"The file '{fileName}' does not exist.", option);
                    }

                }
            }
            else
            {
                if (File.Exists(s))
                {
                    fileNames.Add(s);
                }
                else
                {
                    throw new OptionException($"The file '{s}' does not exist.", option);
                }
            }
            return fileNames;
        }

        private static string _logFileName = $"{Utils.GetHostName()}-{ProgramName.ToLower()}.log";
        private static string _logLevel = "info";
        private static TimeSpan _logFileFlushTimeSpanSec = TimeSpan.FromSeconds(30);
    }
}
