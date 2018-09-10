/* ========================================================================
 * Copyright (c) 2005-2017 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Opc.Ua;
using Opc.Ua.Export;
using Opc.Ua.Server;


namespace Server
{
    /// <summary>
    /// A node manager for a server that exposes several variables.
    /// </summary>
    public class NodeManager : CustomNodeManager2
    {
        #region Constructors
        /// <summary>
        /// Initializes the node manager.
        /// </summary>
        public NodeManager(IServerInternal server, ApplicationConfiguration configuration)
        :
            base(server, configuration, Namespaces.Empty)
        {
            SystemContext.NodeIdFactory = this;

            // get the configuration for the node manager.
            m_configuration = configuration.ParseExtension<ServerConfiguration>();

            // use suitable defaults if no configuration exists.
            if (m_configuration == null)
            {
                m_configuration = new ServerConfiguration();
            }
        }
        #endregion
        
        #region IDisposable Members
        /// <summary>
        /// An overrideable version of the Dispose.
        /// </summary>
        protected override void Dispose(bool disposing)
        {  
            if (disposing)
            {
                // TBD
            }
        }
        #endregion

        #region INodeIdFactory Members
        /// <summary>
        /// Creates the NodeId for the specified node.
        /// </summary>
        public override NodeId New(ISystemContext context, NodeState node)
        {
            return node.NodeId;
        }
        #endregion

        #region INodeManager Members
        /// <summary>
        /// Does any initialization required before the address space can be used.
        /// </summary>
        /// <remarks>
        /// The externalReferences is an out parameter that allows the node manager to link to nodes
        /// in other node managers. For example, the 'Objects' node is managed by the CoreNodeManager and
        /// should have a reference to the root folder node(s) exposed by this node manager.  
        /// </remarks>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                try
                {
                    // Import the initial data model from a NodeSet file
                    Import(SystemContext, Path.Combine(filePath));
                    m_RefrigeratorActualTemperatureTimer = new Timer(SimulateRefrigeratorTemperature, null, 1000, 1000);
                    //m_RefrigeratorDoorTimer = new Timer(SimulateRefrigeratorDoorOpen, null, 1000, 1000);
                    AttacOpenCloseMethod();
                }
                catch (Exception e)
                {
                    Utils.Trace(Utils.TraceMasks.Error, e.Message, "");
                }
            }
        }

        private void AttacOpenCloseMethod()
        {
            foreach (var methodIdentifier in m_RefrigeratorMethods)
            {
                var splitMethodIdentifier = methodIdentifier.Split(';');
                var nodeId = new NodeId(uint.Parse(splitMethodIdentifier[1].Replace("i=", string.Empty)), ushort.Parse(splitMethodIdentifier[0].Replace("ns=", string.Empty)));
                var myMethodNode = (MethodState)PredefinedNodes.Values.FirstOrDefault(nodeState => nodeState.NodeId.Equals(nodeId));
                myMethodNode.OnCallMethod += OnCallMethodOpenDoor;
            }
        }

        private ServiceResult OnCallMethodOpenDoor(ISystemContext context, MethodState method, IList<object> inputarguments, IList<object> outputarguments)
        {
            return ServiceResult.Good;
        }

        private void SimulateRefrigeratorDoorOpen(object state)
        {
            lock (Lock)
            {
               foreach (var doorIdentifier in m_RefrigeratorDoor)
                    {
                        var doorStateIdentifier = doorIdentifier.Split(';');
                        var probability = m_RandomGenerator.NextDouble() * 100;
                        if (probability > 98)
                        {
                            var writeValues = new List<WriteValue>();
                            var valueToWrite = new WriteValue();
                            valueToWrite.NodeId = new NodeId(uint.Parse(doorStateIdentifier[1].Replace("i=", string.Empty)), ushort.Parse(doorStateIdentifier[0].Replace("ns=", string.Empty)));
                            valueToWrite.AttributeId = Attributes.Value;
                            valueToWrite.IndexRange = string.Empty;
                            //valueToWrite.Value = new DataValue() { Value = myNewValue };
                            writeValues.Add(valueToWrite);

                            List<ServiceResult> errors = Enumerable.Repeat(ServiceResult.Good, writeValues.Count).ToList();
                            Write(SystemContext.OperationContext, writeValues, errors);
                        }
                }
            }
        }

        private void SimulateRefrigeratorTemperature(object state)
        {
            lock (Lock)
            {
                foreach (var actualTemperatureIdentifier in m_RefrigeratorTemperature)
                {

                    var actualTemperature = actualTemperatureIdentifier.Split(';');

                    var myNewValue = m_RandomGenerator.NextDouble() * 100;

                    var writeValues = new List<WriteValue>();
                    var valueToWrite = new WriteValue();
                    valueToWrite.NodeId = new NodeId(uint.Parse(actualTemperature[1].Replace("i=", string.Empty)), ushort.Parse(actualTemperature[0].Replace("ns=", string.Empty)));
                    valueToWrite.AttributeId = Attributes.Value;
                    valueToWrite.IndexRange = String.Empty;
                    valueToWrite.Value = new DataValue() { Value = myNewValue };
                    writeValues.Add(valueToWrite);
                    List<ServiceResult> errors = Enumerable.Repeat(ServiceResult.Good, writeValues.Count).ToList();
                    Write(SystemContext.OperationContext, writeValues, errors);
                }
            }
        }


        private ServiceResult Import(ServerSystemContext context, string filePath)
        {
            try
            {
                ImportNodeSet(context, filePath);
            }
            catch (Exception ex)
            {
                Utils.Trace(Utils.TraceMasks.Error, "NodeSetImportNodeManager.Import", "Error loading node set: {0}", ex.Message);
                throw new ServiceResultException(ex, StatusCodes.Bad);
            }
            return ServiceResult.Good;
        }

        private XmlElement[] ImportNodeSet(ISystemContext context, string filePath)
        {
            NodeStateCollection predefinedNodes = new NodeStateCollection();
            List<string> newNamespaceUris = new List<string>();

            XmlElement[] extensions = LoadFromNodeSet2Xml(context, filePath, true, newNamespaceUris, predefinedNodes);

            // Add the node set to the node manager
            for (int ii = 0; ii < predefinedNodes.Count; ii++)
            {
                AddPredefinedNode(context, predefinedNodes[ii]);
            }

            foreach (var item in NamespaceUris)
            {
                if (newNamespaceUris.Contains(item))
                {
                    newNamespaceUris.Remove(item);
                }
            }

            if (newNamespaceUris.Count > 0)
            {
                List<string> allNamespaceUris = newNamespaceUris.ToList();
                allNamespaceUris.AddRange(NamespaceUris);

                SetNamespaces(allNamespaceUris.ToArray());
            }

            UpdateRegistration(this, newNamespaceUris);

            // Ensure the reverse references exist
            Dictionary<NodeId, IList<IReference>> externalReferences = new Dictionary<NodeId, IList<IReference>>();
            AddReverseReferences(externalReferences);

            foreach (var item in externalReferences)
            {
                Server.NodeManager.AddReferences(item.Key, item.Value);
            }

            return extensions;
        }

        /// <summary>
        /// Updates the registration of the node manager in case of nodeset2.xml import
        /// </summary>
        /// <param name="nodeManager">The node manager that performed the import.</param>
        /// <param name="newNamespaceUris">The new namespace Uris that were imported.</param>
        private void UpdateRegistration(INodeManager nodeManager, List<string> newNamespaceUris)
        {
            if (nodeManager == null || newNamespaceUris == null)
            {
                return;
            }

            int index = -1;
            int arrayLength = 0;
            foreach (var namespaceUri in newNamespaceUris)
            {
                index = Server.NamespaceUris.GetIndex(namespaceUri);
                if (index == -1)
                {
                    // Something bad happened
                    Utils.Trace(Utils.TraceMasks.Error, "Nodeset2xmlNodeManager.UpdateRegistration", "Namespace uri: " + namespaceUri + " was not found in the server's namespace table.");

                    continue;
                }

                // m_namespaceManagers is declared Private in MasterNodeManager, therefore we must use Reflection to access it
                FieldInfo fieldInfo = Server.NodeManager.GetType().GetField("m_namespaceManagers", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                if (fieldInfo != null)
                {
                    var namespaceManagers = fieldInfo.GetValue(Server.NodeManager) as INodeManager[][];

                    if (namespaceManagers != null)
                    {
                        if (index <= namespaceManagers.Length - 1)
                        {
                            arrayLength = namespaceManagers[index].Length;
                            Array.Resize(ref namespaceManagers[index], arrayLength + 1);
                            namespaceManagers[index][arrayLength] = nodeManager;
                        }
                        else
                        {
                            Array.Resize(ref namespaceManagers, namespaceManagers.Length + 1);
                            namespaceManagers[namespaceManagers.Length - 1] = new INodeManager[] { nodeManager };
                        }

                        fieldInfo.SetValue(Server.NodeManager, namespaceManagers);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the NodeSet2.xml file and returns the Extensions data of the node set
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="updateTables">if set to <c>true</c> the namespace and server tables are updated with any new URIs.</param>
        /// <param name="namespaceUris">Returns the NamespaceUris defined in the node set.</param>
        /// <param name="predefinedNodes">The required NodeStateCollection</param>
        /// <returns>The collection of global extensions of the NodeSet2.xml file.</returns>
        private XmlElement[] LoadFromNodeSet2Xml(ISystemContext context, string filePath, bool updateTables, List<string> namespaceUris, NodeStateCollection predefinedNodes)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            byte[] readAllBytes = File.ReadAllBytes(filePath);
            MemoryStream istrm = new MemoryStream(readAllBytes);

            if (istrm == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadDecodingError, "Could not load nodes from resource: {0}", filePath);
            }

            return LoadFromNodeSet2(context, istrm, updateTables, namespaceUris, predefinedNodes);
        }

        /// <summary>
        /// Reads the schema information from a NodeSet2 XML document
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="istrm">The data stream containing a UANodeSet file.</param>
        /// <param name="updateTables">If set to <c>true</c> the namespace and server tables are updated with any new URIs.</param>
        /// <param name="namespaceUris">Returns the NamespaceUris defined in the node set.</param>
        /// /// <param name="predefinedNodes">The required NodeStateCollection</param>
        /// <returns>The collection of global extensions of the node set.</returns>
        private XmlElement[] LoadFromNodeSet2(ISystemContext context, Stream istrm, bool updateTables, List<string> namespaceUris, NodeStateCollection predefinedNodes)
        {
            UANodeSet nodeSet = UANodeSet.Read(istrm);

            if (nodeSet != null)
            {
                // Update namespace table
                if (updateTables)
                {
                    if (nodeSet.NamespaceUris != null && context.NamespaceUris != null)
                    {
                        for (int ii = 0; ii < nodeSet.NamespaceUris.Length; ii++)
                        {
                            context.NamespaceUris.GetIndexOrAppend(nodeSet.NamespaceUris[ii]);
                            namespaceUris.Add(nodeSet.NamespaceUris[ii]);
                        }
                    }
                }

                // Update server table
                if (updateTables)
                {
                    if (nodeSet.ServerUris != null && context.ServerUris != null)
                    {
                        for (int ii = 0; ii < nodeSet.ServerUris.Length; ii++)
                        {
                            context.ServerUris.GetIndexOrAppend(nodeSet.ServerUris[ii]);
                        }
                    }
                }

                // Load nodes
                nodeSet.Import(context, predefinedNodes);

                return nodeSet.Extensions;
            }

            return null;
        }

        /// <summary>
        /// Frees any resources allocated for the address space.
        /// </summary>
        public override void DeleteAddressSpace()
        {
            lock (Lock)
            {
                // TBD
            }
        }

        /// <summary>
        /// Returns a unique handle for the node.
        /// </summary>
        protected override NodeHandle GetManagerHandle(ServerSystemContext context, NodeId nodeId, IDictionary<NodeId, NodeState> cache)
        {
            lock (Lock)
            {
                // quickly exclude nodes that are not in the namespace. 
                if (!IsNodeIdInNamespace(nodeId))
                {
                    return null;
                }

                NodeState node = null;

                if (!PredefinedNodes.TryGetValue(nodeId, out node))
                {
                    return null;
                }

                NodeHandle handle = new NodeHandle();

                handle.NodeId = nodeId;
                handle.Node = node;
                handle.Validated = true;

                return handle;
            } 
        }

        /// <summary>
        /// Verifies that the specified node exists.
        /// </summary>
        protected override NodeState ValidateNode(ServerSystemContext context, NodeHandle handle, IDictionary<NodeId, NodeState> cache)
        {
            // not valid if no root.
            if (handle == null)
            {
                return null;
            }

            // check if previously validated.
            if (handle.Validated)
            {
                return handle.Node;
            }
            
            // TBD

            return null;
        }
        #endregion

        #region Overridden Methods
        #endregion

        #region Private Fields
        private ServerConfiguration m_configuration;
        private string[] filePath = { "NodeSet", "Refrigerators.xml" };
        private Timer m_RefrigeratorActualTemperatureTimer;
        private Timer m_RefrigeratorDoorTimer;

             

        private string[] m_RefrigeratorTemperature = {"ns=4;i=2010", "ns=4;i=2026", "ns=4;i=2037"};
        private string[] m_RefrigeratorDoor = {"", "", ""};
        private string[] m_RefrigeratorMethods = { "ns=4;i=2005" };

        private Random m_RandomGenerator = new Random();

        #endregion
    }
}
