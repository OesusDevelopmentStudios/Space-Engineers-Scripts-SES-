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
        private string RED_ALLERT = ">  RED ALERT <";

        private IMyProgrammableBlock EnergyControl;

        private List<IMyTextPanel> infoScreens;

        private List<IMyLightingBlock> primaryLights;
        private List<IMyLightingBlock> mudLights;

        private bool ON_ALERT = false;

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
            Runtime.UpdateFrequency = UpdateFrequency.None;
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

            ON_ALERT = false;
        }

        private void YellowStatus()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
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

            ON_ALERT = false;
        }

        private void RedStatus()
        {
            if (!ON_ALERT)
            {
                //Text
                foreach (IMyTextPanel panel in infoScreens)
                {
                    panel.BackgroundColor = Color.Red;
                    panel.FontColor = Color.Black;
                    panel.WriteText(">  " + RED_ALLERT + "  <");
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

                ON_ALERT = true;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
            }
            else Animation();
        }

        private void Animation()
        {
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.WriteText(" > " + RED_ALLERT + " < ");
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch(argument)
            {
                case "0": NormalStatus(); break;
                case "1": YellowStatus(); break;
                case "2": RedStatus(); break;
                default: NormalStatus(); break;
            }
        }
    }
}
