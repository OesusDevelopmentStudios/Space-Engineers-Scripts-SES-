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

        public void Main(string argument, UpdateType updateSource) {
            int index;
            if(argument!=null && argument.Length>0 && int.TryParse(argument, out index)) {

            }
            else
                index = 0;

            List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
            IMyBatteryBlock battery;
            GridTerminalSystem.GetBlocksOfType(batteries);
            if (batteries.Count > 0) {
                battery = batteries.ElementAt(index);
                Echo(battery.EntityId + " " + battery.GetId());
            }
        }
    }
}
