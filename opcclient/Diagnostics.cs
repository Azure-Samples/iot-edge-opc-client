
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpcClient
{
    using static OpcConfiguration;
    using static Program;

    /// <summary>
    /// Class to enable output to the console.
    /// </summary>
    public static class Diagnostics
    {
        /// <summary>
        /// Interval in seconds to show diagnostic info.
        /// </summary>
        public static uint DiagnosticsInterval { get; set; } = 0;

        public static void Init()
        {
            // init data
            _showDiagnosticsInfoTask = null;
            _shutdownTokenSource = new CancellationTokenSource();

            // kick off the task to show diagnostic info
            if (DiagnosticsInterval > 0)
            {
                _showDiagnosticsInfoTask = Task.Run(async () => await ShowDiagnosticsInfoAsync(_shutdownTokenSource.Token));
            }


        }

        /// <summary>
        /// Shutdown diagnostic task.
        /// </summary>
        /// <returns></returns>
        public async static Task ShutdownAsync()
        {
            // wait for diagnostic task completion if it is enabled
            if (_showDiagnosticsInfoTask != null)
            {
                _shutdownTokenSource.Cancel();
                await _showDiagnosticsInfoTask;
            }

            _shutdownTokenSource = null;
            _showDiagnosticsInfoTask = null;
        }

        /// <summary>
        /// Kicks of the task to show diagnostic information each 30 seconds.
        /// </summary>
        public static async Task ShowDiagnosticsInfoAsync(CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay((int)DiagnosticsInterval * 1000, ct);

                    Logger.Information("==========================================================================");
                    Logger.Information($"{ProgramName} status @ {System.DateTime.UtcNow} (started @ {ProgramStartTime})");
                    Logger.Information("---------------------------------");
                    Logger.Information($"OPC sessions: {NumberOfOpcSessions}");
                    Logger.Information($"connected OPC sessions: {NumberOfConnectedOpcSessions}");
                    Logger.Information($"# of actions: {NumberOfActions}");
                    Logger.Information($"# of recurring actions: {NumberOfRecurringActions}");
                    Logger.Information("---------------------------------");
                    Logger.Information($"current working set in MB: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)}");
                    Logger.Information("==========================================================================");
                }
                catch
                {
                }
            }
        }

        private static CancellationTokenSource _shutdownTokenSource;
        private static Task _showDiagnosticsInfoTask;
    }
}
