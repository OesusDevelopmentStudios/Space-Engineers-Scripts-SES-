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
    partial class Program : MyGridProgram {// UTILITY

        readonly string 
            SHIP_NAME = "",
            SOUND_BLOCK,
            ROTAT_LIGHT,
            OTHER_LIGHT,
            SRED_LIGHT,
            SWITCH_LIGHT,
            ALARM_LIGHT;

        bool
            onAlert;

        IMyProgrammableBlock EnergyControl;

        void GetECBlock() {
            List<IMyProgrammableBlock> Progs = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(Progs);
            foreach(IMyProgrammableBlock block in Progs) {
                if (IsOnThisGrid(block) && block.CustomName.Contains("[ENERGY CONTROL]")) {
                    EnergyControl = block;
                    return;
                }
            }
        }

        bool IsOnThisGrid(IMyCubeBlock block) { return (Me.CubeGrid.Equals(block.CubeGrid)); }

        void SayMyName(string ScriptName, float textSize = 2f) {
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        public Program() {
            SHIP_NAME = Me.CubeGrid.CustomName;
            SayMyName("RED ALERT");
            onAlert = false;
            SOUND_BLOCK = SHIP_NAME + "/Sound Block";
            ROTAT_LIGHT = SHIP_NAME + "/Rotating Light";
            OTHER_LIGHT = SHIP_NAME + "/Light";
            SRED_LIGHT = SHIP_NAME + "/Static Red Light";
            SWITCH_LIGHT = SHIP_NAME + "/Switch Light";
            ALARM_LIGHT = SHIP_NAME + "/Alarm Light";
            GetECBlock();
        }

        public List<IMyLightingBlock> GetRotatingLights() { return GetLights(ROTAT_LIGHT); }

        public List<IMyLightingBlock> GetOtherLights() { return GetLights(OTHER_LIGHT); }

        public List<IMyLightingBlock> GetAlarmLights() { return GetLights(ALARM_LIGHT); }

        public List<IMyLightingBlock> GetSwtchLights() { return GetLights(SWITCH_LIGHT); }

        public List<IMyLightingBlock> GetSredLights() { return GetLights(SRED_LIGHT); }

        public void SetWeapons(bool turn) {
            List<IMyLargeTurretBase> temp = new List<IMyLargeTurretBase>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyLargeTurretBase tb in temp) {
                tb.Enabled = turn;
            }
        }

        public void CloseAllDoors() {
            List<IMyDoor> temp = new List<IMyDoor>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyDoor d in temp) {
                d.CloseDoor();
            }
        }

        public List<IMyLightingBlock> GetLights(string name) {
            List<IMyLightingBlock> result = new List<IMyLightingBlock>();
            List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(name, temp);

            foreach (IMyTerminalBlock b in temp) {
                if (b is IMyLightingBlock) {
                    IMyLightingBlock tempo = b as IMyLightingBlock;
                    result.Add(tempo);
                }
            }
            temp.Clear();

            return result;
        }

        public void ChangeLightsStatus(List<IMyLightingBlock> target, bool turnon) {
            foreach (IMyLightingBlock LB in target) {
                LB.Enabled = turnon;
            }
        }

        public void ChangeLightsColor(List<IMyLightingBlock> target, Color color) {
            foreach (IMyLightingBlock LB in target) {
                LB.Color = color;
            }
        }

        public List<IMyTextPanel> GetSupportScreens() {
            List<IMyTextPanel>  
                temp = new List<IMyTextPanel>(),
                output = new List<IMyTextPanel>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyTextPanel panel in temp) {
                if (IsOnThisGrid(panel) && panel.CustomName.Contains("[RED ALERT]")) output.Add(panel);
            }

            return output;
        }

        public void Output(string input) {
            List<IMyTextPanel> screens = GetSupportScreens();
            if(screens.Count>0)
                foreach (IMyTextPanel screen in screens) {
                    if (screen != null) {
                        screen.FontSize = (float)1.9;
                        screen.Alignment = TextAlignment.CENTER;
                        screen.ContentType = ContentType.TEXT_AND_IMAGE;
                        screen.WriteText(input, false);
                    }
                }
            else Echo(input);
        }

        //Main Functions

        public void SwitchRedAlert() {
            List<IMyLightingBlock> rotatLights = GetRotatingLights();
            List<IMyLightingBlock> alarmLights = GetAlarmLights();
            List<IMyLightingBlock> switchLights= GetSwtchLights();
            List<IMyLightingBlock> otherLights = GetOtherLights();
            List<IMyLightingBlock> sredLights  = GetSredLights();
            IMySoundBlock alarmBlock = GridTerminalSystem.GetBlockWithName(SOUND_BLOCK) as IMySoundBlock;

            onAlert = !onAlert;

            if (onAlert) {   // turn the <s> Fucking Furries </s> on
                ChangeLightsColor   (otherLights,   new Color( 60,  0,  0));
                ChangeLightsColor   (switchLights,  new Color(  0,  0,255));
                ChangeLightsColor   (alarmLights,   new Color(  0,  0,255));
                ChangeLightsColor   (sredLights,    new Color( 60,  0,  0));
                ChangeLightsStatus  (rotatLights, true);
                ChangeLightsStatus  (alarmLights, true);
                if (alarmBlock != null) alarmBlock.Play();
                foreach (IMyLightingBlock l in otherLights) {
                    l.BlinkLength = 50f;
                    l.BlinkIntervalSeconds = 2;
                }
                foreach (IMyLightingBlock l in switchLights) {
                    l.BlinkLength = 50f;
                    l.BlinkIntervalSeconds = 2;
                }
                SetWeapons(true);
                CloseAllDoors();
                Output("\nRED ALERT");
                if (EnergyControl != null) EnergyControl.TryRun("COMBAT");
            }
            else {   // turn the <s> Fucking Furries </s> off
                ChangeLightsColor   (otherLights,   new Color(255,255,255));
                ChangeLightsColor   (switchLights,  new Color(255,255,255));
                ChangeLightsColor   (sredLights,    new Color(255,255,255));
                ChangeLightsStatus  (rotatLights, false);
                ChangeLightsStatus  (alarmLights, false);
                if (alarmBlock != null) alarmBlock.Stop();
                foreach (IMyLightingBlock l in otherLights) {
                    l.BlinkIntervalSeconds = 0;
                    l.BlinkOffset = 50f;
                }
                foreach (IMyLightingBlock l in switchLights) {
                    l.BlinkIntervalSeconds = 0;
                }
                Output("\nIN ORDER");
                if (EnergyControl != null) EnergyControl.TryRun("NORMAL");
            }
        }

        public bool CheckForIgnore(IMyTerminalBlock block) {
            string name = block.CustomName.ToLower();
            if(name.Contains("(ignore)")|| name.Contains("[ignore]")) return true;
            return false;
        }

        public string UnpackList(List<object> list) {
            string output = "";
            foreach(object obj in list) {
                output += obj.ToString() + "\n";
            }
            return output;
        }

        public string UnpackList(List<ITerminalAction> list) {
            string output = "";
            foreach (ITerminalAction obj in list) {
                output += obj.Name + "\n";
            }
            return output;
        }

        public void Main(string argument, UpdateType updateSource) {
            /**/

            String[] eval = argument.Split(' ');

            if(eval.Length<=0)
                SwitchRedAlert();
            else
            switch (eval[0].ToLower()) {
                case "lazy":
                    List<IMyLightingBlock> temp = new List<IMyLightingBlock>();
                    GridTerminalSystem.GetBlocksOfType(temp);
                    foreach (IMyLightingBlock bl in temp) {
                        if (
                            !CheckForIgnore(bl) &&
                            !bl.CustomName.Equals(ALARM_LIGHT) &&
                            !bl.CustomName.Equals(ROTAT_LIGHT) &&
                            !bl.CustomName.Equals(SWITCH_LIGHT) &&
                            !bl.CustomName.Equals(OTHER_LIGHT) &&
                            bl.CubeGrid.Equals(Me.CubeGrid)
                        ) {
                            if(bl.BlockDefinition.SubtypeName.Equals("RotatingLightLarge")
                               || bl.BlockDefinition.SubtypeName.Equals("RotatingLightSmall")) {
                                bl.CustomName = ROTAT_LIGHT;
                            }
                            else
                            if(bl.BlockDefinition.SubtypeName.Equals("LargeBlockFrontLight")
                               || bl.BlockDefinition.SubtypeName.Equals("SmallBlockFrontLight")) {
                                bl.CustomName = SHIP_NAME+"/Spotlight";
                            }
                            else {
                                bl.CustomName = OTHER_LIGHT;
                            }
                        }
                    }
                    break;

                case "test":
                        string chckName = argument.Substring(eval[0].Length+1);
                        IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName(chckName) as IMyTerminalBlock;
                        if (block == null) {
                            Echo("UnU");
                            return;
                        }
                        Echo(block.GetType().FullName);
                        List<ITerminalAction> list = new List<ITerminalAction>();
                        block.GetActions(list);
                        Echo(block.BlockDefinition.SubtypeId);
                        Echo(UnpackList(list));
                    break;


                default: SwitchRedAlert(); break;
            }
        }
    }
}
