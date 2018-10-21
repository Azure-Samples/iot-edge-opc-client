
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace OpcClient
{
    using System.ComponentModel;
    using static Program;

    /// <summary>
    /// Class describing an action to execute
    /// </summary>
    public class ActionModel
    {
        /// <summary>
        /// Ctor for the action.
        /// </summary>
        public ActionModel()
        {
            Id = null;
            Interval = 0;
        }

        /// <summary>
        /// Ctor for the action
        /// </summary>
        public ActionModel(string id, int interval = 0)
        {
            Id = id;
            Interval = interval;
        }
        
        // Id of the target node. Can be:
        // a NodeId ("ns=")
        // an ExpandedNodeId ("nsu=")
        public string Id;

        // if set action will recur with a period of Interval seconds, if set to 0 it will done only once
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling =DefaultValueHandling.IgnoreAndPopulate)]
        public int Interval;
    }

    /// <summary>
    /// Class describing a read action on an OPC UA server.
    /// </summary>
    public class ReadActionModel : ActionModel
    {
        /// <summary>
        /// Ctor of a read action model.
        /// </summary>
        public ReadActionModel()
        {
            Id = null;
            Interval = 0;
        }

        /// <summary>
        /// Ctor of a read action model.
        /// </summary>
        public ReadActionModel(string id, int interval = 0)
        {
            Id = id;
            Interval = interval;
        }

        /// <summary>
        /// Ctor of a read action model.
        /// </summary>
        public ReadActionModel(ReadActionModel action)
        {
            Id = action.Id;
            Interval = action.Interval;
        }
    }

    /// <summary>
    /// Class describing a test action on an OPC UA server.
    /// </summary>
    public class TestActionModel : ActionModel
    {
        /// <summary>
        /// Ctor of a test action model.
        /// Default test works on the current time node with an interval of 30 sec.
        /// </summary>
        public TestActionModel()
        {
            Id = "i=2258";
            Interval = 30;
        }

        /// <summary>
        /// Ctor of a test action model.
        /// </summary>
        public TestActionModel(string id, int interval = 0)
        {
            Id = id;
            Interval = interval;
        }

        /// <summary>
        /// Ctor of a test action model.
        /// </summary>
        public TestActionModel(TestActionModel action)
        {
            Id = action.Id;
            Interval = action.Interval;
        }
    }

    /// <summary>
    /// Class describing a model for an OPC action.
    /// </summary>
    public partial class OpcActionConfigurationModel
    {
        /// <summary>
        /// Ctor of the action configuration model.
        /// </summary>
        public OpcActionConfigurationModel()
        {
            Init();
        }

        /// <summary>
        /// Ctor of the action configuration model.
        /// </summary>
        public OpcActionConfigurationModel(ReadActionModel action, string endpointUrl = null, bool useSecurity = true)
        {
            Init();
            EndpointUrl = new Uri(endpointUrl ?? DefaultEndpointUrl);
            UseSecurity = useSecurity;
            Read.Add(new ReadActionModel(action));
        }

        /// <summary>
        /// Ctor of the action configuration model.
        /// </summary>
        public OpcActionConfigurationModel(TestActionModel action, string endpointUrl = null, bool useSecurity = true)
        {
            Init();
            EndpointUrl = new Uri(endpointUrl ?? DefaultEndpointUrl);
            UseSecurity = useSecurity;
            Test.Add(new TestActionModel(action));
        }

        /// <summary>
        /// Init the action configuration model.
        /// </summary>
        private void Init()
        {
            EndpointUrl = new Uri(DefaultEndpointUrl);
            UseSecurity = true;
            Read = new List<ReadActionModel>();
            Test = new List<TestActionModel>();
        }

        /// <summary>
        /// Endpoint URL of the server the action should target.
        /// </summary>
        public Uri EndpointUrl { get; set; }

        /// <summary>
        /// Controls if the OPC UA session should use a secure endpoint.
        /// </summary>
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public bool UseSecurity { get; set; }

        /// <summary>
        /// The read actions on the endpoint.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ReadActionModel> Read { get; set; }

        /// <summary>
        /// The test actions on the endpoint.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<TestActionModel> Test { get; set; }
    }
}
