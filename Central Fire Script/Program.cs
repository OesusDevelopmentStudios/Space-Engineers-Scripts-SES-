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

        List<IMyProgrammableBlock> turrets;
        List<IMyTextPanel> screens;

        const string
            TURRET_BASE = "TRRT-",
            MY_PREFIX   = "CENTRAL FIRE";

        int timeNo      = 0;

        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = "[" + ScriptName + "] Script";
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SayMyName(MY_PREFIX);
            GetMeTheTurrets();
        }

        public void GetMeTheTurrets() {
            List<IMyProgrammableBlock> 
                temp    = new List<IMyProgrammableBlock>();
                turrets = new List<IMyProgrammableBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyProgrammableBlock block in temp) {
                if (AreOnSameGrid(block, Me) && block.CustomName.Contains(TURRET_BASE))
                    turrets.Add(block);
            }
        }

        public void ShareInfo(string data) {
            foreach(IMyProgrammableBlock block in turrets) {
                block.CustomData = data;
                block.TryRun("reg");
            }
        }

        public void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (AreOnSameGrid(Me, screen) && screen.CustomName.Contains("[TRRT]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) screen.WriteText(message, append);
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {
                        case "reg":
                            string
                                content = Me.CustomData;

                            ShareInfo(content);
                            break;
                    }
                }
            }
            else {
                Output("");
                if (timeNo++ > 60) {
                    timeNo = 0;
                    GetMeTheTurrets();
                }
            }
        }
    }
}
