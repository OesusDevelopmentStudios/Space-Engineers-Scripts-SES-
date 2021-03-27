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

namespace IngameScript{
    partial class Program : MyGridProgram{

        public Program(){
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void Main() {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            //int counter = 0;
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("ANTIMISSILE-")) {
                    string toParse = pb.CustomName.Substring(12);
                    Echo(toParse + "\n");
                }
            }

            //Echo(all + " " + those);
        }
    }
}
