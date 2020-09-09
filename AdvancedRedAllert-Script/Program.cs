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
        private string RED_ALLERT = "RED ALLERT";

        private int ALLERT_STATE = 0;

        private IMyProgrammableBlock EnergyControl;

        private List<IMyTextPanel> infoScreens;

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
        }

        private bool IsOnThisGrid(IMyCubeBlock block)
        {
            if (Me.CubeGrid.Equals(block.CubeGrid)) return true;
            else return false;
        }

        private void NormalStatus()
        {
            foreach(IMyTextPanel panel in infoScreens)
            {
                panel.WriteText(NORMAL_ALLERT);
            }
        }

        private void YellowStatus()
        {
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.WriteText(YELLOW_ALLERT);
            }
        }

        private void RedStatus()
        {
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.WriteText(RED_ALLERT);
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch(argument)
            {
                case "0": NormalStatus(); break;
                case "1": YellowStatus(); break;
                case "2": RedStatus(); break;
            }
        }
    }
}
