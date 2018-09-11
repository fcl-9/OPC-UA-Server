using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Server;
using Server;
using Server.Boiler;

namespace Opc.Ua
{
    public class BoilerFactory
    {
        private string m_BoilerName;
        private ushort m_namespaceindex;
        private NodeFactory m_nodeFactory;
        private NodeState m_parentNode;
        private CustomNodeManager2 m_nodeManager;

        internal BoilerState Boiler;

        private static uint countBoilers = 10000;
        
        /// <summary>
        /// Creates a new boiler for each instantiation of this class
        /// </summary>
        /// <param name="parentNode"></param>
        /// <param name="boilerName"></param>
        /// <param name="customManager"></param>
        public BoilerFactory(NodeState parentNode, string boilerName, CustomNodeManager2 customManager, uint count)
        {
            m_BoilerName = boilerName;
            m_namespaceindex = customManager.NamespaceIndex;
            m_nodeFactory = new NodeFactory(customManager);
            m_parentNode = parentNode;
            m_nodeManager = customManager;
            countBoilers = countBoilers + count; 
            CreateBoiler();
        }

        private void CreateBoiler()
        {
            Boiler = new BoilerState(null);
            Boiler.Create(m_nodeManager.SystemContext, new NodeId(countBoilers, m_namespaceindex), new QualifiedName(m_BoilerName, m_nodeManager.NamespaceIndexes[0]), null, true);

            IList<BaseInstanceState> children = new List<BaseInstanceState>();
            Boiler.GetChildren(m_nodeManager.SystemContext, children);
            RecursiveNodeIdChanger(children);

            List<IReference> references = new List<IReference>();
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, Boiler.NodeId));
            m_parentNode.AddChild(Boiler);
            
        }

        private void RecursiveNodeIdChanger(IList<BaseInstanceState> children)
        {
            if (children.Count == 0)
            {
                return;
            }
            foreach (var child in children)
            {
                child.NodeId = new NodeId((uint)child.NodeId.Identifier + countBoilers, m_namespaceindex);
                children = new List<BaseInstanceState>();
                child.GetChildren(m_nodeManager.SystemContext, children);
                if (children.Count == 0)
                {
                    return;
                }
                RecursiveNodeIdChanger(children);
            }
        }

    }
}