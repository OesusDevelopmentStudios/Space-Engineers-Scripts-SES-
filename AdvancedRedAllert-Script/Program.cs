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

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private string NORMAL_ALLERT = "STATUS: NORMAL";
        private string YELLOW_ALLERT = "STATUS: YELLOW";
        private string RED_ALLERT = "RED ALERT";

        private IMyProgrammableBlock EnergyControl;

        private List<IMyTextPanel> infoScreens;

        private List<IMyLightingBlock> primaryLights;
        private List<IMyLightingBlock> mudLights;

        private int ANIM_STATE = 0;
        private int SLOW = 0;

        public Program()
        {
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            infoScreens = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(infoScreens);
            foreach(IMyTextPanel panel in infoScreens)
            {
                if (IsOnThisGrid(panel) && panel.CustomName.Contains("INFO_SCREEN"))
                {
                    temp.Add(panel);
                }
            }
            infoScreens = temp;

            List<IMyLightingBlock> temp2 = new List<IMyLightingBlock>();
            primaryLights = new List<IMyLightingBlock>();
            mudLights = new List<IMyLightingBlock>();
            GridTerminalSystem.GetBlocksOfType(temp2);
            foreach(IMyLightingBlock light in temp2)
            {
                if(IsOnThisGrid(light))
                {
                    if (light.CustomName.Contains("NORMAL_LIGHTS"))
                    {
                        primaryLights.Add(light);
                    }
                    else if (light.CustomName.Contains("MUD_LIGHTS"))
                    {
                        mudLights.Add(light);
                    }
                }
            }

        }

        private bool IsOnThisGrid(IMyCubeBlock block)
        {
            if (Me.CubeGrid.Equals(block.CubeGrid)) return true;
            else return false;
        }

        private void NormalStatus()
        {
                //Text
                foreach (IMyTextPanel panel in infoScreens)
                {
                    panel.BackgroundColor = Color.Black;
                    panel.FontColor = Color.Green;
                    panel.WriteText(NORMAL_ALLERT);
                }
                //Lights
                foreach (IMyLightingBlock light in primaryLights)
                {
                    light.Intensity = 10;
                }
                foreach (IMyLightingBlock light in mudLights)
                {
                    light.Color = Color.White;
                    light.BlinkIntervalSeconds = 0;
                }
        }

        private void YellowStatus()
        {
            //Text
            foreach (IMyTextPanel panel in infoScreens)
            {
                //Text
                panel.BackgroundColor = Color.Black;
                panel.FontColor = Color.Yellow;
                panel.WriteText(YELLOW_ALLERT);
            }
            //Lights
            foreach (IMyLightingBlock light in primaryLights)
            {
                light.Intensity = 10;
            }
            foreach (IMyLightingBlock light in mudLights)
            {
                light.Color = Color.Yellow;
                light.BlinkIntervalSeconds = 2;
                light.BlinkLength = 50;
            }
        }

        private void RedStatus()
        {
            //Text
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.BackgroundColor = Color.Red;
                panel.FontColor = Color.Black;
                panel.WriteText(">     " + RED_ALLERT + "     <");
            }
            //Lights
            foreach (IMyLightingBlock light in primaryLights)
            {
                light.Intensity = 4;
            }
            foreach (IMyLightingBlock light in mudLights)
            {
                light.Color = Color.Red;
                light.BlinkIntervalSeconds = 2;
                light.BlinkLength = 50;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        private void Animation()
        {
            switch (ANIM_STATE)
            {
                case 0:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText(" >    " + RED_ALLERT + "    < ");
                    }
                    if (SLOW == 4) { ANIM_STATE = 1; SLOW = 0; }
                    SLOW++;
                } break;
                case 1:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText("  >   " + RED_ALLERT + "   <  ");
                    }
                    if (SLOW == 4) { ANIM_STATE = 2; SLOW = 0; }
                    SLOW++;
                    } break;
                case 2:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText("   >  " + RED_ALLERT + "  <   ");
                    }
                    if (SLOW == 4) { ANIM_STATE = 3; SLOW = 0; }
                    SLOW++;
                    } break;
                case 3:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText("    > " + RED_ALLERT + " <    ");
                    }
                    if (SLOW == 4) { ANIM_STATE = 4; SLOW = 0; }
                    SLOW++;
                    } break;
                case 4:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText("     >" + RED_ALLERT + "<     ");
                    }
                    if (SLOW == 4) { ANIM_STATE = 5; SLOW = 0; }
                    SLOW++;
                    } break;
                case 5:
                {
                    foreach (IMyTextPanel panel in infoScreens)
                    {
                        panel.WriteText(">     " + RED_ALLERT + "     <");
                    }
                    if (SLOW == 4) { ANIM_STATE = 0; SLOW = 0; }
                    SLOW++;
                    } break;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                switch (argument)
                {              
                    case "0": NormalStatus(); break;
                    case "1": YellowStatus(); break;
                    case "2": RedStatus(); break;
                    default: NormalStatus(); break;
                }
            }
            else Animation();
        }
    }
}
