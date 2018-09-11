using System;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Server;

namespace Server.Refrigerator
{
    public class RefrigeratorFactory
    {
        private string m_refrigeratorName;
        private ushort m_namespaceindex;
        private NodeFactory m_nodeFactory;
        private NodeState m_parentNode;
        private ServerSystemContext m_systemContext;

        internal NodeState Refrigerator;
        internal NodeState ActualTemperature;
        internal NodeState MotorTemperature;
        internal NodeState CoolingMotorRunning;
        internal NodeState DoorState;
        internal NodeState SetPointTemperature;
        internal NodeState LightStatus;
        internal NodeState State;
        private MethodState _openClose;

        private Random _numGenerator;

        /// <summary>
        /// Creates a refrigerator factory
        /// </summary>
        /// <param name="parentNode">The node that should old the refrigerators</param>
        /// <param name="refrigeratorName">The name for the new refrigerator</param>
        /// <param name="customManager">The node manager for the nodes created.</param>
        public RefrigeratorFactory(NodeState parentNode, string refrigeratorName, CustomNodeManager2 customManager)
        {
            m_refrigeratorName = refrigeratorName;
            m_namespaceindex = customManager.NamespaceIndex;
            m_systemContext = customManager.SystemContext;
            m_nodeFactory = new NodeFactory(customManager);
            m_parentNode = parentNode;
            _numGenerator = new Random();
            CreateRefrigerator();
        }

        /// <summary>
        /// Creates a new refrigerator.
        /// </summary>
        /// <returns>Returns a reference for the new refrigerator created.</returns>
        private void CreateRefrigerator()
        {
            Refrigerator = m_nodeFactory.CreateObject(m_parentNode, m_refrigeratorName, m_refrigeratorName, m_namespaceindex);
            ActualTemperature = m_nodeFactory.CreateVariable(Refrigerator, "ActualTemperature", "ActualTemperature", BuiltInType.Double, ValueRanks.Scalar, m_namespaceindex);
            MotorTemperature = m_nodeFactory.CreateVariable(Refrigerator, "MotorTemperature", "MotorTemperature", BuiltInType.Double, ValueRanks.Scalar, m_namespaceindex);
            CoolingMotorRunning = m_nodeFactory.CreateVariable(Refrigerator, "CoolingMotorRunning", "CoolingMotorRunning", BuiltInType.Boolean, ValueRanks.Scalar, m_namespaceindex);
            DoorState = m_nodeFactory.CreateVariable(Refrigerator, "DoorState", "DoorState", BuiltInType.Boolean, ValueRanks.Scalar, m_namespaceindex);
            SetPointTemperature = m_nodeFactory.CreateVariable(Refrigerator, "SetPointTemperature", "SetPointTemperature", BuiltInType.Boolean, ValueRanks.Scalar, m_namespaceindex);
            LightStatus = m_nodeFactory.CreateVariable(Refrigerator, "LightStatus", "LightStatus", BuiltInType.Boolean, ValueRanks.Scalar, m_namespaceindex);
            State = m_nodeFactory.CreateVariable(Refrigerator, "State", "State", BuiltInType.Boolean, ValueRanks.Scalar, m_namespaceindex);
            _openClose = m_nodeFactory.CreateMethod(Refrigerator, "OpenCloseDoor", "OpenCloseDoor", m_namespaceindex);
            SetOpenCloseArguments();
            
            // Set the Refrigerator into a Running State.
            Refrigerator.SetChildValue(m_systemContext, State.BrowseName, true, false);
            //return Refrigerator;
        }
        
        /// <summary>
        /// Adds Arguments to the method that was created.
        /// </summary>
        /// <returns>Returns the new Method that shall belong to a refrigerator.</returns>
        private void SetOpenCloseArguments()
        {
            // set input arguments
            _openClose.InputArguments = new PropertyState<Argument[]>(_openClose);
            _openClose.InputArguments.NodeId = new NodeId(_openClose.BrowseName.Name + "InArgs", m_namespaceindex);
            _openClose.InputArguments.BrowseName = BrowseNames.InputArguments;
            _openClose.InputArguments.DisplayName = _openClose.InputArguments.BrowseName.Name;
            _openClose.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
            _openClose.InputArguments.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            _openClose.InputArguments.DataType = DataTypeIds.Argument;
            _openClose.InputArguments.ValueRank = ValueRanks.OneDimension;

            _openClose.InputArguments.Value = new Argument[]
            {
                new Argument() { Name = "DoorState", Description = "When Set False Door Shall Be Closed, When Set To True Shall Be Open.",  DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar },
            };
            _openClose.OnCallMethod = OnOpenDoorCall;
        }

        /// <summary>
        /// Handles the open a close of the door in the refrigerator.
        /// </summary>
        /// <param name="context">Server Context</param>
        /// <param name="method">The method called.</param>
        /// <param name="inputarguments">The input arguments for the method that was called.</param>
        /// <param name="outputarguments">The output arguments for the method that was created.</param>
        /// <returns></returns>
        private ServiceResult OnOpenDoorCall(ISystemContext context, MethodState method, IList<object> inputarguments, IList<object> outputarguments)
        {
            if (inputarguments.Count != 1)
            {
                return StatusCodes.BadArgumentsMissing;
            }
            if (Refrigerator.SetChildValue(context, DoorState.BrowseName, (bool)inputarguments[0], false))
            {
                return ServiceResult.Good;
            }
            return StatusCodes.Bad;
        }
    }
}