using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        IMyBroadcastListener
                universalListener;

        public Program() {
            universalListener = IGC.RegisterBroadcastListener("CODE");
            universalListener.SetMessageCallback();
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & UpdateType.IGC) > 0) {
                if (universalListener != null && universalListener.HasPendingMessage) {
                    MyIGCMessage message = universalListener.AcceptMessage();
                    List<IMyTextPanel> panels = new List<IMyTextPanel>();
                    GridTerminalSystem.GetBlocksOfType(panels);
                    if (panels.Count > 0) panels[0].WriteText(message.Tag +" "+message.Source+" "+message.Data+"\n", true);
                }
            }
            else {
                universalListener = IGC.RegisterBroadcastListener(argument);
                universalListener.SetMessageCallback();
                Echo("CURRENT LISTENING CODE: " + argument);
            }
        }

    }
}
