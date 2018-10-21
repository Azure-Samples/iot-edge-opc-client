
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpcClient
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using static OpcApplicationConfiguration;
    using static Program;

    /// <summary>
    /// Class for the applications internal OPC relevant configuration.
    /// </summary>
    public static class OpcConfiguration
    {
        /// <summary>
        /// list of all OPC sessions to manage
        /// </summary>
        public static List<OpcSession> OpcSessions { get; set; }

        /// <summary>
        /// Semaphore to protect access to the OPC sessions list.
        /// </summary>
        public static SemaphoreSlim OpcSessionsListSemaphore { get; set; }

        /// <summary>
        /// Semaphore to protect the access to the action list.
        /// </summary>
        public static SemaphoreSlim OpcActionListSemaphore { get; set; }

        /// <summary>
        /// Filename of the configuration file.
        /// </summary>
        public static string OpcActionConfigurationFilename { get; set; } = $"{System.IO.Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}actionconfig.json";

        /// <summary>
        /// Reports number of OPC sessions.
        /// </summary>
        public static int NumberOfOpcSessions
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Count();
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Reports number of connected OPC sessions.
        /// </summary>
        public static int NumberOfConnectedOpcSessions
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Count(s => s.State == OpcSession.SessionState.Connected);
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Reports number of configured recurring actions.
        /// </summary>
        public static int NumberOfRecurringActions
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Where(s => s.OpcActions.Count > 0).Select(s => s.OpcActions).Sum(a => a.Count(i => i.Interval > 0));
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Reports number of all actions.
        /// </summary>
        public static int NumberOfActions
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Select(s => s.OpcActions).Sum(a => a.Count());
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Initialize resources for the configuration management.
        /// </summary>
        public static void Init()
        {
            OpcSessionsListSemaphore = new SemaphoreSlim(1);
            OpcSessions = new List<OpcSession>();
            OpcActionListSemaphore = new SemaphoreSlim(1);
            OpcSessions = new List<OpcSession>();
            _actionConfiguration = new List<OpcActionConfigurationModel>();
        }

        /// <summary>
        /// Frees resources for configuration management.
        /// </summary>
        public static void Deinit()
        {
            OpcSessions = null;
            OpcSessionsListSemaphore.Dispose();
            OpcSessionsListSemaphore = null;
            OpcActionListSemaphore.Dispose();
            OpcActionListSemaphore = null;
        }

        /// <summary>
        /// Read and parse the startup configuration file.
        /// </summary>
        public static async Task<bool> ReadOpcConfigurationAsync()
        {
            try
            {
                await OpcActionListSemaphore.WaitAsync();

                // if the file exists, read it, if not just continue 
                if (File.Exists(OpcActionConfigurationFilename))
                {
                    Logger.Information($"Attemtping to load action configuration from: {OpcActionConfigurationFilename}");
                    _actionConfiguration = JsonConvert.DeserializeObject<List<OpcActionConfigurationModel>>(File.ReadAllText(OpcActionConfigurationFilename));
                }
                else
                {
                    Logger.Information($"The action configuration file '{OpcActionConfigurationFilename}' does not exist. Continue...");
                }

                // add connectivity test action if requested
                if (TestConnectivity)
                {
                    Logger.Information($"Creating test action to test connectivity");
                    _actionConfiguration.Add(new OpcActionConfigurationModel(new TestActionModel()));
                }

                // add unsecure connectivity test action if requested
                if (TestUnsecureConnectivity)
                {
                    Logger.Information($"Creating test action to test unsecured connectivity");
                    _actionConfiguration.Add(new OpcActionConfigurationModel(new TestActionModel(), DefaultEndpointUrl, false));
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Loading of the action configuration file failed. Does the file exist and has correct syntax? Exiting...");
                return false;
            }
            finally
            {
                OpcActionListSemaphore.Release();
            }
            Logger.Information($"There is(are) {_actionConfiguration.Sum(c => c.Read.Count + c.Test.Count)} action(s) configured.");
            return true;
        }

        /// <summary>
        /// Create the data structures to manage actions.
        /// </summary>
        public static async Task<bool> CreateOpcActionDataAsync()
        {
            try
            {
                await OpcActionListSemaphore.WaitAsync();
                await OpcSessionsListSemaphore.WaitAsync();

                // create actions out of the configuration
                var uniqueSessionInfo = _actionConfiguration.Select(n => new Tuple<Uri, bool>(n.EndpointUrl, n.UseSecurity)).Distinct();
                foreach (var sessionInfo in uniqueSessionInfo)
                {
                    // create new session info.
                    OpcSession opcSession = new OpcSession(sessionInfo.Item1, sessionInfo.Item2, OpcSessionCreationTimeout);

                    // add all actions to the session
                    List<OpcAction> actionsOnEndpoint = new List<OpcAction>();
                    var endpointConfigs = _actionConfiguration.Where(c => c.EndpointUrl == sessionInfo.Item1 && c.UseSecurity == sessionInfo.Item2);
                    foreach (var config in endpointConfigs)
                    {
                        config?.Read.ForEach(a => opcSession.OpcActions.Add(new OpcReadAction(config.EndpointUrl, a)));
                        config?.Test.ForEach(a => opcSession.OpcActions.Add(new OpcTestAction(config.EndpointUrl, a)));
                    }

                    // report actions
                    Logger.Information($"Actions on '{opcSession.EndpointUrl.AbsoluteUri}' {(opcSession.UseSecurity ? "with" : "without")} security.");
                    foreach (var action in opcSession.OpcActions)
                    {
                        Logger.Information($"{action.Description}, recurring each: {action.Interval} sec");
                    }

                    // add session
                    OpcSessions.Add(opcSession);
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Creation of the internal OPC management structures failed. Exiting...");
                return false;
            }
            finally
            {
                OpcSessionsListSemaphore.Release();
                OpcActionListSemaphore.Release();
            }
            return true;
        }

        /// <summary>
        /// Create an OPC session management data structures.
        /// </summary>
        private static void CreateOpcSession(Uri endpointUrl, bool useSecurity)
        {
            try
            {
                // create new session info
                OpcSession opcSession = new OpcSession(endpointUrl, useSecurity, OpcSessionCreationTimeout);

                // add all actions to the session
                List<OpcAction> actionsOnEndpoint = new List<OpcAction>();
                var endpointConfigs = _actionConfiguration.Where(c => c.EndpointUrl == endpointUrl);
                foreach (var config in endpointConfigs)
                {
                    config?.Read.ForEach(a => opcSession.OpcActions.Add(new OpcReadAction(config.EndpointUrl, a)));
                    config?.Test.ForEach(a => opcSession.OpcActions.Add(new OpcTestAction(config.EndpointUrl, a)));
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Creation of the OPC session failed.");
                throw e;
            }
        }
        private static List<OpcActionConfigurationModel> _actionConfiguration;
    }
}
