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

        IMyProgrammableBlock Radar_Controller;

        List<IMyProgrammableBlock> turrets;
        List<IMyLargeTurretBase> genericTurrets;
        List<IMyTextPanel> screens;

        const string
            TURRET_BASE = "TRRT-",
            MY_PREFIX   = "CFC";

        string content  = "";

        int timeNo = 0,
            ticksWOOrders = 0;

        string GetFullScriptName(string ScriptName) { return "[" + ScriptName + "] Script"; }
        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = GetFullScriptName(ScriptName);
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText("\n\n" + ScriptName);
        }

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        bool SetRadarControl() {
            /// this should be okay if there are no ships with Radar Control Script docked to the main ship
            Radar_Controller = GridTerminalSystem.GetBlockWithName(GetFullScriptName("RADAR")) as IMyProgrammableBlock;

            /// if the programmable block we picked is not from this ship, we commence the search to find it anyway
            if (Radar_Controller != null && !AreOnSameGrid(Me, Radar_Controller)) {
                List<IMyProgrammableBlock> temp = new List<IMyProgrammableBlock>();
                Radar_Controller = null;
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach(IMyProgrammableBlock prog in temp) {
                    if (AreOnSameGrid(prog, Me) && prog.CustomName.Equals(GetFullScriptName("RADAR"))) {
                        Radar_Controller = prog; return true;
                    }
                }
            }
            /// and if we fail... welp, we can just inform the rest of the script that we can't do nothing
            return Radar_Controller != null;
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SetRadarControl();
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

            List<IMyLargeTurretBase>
                tmp             = new List<IMyLargeTurretBase>();
                genericTurrets  = new List<IMyLargeTurretBase>();

            GridTerminalSystem.GetBlocksOfType(tmp);
            foreach (IMyLargeTurretBase block in tmp) {
                if (AreOnSameGrid(block, Me))
                    genericTurrets.Add(block);
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

        public string Stringify(MyDetectedEntityInfo entity) {
            return
                entity.IsEmpty()?
                "":
                entity.EntityId.ToString() + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Z * 10)) + ";" +

                string.Format("{0:0.}", Math.Round((double)entity.Velocity.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Z * 10)) + "\n";
        }

        public string GetGenericTargettingData() {
            string output = "";
            Dictionary<long, MyDetectedEntityInfo> targets = new Dictionary<long, MyDetectedEntityInfo>();

            foreach (IMyLargeTurretBase turret in genericTurrets) {
                MyDetectedEntityInfo entity = turret.GetTargetedEntity();
                if (!targets.ContainsKey(entity.EntityId)) {
                    targets.Add(entity.EntityId, entity);
                    output += Stringify(entity);
                }
            }

            targets.Clear();
            return output;
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {
                        case "reg":
                            if (Radar_Controller == null) {
                                if (!SetRadarControl()) break;
                            }
                            
                            content = Radar_Controller.CustomData;

                            //ShareInfo(content);
                            /**/
                            break;
                    }
                }
            }
            else {
                Output("");
                if (content.Length > 0) {
                    ShareInfo(content + GetGenericTargettingData());
                    ticksWOOrders = 0;
                    content = "";
                }
                else {
                    if (ticksWOOrders >= 10) {
                        ShareInfo(GetGenericTargettingData());
                    }
                    else ticksWOOrders++;
                }

                if (timeNo++ >= 120) {
                    timeNo = 0;
                    GetMeTheTurrets();
                }
            }
        }
    }
}
