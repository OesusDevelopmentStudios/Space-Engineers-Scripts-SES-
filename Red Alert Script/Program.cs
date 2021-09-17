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
            ROTAT_LIGHT,
            OTHER_LIGHT,
            SRED_LIGHT,
            SWITCH_LIGHT,
            ALARM_LIGHT;

        bool
            onAlert;

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
            ROTAT_LIGHT = "Rotating Light";
            OTHER_LIGHT = "Light";
            SRED_LIGHT = "Static Red Light";
            SWITCH_LIGHT = "Switch Light";
            ALARM_LIGHT = "Alarm Light";
        }

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
            List<IMyLightingBlock> 
                result = new List<IMyLightingBlock>(),
                temp = new List<IMyLightingBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyLightingBlock light in temp) {
                if (IsOnThisGrid(light) && light.CustomName.Contains(name))
                    result.Add(light);
            }

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

        public void SwitchRedAlert() {
            List<IMyLightingBlock> rotatLights = GetLights(ROTAT_LIGHT);
            List<IMyLightingBlock> alarmLights = GetLights(ALARM_LIGHT);
            List<IMyLightingBlock> switchLights= GetLights(SWITCH_LIGHT);
            List<IMyLightingBlock> otherLights = GetLights(OTHER_LIGHT);
            List<IMyLightingBlock> sredLights  = GetLights(SRED_LIGHT);

            onAlert = !onAlert;

            if (onAlert) {   // turn the <s> Fucking Furries </s> on
                ChangeLightsColor   (otherLights,   new Color( 60,  0,  0));
                ChangeLightsColor   (switchLights,  new Color(  0,  0,255));
                ChangeLightsColor   (alarmLights,   new Color(  0,  0,255));
                ChangeLightsColor   (sredLights,    new Color( 60,  0,  0));
                ChangeLightsStatus  (rotatLights, true);
                ChangeLightsStatus  (alarmLights, true);
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
            }
            else {   // turn the <s> Fucking Furries </s> off
                ChangeLightsColor   (otherLights,   new Color(255,255,255));
                ChangeLightsColor   (switchLights,  new Color(255,255,255));
                ChangeLightsColor   (sredLights,    new Color(255,255,255));
                ChangeLightsStatus  (rotatLights, false);
                ChangeLightsStatus  (alarmLights, false);
                foreach (IMyLightingBlock l in otherLights) {
                    l.BlinkIntervalSeconds = 0;
                    l.BlinkOffset = 50f;
                }
                foreach (IMyLightingBlock l in switchLights) {
                    l.BlinkIntervalSeconds = 0;
                }
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
                                bl.CustomName += ROTAT_LIGHT;
                            }
                            else
                            if(bl.BlockDefinition.SubtypeName.Equals("LargeBlockFrontLight")
                               || bl.BlockDefinition.SubtypeName.Equals("SmallBlockFrontLight")) {
                                bl.CustomName += "Spotlight";
                            }
                            else {
                                bl.CustomName += OTHER_LIGHT;
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
