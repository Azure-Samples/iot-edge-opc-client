
using System;

namespace OpcClient
{
    using Opc.Ua;
    using static Program;

    /// <summary>
    /// Class to manage OPC sessions.
    /// </summary>
    public class OpcAction
    {
        /// <summary>
        /// Next action id.
        /// </summary>
        private static uint IdCount = 0;

        /// <summary>
        /// Instance action id.
        /// </summary>
        public uint Id;

        /// <summary>
        /// Endpoint URL of the target server.
        /// </summary>
        public string EndpointUrl;

        /// <summary>
        /// Configured id of action target node.
        /// </summary>
        public string OpcNodeId;

        /// <summary>
        /// Recurring interval of action in sec.
        /// </summary>
        public int Interval;

        /// <summary>
        /// Next execution of action in utc ticks.
        /// </summary>
        public long NextExecution;

        /// <summary>
        /// OPC UA node id of action target node.
        /// </summary>
        public NodeId OpcUaNodeId;

        /// <summary>
        /// Description of action.
        /// </summary>
        public string Description => $"ActionId: {Id:D3} ActionType: '{GetType().Name}', Endpoint: '{EndpointUrl}' Node '{OpcNodeId}'";

        /// <summary>
        /// Ctor for the action.
        /// </summary>
        public OpcAction(Uri endpointUrl, string opcNodeId, int interval)
        {
            Id = IdCount++; ;
            EndpointUrl = endpointUrl.AbsoluteUri;
            Interval = interval;
            OpcNodeId = opcNodeId;
            NextExecution = DateTime.UtcNow.Ticks;
            OpcUaNodeId = null;
        }

        /// <summary>
        /// Execute function needs to be overloaded.
        /// </summary>
        public virtual void Execute(OpcSession session)
        {
            Logger.Error($"No Execute method for action ({Description}) defined.");
            throw new Exception($"No Execute method for action ({ Description}) defined.");
        }

        /// <summary>
        /// Report result of action.
        /// </summary>
        public virtual void ReportResult(ServiceResultException sre)
        {
            if (sre == null)
            {
                ReportSuccess();
            }
            else
            {
                ReportFailure(sre);
            }
        }

        /// <summary>
        /// Report successful action execution.
        /// </summary>
        public virtual void ReportSuccess()
        {
            Logger.Information($"Action ({Description}) completed successfully");
        }

        /// <summary>
        /// Report failed action execution.
        /// </summary>
        public virtual void ReportFailure(ServiceResultException sre)
        {
            Logger.Information($"Action ({Description}) execution with error");
            Logger.Information($"Result ({Description}): {sre.Result.ToString()}");
            if (sre.InnerException != null)
            {
                Logger.Information($"Details ({Description}): {sre.InnerException.Message}");
            }
        }
    }

    /// <summary>
    /// Class to describe a read action.
    /// </summary>
    public class OpcReadAction : OpcAction
    {
        /// <summary>
        /// Value read by the action.
        /// </summary>
        public dynamic Value;

        /// <summary>
        /// Ctor for the read action.
        /// </summary>
        public OpcReadAction(Uri endpointUrl, ReadActionModel action) : base(endpointUrl, action.Id, action.Interval)
        {
        }

        /// <inheritdoc />
        public override void Execute(OpcSession session)
        {
            Logger.Information($"Start action {Description} on '{session.EndpointUrl}'");

            // read the node info
            Node node = session.OpcUaClientSession.ReadNode(OpcUaNodeId);

            // report the node info
            Logger.Information($"Node Displayname is '{node.DisplayName}'");
            Logger.Information($"Node Description is '{node.Description}'");

            // read the value
            DataValue dataValue = session.OpcUaClientSession.ReadValue(OpcUaNodeId);

            // report the node value
            Logger.Information($"Node Value is '{dataValue.Value}'");
            Logger.Information($"Node Value is '{dataValue.ToString()}'");
        }

        /// <inheritdoc />
        public override void ReportSuccess()
        {
            Logger.Information($"Action ({Description}) completed successfully");
            Logger.Information($"Value ({Description}): {Value}");
        }
    }

    /// <summary>
    /// Class to describe a test action.
    /// </summary>
    public class OpcTestAction : OpcAction
    {
        /// <summary>
        /// Value read by the action.
        /// </summary>
        public dynamic Value;

        /// <summary>
        /// Ctor for the test action.
        /// </summary>
        public OpcTestAction(Uri endpointUrl, TestActionModel action) : base(endpointUrl, action.Id, action.Interval)
        {
        }

        /// <inheritdoc />
        public override void Execute(OpcSession session)
        {
            Logger.Debug($"Start action {Description} on '{session.EndpointUrl}'");

            if (OpcUaNodeId == null)
            {
                // get the NodeId
                OpcUaNodeId = session.GetNodeIdFromId(OpcNodeId);
            }

            // read the node info
            Node node = session.OpcUaClientSession.ReadNode(OpcUaNodeId);

            // report the node info
            Logger.Debug($"Action ({Description}) Node DisplayName is '{node.DisplayName}'");
            Logger.Debug($"Action ({Description}) Node Description is '{node.Description}'");

            // read the value
            DataValue dataValue = session.OpcUaClientSession.ReadValue(OpcUaNodeId);
            try
            {
                Value = dataValue.Value;
            }
            catch (Exception e)
            {
                Logger.Warning(e, $"Cannot convert type of read value.");
                Value = "Cannot convert type of read value.";
            }

            // report the node value
            Logger.Debug($"Action ({Description}) Node data value is '{dataValue.Value}'");
        }

        /// <inheritdoc />
        public override void ReportSuccess()
        {
            Logger.Information($"Action ({Description}) completed successfully");
            Logger.Information($"Value ({Description}): {Value}");
        }
    }
}
