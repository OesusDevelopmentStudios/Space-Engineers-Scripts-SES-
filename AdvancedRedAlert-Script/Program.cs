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
        private string NORMAL_ALERT = "STATUS: NORMAL";
        private string YELLOW_ALERT = "STATUS: YELLOW";
        private string RED_ALERT = "RED ALERT";

        private IMyProgrammableBlock EnergyControl;

        private List<IMyTextPanel> infoScreens;

        private List<IMyLightingBlock> primaryLights;
        private List<IMyLightingBlock> moodLights;

        private List<IMySoundBlock> woofers;

        private List<IMyAirtightHangarDoor> hangarbay;
        private List<IMyAirtightHangarDoor> cannonDoor;

        private List<IMySensorBlock> sensorBlocks;

        private List<IMyAirVent> airVents;

        private int ANIM_STATE = 0;
        private int SLOW = 0;

        public Program()
        {
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            infoScreens = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(infoScreens);
            foreach (IMyTextPanel panel in infoScreens)
            {
                if (IsOnThisGrid(panel) && panel.CustomName.Contains("INFO_SCREEN"))
                {
                    temp.Add(panel);
                }
            }
            infoScreens = temp;

            List<IMyLightingBlock> temp2 = new List<IMyLightingBlock>();
            primaryLights = new List<IMyLightingBlock>();
            moodLights = new List<IMyLightingBlock>();
            GridTerminalSystem.GetBlocksOfType(temp2);
            foreach (IMyLightingBlock light in temp2)
            {
                if (IsOnThisGrid(light))
                {
                    if (light.CustomName.Contains("NORMAL_LIGHTS"))
                    {
                        primaryLights.Add(light);
                    }
                    else if (light.CustomName.Contains("MOOD_LIGHTS"))
                    {
                        moodLights.Add(light);
                    }
                }
            }

            List<IMySoundBlock> temp3 = new List<IMySoundBlock>();
            woofers = new List<IMySoundBlock>();
            GridTerminalSystem.GetBlocksOfType(temp3);
            foreach (IMySoundBlock music in temp3)
            {
                if (IsOnThisGrid(music) && music.CustomName.Contains("PARTY_HARD"))
                {
                    woofers.Add(music);
                }
            }

            List<IMyAirtightHangarDoor> temp4 = new List<IMyAirtightHangarDoor>();
            hangarbay = new List<IMyAirtightHangarDoor>();
            cannonDoor = new List<IMyAirtightHangarDoor>();
            GridTerminalSystem.GetBlocksOfType(temp4);
            foreach (IMyAirtightHangarDoor door in temp4)
            {
                if (IsOnThisGrid(door))
                {
                    if (door.CustomName.Contains("ARBALEST_DOOR"))
                    {
                        cannonDoor.Add(door);
                    }
                    else if (door.CustomName.Contains("HANGAR_DOOR"))
                    {
                        hangarbay.Add(door);
                    }
                }
            }

            List<IMySensorBlock> temp5 = new List<IMySensorBlock>();
            sensorBlocks = new List<IMySensorBlock>();
            GridTerminalSystem.GetBlocksOfType(temp5);
            foreach (IMySensorBlock sensor in temp5)
            {
                if (IsOnThisGrid(sensor) && sensor.CustomName.Contains("DOOR_SENSOR"))
                {
                    sensorBlocks.Add(sensor);
                }
            }

            List<IMyAirVent> temp6 = new List<IMyAirVent>();
            airVents = new List<IMyAirVent>();
            GridTerminalSystem.GetBlocksOfType(temp6);
            foreach (IMyAirVent air in temp6)
            {
                if (IsOnThisGrid(air))
                {
                    airVents.Add(air);
                }
            }

            GetECBlock();
        }

        private bool IsOnThisGrid(IMyCubeBlock block)
        {
            if (Me.CubeGrid.Equals(block.CubeGrid)) return true;
            else return false;
        }

        void GetECBlock()
        {
            List<IMyProgrammableBlock> Progs = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(Progs);
            foreach (IMyProgrammableBlock block in Progs)
            {
                if (IsOnThisGrid(block) && block.CustomName.Contains("[ENERGY CONTROL]"))
                {
                    EnergyControl = block;
                    return;
                }
            }
        }

        private void NormalStatus()
        {
            //HangarDoors
            foreach (IMyAirtightHangarDoor door in cannonDoor)
            {
                door.CloseDoor();
            }
            //Text
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.BackgroundColor = Color.Black;
                panel.FontColor = Color.Green;
                panel.WriteText(NORMAL_ALERT);
            }
            //Lights
            foreach (IMyLightingBlock light in primaryLights)
            {
                light.Intensity = 10;
            }
            foreach (IMyLightingBlock light in moodLights)
            {
                light.Color = Color.White;
                light.BlinkIntervalSeconds = 0;
            }
            //Sound
            foreach (IMySoundBlock music in woofers)
            {
                music.Stop();
            }
            //Sensors
            foreach (IMySensorBlock sensor in sensorBlocks)
            {
                sensor.Enabled = true;
            }
            //HangarDoors2
            foreach (IMyAirtightHangarDoor door in cannonDoor)
            {
                while (door.Status == DoorStatus.Closing) { }
                door.Enabled = false;
            }
            //AirVents
            foreach (IMyAirVent air in airVents)
            {
                air.Depressurize = false;
            }
            //EnergyControl
            if (EnergyControl != null) EnergyControl.TryRun("NORMAL");
        }

        private void YellowStatus()
        {
            //Text
            foreach (IMyTextPanel panel in infoScreens)
            {
                //Text
                panel.BackgroundColor = Color.Black;
                panel.FontColor = Color.Yellow;
                panel.WriteText(YELLOW_ALERT);
            }
            //Lights
            foreach (IMyLightingBlock light in primaryLights)
            {
                light.Intensity = 10;
            }
            foreach (IMyLightingBlock light in moodLights)
            {
                light.Color = Color.Yellow;
                light.BlinkIntervalSeconds = 2;
                light.BlinkLength = 50;
            }
            //Sound
            foreach (IMySoundBlock music in woofers)
            {
                music.SelectedSound = "YELLOW_ALERT";
                music.Play();
            }
            //HangarDoors          
            foreach (IMyAirtightHangarDoor door in hangarbay)
            {
                door.Enabled = true;
            }
            //Sensors
            foreach (IMySensorBlock sensor in sensorBlocks)
            {
                sensor.Enabled = true;
            }
            //AirVents
            foreach (IMyAirVent air in airVents)
            {
                air.Depressurize = false;
            }
            //EnergyControl
            if (EnergyControl != null) EnergyControl.TryRun("NORMAL");
        }

        private void RedStatus()
        {
            //Text
            foreach (IMyTextPanel panel in infoScreens)
            {
                panel.BackgroundColor = Color.Red;
                panel.FontColor = Color.Black;
                panel.WriteText(">     " + RED_ALERT + "     <");
            }
            //Lights
            foreach (IMyLightingBlock light in primaryLights)
            {
                light.Intensity = 4;
            }
            foreach (IMyLightingBlock light in moodLights)
            {
                light.Color = Color.Red;
                light.BlinkIntervalSeconds = 2;
                light.BlinkLength = 50;
            }
            //Sound
            foreach (IMySoundBlock music in woofers)
            {
                music.SelectedSound = "RED_ALERT";
                music.LoopPeriod = 120;
                music.Play();
            }
            //HangarDoors
            foreach (IMyAirtightHangarDoor door in cannonDoor)
            {
                door.Enabled = true;
            }
            foreach (IMyAirtightHangarDoor door in hangarbay)
            {
                door.CloseDoor();
            }
            //Sensors
            foreach (IMySensorBlock sensor in sensorBlocks)
            {
                sensor.Enabled = false;
            }
            //AirVents
            foreach (IMyAirVent air in airVents)
            {
                air.Depressurize = true;
            }
            //EnergyControl
            if (EnergyControl != null) EnergyControl.TryRun("COMBAT");

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
                            panel.WriteText(" >    " + RED_ALERT + "    < ");
                        }
                        if (SLOW == 4) { ANIM_STATE = 1; SLOW = 0; }
                        SLOW++;
                    }
                    break;
                case 1:
                    {
                        foreach (IMyTextPanel panel in infoScreens)
                        {
                            panel.WriteText("  >   " + RED_ALERT + "   <  ");
                        }
                        if (SLOW == 4) { ANIM_STATE = 2; SLOW = 0; }
                        SLOW++;
                    }
                    break;
                case 2:
                    {
                        foreach (IMyTextPanel panel in infoScreens)
                        {
                            panel.WriteText("   >  " + RED_ALERT + "  <   ");
                        }
                        if (SLOW == 4) { ANIM_STATE = 3; SLOW = 0; }
                        SLOW++;
                    }
                    break;
                case 3:
                    {
                        foreach (IMyTextPanel panel in infoScreens)
                        {
                            panel.WriteText("    > " + RED_ALERT + " <    ");
                        }
                        if (SLOW == 4) { ANIM_STATE = 4; SLOW = 0; }
                        SLOW++;
                    }
                    break;
                case 4:
                    {
                        foreach (IMyTextPanel panel in infoScreens)
                        {
                            panel.WriteText("     >" + RED_ALERT + "<     ");
                        }
                        if (SLOW == 4) { ANIM_STATE = 5; SLOW = 0; }
                        SLOW++;
                    }
                    break;
                case 5:
                    {
                        foreach (IMyTextPanel panel in infoScreens)
                        {
                            panel.WriteText(">     " + RED_ALERT + "     <");
                        }
                        if (SLOW == 4) { ANIM_STATE = 0; SLOW = 0; }
                        SLOW++;
                    }
                    break;
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
