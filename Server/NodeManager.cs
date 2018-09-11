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
using Opc.Ua.Test;
using Server.Boiler;
using Server.Refrigerator;
using ObjectIds = Opc.Ua.ObjectIds;
using Objects = Server.Boiler.Objects;
using ReferenceTypes = Opc.Ua.ReferenceTypes;


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
        public NodeManager(IServerInternal server, ApplicationConfiguration configuration):base(server, configuration)
        {
            SystemContext.NodeIdFactory = this;

            // get the configuration for the node manager.
            m_configuration = configuration.ParseExtension<ServerConfiguration>();

            // use suitable defaults if no configuration exists.
            if (m_configuration == null)
            {
                m_configuration = new ServerConfiguration();
            }


            string[] namespaceUrls = new string[4];
            namespaceUrls[0] = global::Server.Boiler.Namespaces.Boiler;
            namespaceUrls[1] = global::Server.Boiler.Namespaces.Boiler + "/Instance";
            namespaceUrls[2] = Namespaces.DemoServer;
            namespaceUrls[3] = Namespaces.DemoServer + "/Instance";
            SetNamespaces(namespaceUrls);




            








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
                IList<IReference> references = null;

                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                var myNodeFactory = new NodeFactory(this);
                try
                {
                    LoadPredefinedNodes(SystemContext, externalReferences);
                    // start a simulation that changes the values of the nodes.
                    //// Folder to organize the refrigerator
                    FolderState refrigeratorOrganizer = myNodeFactory.CreateFolder(null, "Refrigerators", "Refrigerators", NamespaceIndex);
                    refrigeratorOrganizer.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, refrigeratorOrganizer.NodeId));
                    AddRootNotifier(refrigeratorOrganizer);
                    // Add Refrigerators
                    for (int i = 1; i <= 5; i++)
                    {
                        var fridgeFactory = new RefrigeratorFactory(refrigeratorOrganizer, "Refrigerator " + i, this);
                        m_refrigeratorBuffer.Add(fridgeFactory, new Timer(AttachTemperatureSimulator, fridgeFactory, 5000, 5000));
                    }
                    AddPredefinedNode(SystemContext, refrigeratorOrganizer);

                    //// Folder to organize the boiler
                    FolderState boilerOrganizer = myNodeFactory.CreateFolder(null, "Boilers", "Boilers", NamespaceIndex);
                    boilerOrganizer.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, boilerOrganizer.NodeId));
                    AddRootNotifier(boilerOrganizer);
                    //// Add Boiler
                    for (uint i = 1; i <= 5; i++)
                    {
                        var boilerFactory = new BoilerFactory(boilerOrganizer, "Boiler" + i, this, i);
                        m_boilerBuffer.Add(boilerFactory, null);
                    }
                    AddPredefinedNode(SystemContext, boilerOrganizer);
                }
                catch (Exception e)
                {
                    Utils.Trace(Utils.TraceMasks.Error, e.Message, "");
                }
            }
        }
        /// <summary>
        /// Attach Temperature Simulator for the Boiler
        /// </summary>
        /// <param name="refrigeratorFactory"></param>
        private void AttachTemperatureSimulator(object refrigeratorFactory)
        {
            lock (Lock)
            {
                try
                {
                    var factory = (RefrigeratorFactory)refrigeratorFactory;

                    // Sets Actual Temperature
                    factory.Refrigerator.SetChildValue(SystemContext, factory.ActualTemperature.BrowseName, m_randomizer.NextDouble() * (2.0 - (-5.0)) + (-5.0), false);
                    // Set Motor Temperature Simulator
                    factory.Refrigerator.SetChildValue(SystemContext, factory.MotorTemperature.BrowseName, m_randomizer.NextDouble() * (90.0 - 20.0) + 20.0, false);
                }
                catch (Exception e)
                {
                    Utils.Trace(e, "Unexpected error doing simulation.");
                }

                
            }
        }

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
        private DataGenerator randomDataGenerator = new Opc.Ua.Test.DataGenerator(null);

        private ServerConfiguration m_configuration;

        private Dictionary<RefrigeratorFactory, Timer> m_refrigeratorBuffer = new Dictionary<RefrigeratorFactory, Timer>();
        private Dictionary<BoilerFactory, Timer> m_boilerBuffer = new Dictionary<BoilerFactory, Timer>();
        private BoilerState m_boiler2;
        private Random m_randomizer = new Random();
        #endregion
    }
}