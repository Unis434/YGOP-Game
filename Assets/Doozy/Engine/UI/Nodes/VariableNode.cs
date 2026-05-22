// Copyright (c) 2015 - 2020 Doozy Entertainment. All Rights Reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement
// A Copy of the EULA APPENDIX 1 is available at http://unity3d.com/company/legal/as_terms

using System.Collections.Generic;
using Doozy.Engine.Nody.Attributes;
using Doozy.Engine.Nody.Connections;
using Doozy.Engine.Nody.Models;
using Doozy.Engine.UI.Connections;
using Doozy.Engine.Utils;
using UnityEngine;

namespace Doozy.Engine.UI.Nodes
{
    /// <summary>
    /// </summary>
    [NodeMenu(MenuUtils.VariableNode_CreateNodeMenu_Name, MenuUtils.VariableNode_CreateNodeMenu_Order)]
    public class VariableNode : Node
    {
        public static string NextNodeName { get; set; }

        public override void OnCreate()
        {
            base.OnCreate();
            CanBeDeleted = true;
            SetNodeType(NodeType.General);
            SetName(UILabels.VariableNodeName);
            SetAllowDuplicateNodeName(true);
        }

        public override void AddDefaultSockets()
        {
            base.AddDefaultSockets();
            AddInputSocket(ConnectionMode.Multiple, typeof(PassthroughConnection), false, false);
            AddOutputSocket(ConnectionMode.Override, typeof(NamedConnection), true, false);
            AddOutputSocket(ConnectionMode.Override, typeof(NamedConnection), true, false);
        }

        public override void OnEnter(Node previousActiveNode, Connection connection)
        {
            base.OnEnter(previousActiveNode, connection);
            if (ActiveGraph == null) return;
            SelectNamedOutputSocket();
        }

        private void SelectNamedOutputSocket()
        {
            foreach (Socket socket in OutputSockets)
            {
                if (!socket.IsConnected) continue;

                NamedConnection connection = NamedConnection.GetValue(socket);
                if (connection.Name == NextNodeName)
                {
                    ActiveGraph.SetActiveNodeByConnection(socket.FirstConnection);
                    return;
                }
            }
        }
    }
}