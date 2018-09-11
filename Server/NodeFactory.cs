using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Test;

namespace Server
{
    public class NodeFactory
    {
        private DataGenerator m_generator;
        private CustomNodeManager2 m_customManager;

        public NodeFactory(CustomNodeManager2 customManager)
        {
            this.m_customManager = customManager;
        }

        internal FolderState CreateFolder(NodeState parent, string path, string name, ushort namespaceIndex)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;
            folder.NodeId = new NodeId(path, namespaceIndex);
            folder.BrowseName = new QualifiedName(path, namespaceIndex);
            folder.DisplayName = new LocalizedText("en", name);
            folder.WriteMask = AttributeWriteMask.None;
            folder.UserWriteMask = AttributeWriteMask.None;
            folder.EventNotifier = EventNotifiers.None;

            if (parent != null)
            {
                parent.AddChild(folder);
            }

            return folder;
        }
        
        internal MethodState CreateMethod(NodeState parent, string path, string name, ushort namespaceIndex)
        {
            MethodState method = new MethodState(parent);

            method.SymbolicName = name;
            method.ReferenceTypeId = ReferenceTypeIds.HasComponent;
            method.NodeId = new NodeId(path, namespaceIndex);
            method.BrowseName = new QualifiedName(path, namespaceIndex);
            method.DisplayName = new LocalizedText("en", name);
            method.WriteMask = AttributeWriteMask.None;
            method.UserWriteMask = AttributeWriteMask.None;
            method.Executable = true;
            method.UserExecutable = true;

            if (parent != null)
            {
                parent.AddChild(method);
            }

            return method;
        }

        internal BaseObjectState CreateObject(NodeState parent, string path, string name, ushort namespaceIndex)
        {
            BaseObjectState folder = new BaseObjectState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.BaseObjectType;
            folder.NodeId = new NodeId(path, namespaceIndex);
            folder.BrowseName = new QualifiedName(name, namespaceIndex);
            folder.DisplayName = folder.BrowseName.Name;
            folder.WriteMask = AttributeWriteMask.None;
            folder.UserWriteMask = AttributeWriteMask.None;
            folder.EventNotifier = EventNotifiers.None;

            if (parent != null)
            {
                parent.AddChild(folder);
            }

            return folder;
        }

        internal BaseDataVariableState CreateVariable(NodeState parent, string path, string name, BuiltInType dataType, int valueRank, ushort namespaceindex)
        {
            return CreateVariable(parent, path, name, (uint)dataType, valueRank, namespaceindex);
        }

        internal BaseDataVariableState CreateVariable(NodeState parent, string path, string name, NodeId dataType, int valueRank, ushort namespaceindex)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);

            variable.SymbolicName = name;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
            variable.NodeId = new NodeId(path, namespaceindex);
            variable.BrowseName = new QualifiedName(path, namespaceindex);
            variable.DisplayName = new LocalizedText("en", name);
            variable.WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.DataType = dataType;
            variable.ValueRank = valueRank;
            variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.Historizing = false;
            variable.Value = GetNewValue(variable);
            variable.StatusCode = StatusCodes.Good;
            variable.Timestamp = DateTime.UtcNow;

            if (valueRank == ValueRanks.OneDimension)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 });
            }
            else if (valueRank == ValueRanks.TwoDimensions)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0, 0 });
            }

            if (parent != null)
            {
                parent.AddChild(variable);
            }

            return variable;
        }

        internal BaseDataVariableState[] CreateVariables(NodeState parent, string path, string name, BuiltInType dataType, int valueRank, UInt16 numVariables, ushort namespaceindex)
        {
            return CreateVariables(parent, path, name, (uint)dataType, valueRank, numVariables, namespaceindex);
        }

        internal BaseDataVariableState[] CreateVariables(NodeState parent, string path, string name, NodeId dataType, int valueRank, UInt16 numVariables, ushort namespaceindex)
        {
            // first, create a new Parent folder for this data-type
            FolderState newParentFolder = CreateFolder(parent, path, name, namespaceindex);

            List<BaseDataVariableState> itemsCreated = new List<BaseDataVariableState>();
            // now to create the remaining NUMBERED items
            for (uint i = 0; i < numVariables; i++)
            {
                string newName = string.Format("{0}_{1}", name, i.ToString("00"));
                string newPath = string.Format("{0}_{1}", path, newName);
                itemsCreated.Add(CreateVariable(newParentFolder, newPath, newName, dataType, valueRank, namespaceindex));
            }
            return (itemsCreated.ToArray());
        }
        
        private object GetNewValue(BaseVariableState variable)
        {
            if (m_generator == null)
            {
                m_generator = new Opc.Ua.Test.DataGenerator(null);
                m_generator.BoundaryValueFrequency = 0;
            }

            object value = null;

            while (value == null)
            {
                value = m_generator.GetRandom(variable.DataType, variable.ValueRank, new uint[] { 10 }, m_customManager.Server.TypeTree);
            }

            return value;
        }
    }
}