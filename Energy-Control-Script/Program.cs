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

        void SayMyName(string ScriptName, float textSize = 2f) {
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            GetNeededBlocks();
            SayMyName("ENERGY CONTROL");
        }

        List<IMyPowerProducer>  Power;
        List<IMyBatteryBlock>   Batteries;
        List<IMyReactor>        Reactors;
        List<IMyProductionBlock>Producers;
        List<IMyGasTank>        Tanks;
        List<IMyFunctionalBlock>Everything;    // Now this.... This is an Absolute Madness

        void GetNeededBlocks() {
            List<IMyFunctionalBlock> temp = new List<IMyFunctionalBlock>();
            GridTerminalSystem.GetBlocksOfType(temp);

            Power       = new List<IMyPowerProducer>();
            Batteries   = new List<IMyBatteryBlock>();
            Reactors    = new List<IMyReactor>();
            Producers   = new List<IMyProductionBlock>();
            Tanks       = new List<IMyGasTank>();
            Everything  = new List<IMyFunctionalBlock>();

            foreach (IMyFunctionalBlock block in temp) {
                if (isOnThisGrid(block)) {
                    Everything.Add(block);
                    if(block is IMyPowerProducer) {
                        IMyPowerProducer power = (IMyPowerProducer)block;
                        Power.Add(power);
                        if(block is IMyBatteryBlock) {
                            IMyBatteryBlock battery = (IMyBatteryBlock)block;
                            Batteries.Add(battery);
                        }
                        else
                        if(block is IMyReactor) {
                            IMyReactor reactor = (IMyReactor)block;
                            Reactors.Add(reactor);
                        }
                    }
                    else
                    if(block is IMyProductionBlock) {
                        IMyProductionBlock producer = (IMyProductionBlock)block;
                        Producers.Add(producer);
                    }
                    else
                    if(block is IMyGasTank) {
                        IMyGasTank tank = (IMyGasTank)block;
                        if (tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) Tanks.Add(tank);
                    }
                }
            }
        }

        public int lastDropCounter = 0;
        public int lastUpCounter = 0;

        public string LastState = "NULL";

        public bool isOnThisGrid(IMyCubeBlock G) {
            if (G.CubeGrid.Equals(Me.CubeGrid)) return true;
            else return false;
        }

        public float getOutputPercent() {
            float CurrentShipOutput = 0, MaxShipOutput = 0;
            foreach (IMyPowerProducer P in Power) {
                if (P.IsWorking) MaxShipOutput += P.MaxOutput;
                CurrentShipOutput += P.CurrentOutput;
            }

            foreach (IMyBatteryBlock B in Batteries) {
                CurrentShipOutput -= B.CurrentInput;
            }

            if (MaxShipOutput > 0) return 100 * CurrentShipOutput / MaxShipOutput;
            else return 0;
        }

        public float getPowerPercent() {
            float max = 0, curr = 0;

            foreach (IMyBatteryBlock B in Batteries) {
                curr += B.CurrentStoredPower;
                max += B.MaxStoredPower;
            }
            if (max > 0) return 100 * curr / max;
            else return 0;
        }

        public void setProduction(bool set) {
            foreach (IMyProductionBlock P in Producers) {
                P.Enabled = set;
            }
        }

        public bool getProduction() {
            foreach (IMyProductionBlock P in Producers) {
                if (P.Enabled == false) return false;
            }
            return true;
        }

        public void setH2(bool set) {
            foreach (IMyPowerProducer P in Power) {
                if (!(P is IMyReactor) && !(P is IMyBatteryBlock) && !(P is IMySolarPanel)) P.Enabled = set;
            }
        }

        public bool getH2() {
            foreach (IMyPowerProducer P in Power) {
                if (!(P is IMyReactor) && !(P is IMyBatteryBlock) && !(P is IMySolarPanel)) if (P.Enabled == false) return false;
            }
            return true;
        }

        public double getMedH2Capacity() {
            if (Tanks.Count == 0) return 0d;
            double output = 0;
            double tempo = Convert.ToDouble(Tanks.Count);
            foreach (IMyGasTank t in Tanks) {
                output += t.FilledRatio;
            }
            return (output / tempo);
        }

        public void setReactors(bool set) {
            foreach (IMyReactor P in Reactors) {
                P.Enabled = set;
            }
        }

        public bool getReactors() {
            foreach (IMyReactor P in Reactors) {
                if (P.Enabled == false) return false;
            }
            return true;
        }

        public void setBatteries(bool set) {
            foreach (IMyBatteryBlock B in Batteries) {
                if (!set) {
                    B.ChargeMode = ChargeMode.Recharge;
                }
                else {
                    B.ChargeMode = ChargeMode.Auto;
                }
            }
        }

        public bool getBatteries() {
            foreach (IMyBatteryBlock B in Batteries) {
                if (B.ChargeMode == ChargeMode.Recharge || B.Enabled == false) return false;
            }
            return true;
        }

        public void enableBatteries(bool set) {
            foreach (IMyBatteryBlock B in Batteries) {
                B.Enabled = set;
            }
        }

        public bool checkIfEmergency() {
            if (getPowerPercent() < 5f && getMedH2Capacity() < 10f) return true;
            else return false;
        }

        public void Emergency() {
            LastState = "EMERGENCY";
            setReactors(true);
            foreach (IMyFunctionalBlock A in Everything) {
                if (A is IMyBatteryBlock) {
                    IMyBatteryBlock batt = (IMyBatteryBlock)A;
                    batt.ChargeMode = ChargeMode.Auto;
                }
                else
                if (!(A is IMyPowerProducer) && !(A is IMyShipConnector) && !(A is IMyShipMergeBlock)
                &&  !(A is IMyTextPanel) && !(A is IMyUpgradeModule) && !(A is IMyTextSurface)
                &&  !(A is IMyProgrammableBlock) && !(A is IMyTimerBlock) && !(A is IMyDoor) && !(A is IMyThrust) && !(A is IMyGyro)) {
                    A.Enabled = false;
                }
            }
        }

        public void ClearEmergency() {
            foreach (IMyFunctionalBlock A in Everything) {
                if (!(A is IMyReactor) && !(A is IMyShipConnector) && !(A is IMyShipMergeBlock)
                && !(A is IMyTextPanel) && !(A is IMyUpgradeModule) && !(A is IMyTextSurface)
                && !(A is IMyProgrammableBlock) && !(A is IMyTimerBlock) && !(A is IMyDoor) && !(A is IMyThrust) && !(A is IMyGyro))  {
                    A.Enabled = true;
                }
            }
        }

        public void Normal() {
            setProduction(true);
            setH2(true);
            setBatteries(true);
            setReactors(false);
        }

        public void Combat() {
            setProduction(false);
            setH2(true);
            setBatteries(true);
        }

        public void Auto() {
            lastUpCounter++;
            lastDropCounter++;
            if ((getOutputPercent() > 90f || getPowerPercent() < 10f) && lastUpCounter > 20) {
                lastUpCounter = 0;
                if (getBatteries() == false) { // Emergency Level 0
                    setBatteries(true);
                    enableBatteries(true);
                    Output("\nBatteries turned on", true);
                }
                else
                if (getH2() == false) { // Emergency Level 1
                    setH2(true);
                    Output("\nPower cores turned on", true);
                }
                else
                if (getProduction() == true) { // Emergency Level 2
                    setProduction(false);
                    Output("\nProduction blocks\nturned off", true);
                }
                else
                if (getReactors() == false) { // Emergency Level 3
                    setReactors(true);
                    Output("\nReactors\nturned on", true);
                }
                else // Emergency Level 4
                if (checkIfEmergency()) Emergency();
            }
            else
            if (getOutputPercent() < 10f && getPowerPercent() > 10f && lastDropCounter > 100) {
                lastDropCounter = 0;
                if (getReactors() == true) {
                    setReactors(false);
                    Output("\nReactors turned off", true);
                }
                else
                if (getProduction() == false) {
                    setProduction(true);
                    Output("\nProduction turned\nback online", true);
                }
                else
                if (getH2() == true && getBatteries() == true) {
                    setH2(false);
                    Output("\nPower Cores\nare offline", true);
                }
                else {
                    if (getOutputPercent() < (float)3 && getMedH2Capacity() > 0.85D) {
                        setH2(true);
                        setBatteries(false);
                        Output("\nRecharging Batteries", true);
                    }
                }
            }
        }

        public void Output(String output, bool append = false) {
            foreach (IMyTextPanel EnergyScreen in GetEnergyScreens()) {
                EnergyScreen.FontSize = 2.0f;
                EnergyScreen.Alignment = TextAlignment.CENTER;
                EnergyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
                EnergyScreen.WriteText(output, append);
            }
        }

        public List<IMyTextPanel> GetEnergyScreens() {
            List<IMyTextPanel> output = new List<IMyTextPanel>();
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyTextPanel b in temp) {
                if (isOnThisGrid(b) && b.CustomName.Contains("[ENERGY CONTROL]")) {
                    output.Add(b);
                }
            }
            return output;
        }

        public void Main(string argument, UpdateType updateSource) {
            Echo(argument + " " + LastState);
            if (LastState == "NULL") LastState = "NORMAL";
            switch (argument.ToUpper()) {
                case "AUTO":
                    LastState = "AUTO";
                    Output("\nAUTO MODE");
                    Auto();
                    break;

                case "NORMAL":
                    LastState = "NORMAL";
                    Output("\nNORMAL MODE");
                    Normal();
                    break;

                case "COMBAT":
                    LastState = "COMBAT";
                    Output("\nCOMBAT MODE");
                    Combat();
                    break;

                case "EMON":
                    Emergency();
                    break;

                case "EMOFF":
                    ClearEmergency();
                    break;

                default:
                    if (LastState == "AUTO") {
                        Output("\nAUTO MODE");
                        Auto();
                    }
                    //else if(LastState == "EMERGENCY" && !checkIfEmergency()) {ClearEmergency(); LastState = "AUTO"; Auto();}
                    break;
            }
        }
    }
}
