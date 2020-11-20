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
        string  ScreenName = "[ENERGY INFO]";
        float   recentStoredPower = -1f;
        long    iceAmmount;

        public class Logs {

            private static float value;
            public static int count = 0;

            public static float get() {
                if (count == 0) return 3600;
                return value;
            }

            public static void add() {
                count++;
            }

            public static void add(float input){
                count++;

                if((input>0 && value<0) || (input < 0 && value > 0)) {
                    count = 1;
                }

                if (count != 1) 
                    {value = value + ((input - value) / count);}
                else 
                    value = input;

                if (count > 3600) count = 360;
            }
        }

        void SayMyName(string ScriptName, float textSize = 2f) {
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);

            Me.CustomName = "[" + ScriptName + "] Script";
        }

        Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            iceAmmount = getIceAmmount();
            SayMyName("ENERGY INFO");
        }

        public long getIceAmmount() {
            List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo);

            List<IMyGasGenerator> gasG = new List<IMyGasGenerator>();
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(gasG);

            long output = 0;

            foreach (IMyCargoContainer c in cargo) {
                for (int i = 0; i < c.GetInventory().ItemCount; i++)
                    if (c.GetInventory().GetItemAt(i).Value.Type.SubtypeId.Equals("Ice"))
                        output += c.GetInventory().GetItemAt(i).Value.Amount.RawValue;
            }

            foreach (IMyGasGenerator gg in gasG) {
                for (int i = 0; i < gg.GetInventory().ItemCount; i++)
                    if (gg.GetInventory().GetItemAt(i).Value.Type.SubtypeId.Equals("Ice"))
                        output += gg.GetInventory().GetItemAt(i).Value.Amount.RawValue;
            }



            return output
                //     /1000000 //to get 'normal' numbers
                ;
        }

        public double getMedH2Capacity() {
            List<IMyGasTank> temp = new List<IMyGasTank>();
            List<IMyGasTank> tank = new List<IMyGasTank>();
            int counter = 0;
            double output = 0;
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(temp);
            foreach (IMyGasTank t in temp) {
                if (t.Capacity > 100000f && isOnThisGrid(t.CubeGrid)) {
                    tank.Add(t);
                    counter++;
                }
            }
            if (counter == 0) return 0D;
            else {
                Double tempo = Convert.ToDouble(counter);
                foreach (IMyGasTank t in tank) {
                    output += t.FilledRatio;
                }
                return (output / tempo);
            }
        }

        public bool isOnThisGrid(IMyCubeGrid G) {
            if (G == Me.CubeGrid) return true;
            else return false;
        }

        public String toPercent(float up, float down) {
            float input = (100 * up) / down;
            String output = input.ToString("0.00");
            return output;
        }

        public String printEnergyInfo() {
            float ShipsStoredPower = 0;
            float ShipsMaxPower = 0;
            float MaxShipOutput = 0;
            float CurrentBatteryOutput = 0;
            float CurrentShipOutput = 0;
            int Online = 0;
            int Recharging = 0;
            int Empty = 0;
            int Offline = 0;
            int RNominal = 0;
            int ROff = 0;

            List<IMyPowerProducer> Producers = new List<IMyPowerProducer>();
            GridTerminalSystem.GetBlocksOfType(Producers);

            foreach (IMyPowerProducer P in Producers) {
                if (isOnThisGrid(P.CubeGrid)) {
                    if (P.IsWorking) MaxShipOutput += P.MaxOutput;
                    CurrentShipOutput += P.CurrentOutput;

                    if (P is IMySolarPanel) { }
                    else if (!(P is IMyBatteryBlock)) {
                        if (P.IsWorking) RNominal++;
                        else ROff++;
                    }
                    else {
                        IMyBatteryBlock B = (IMyBatteryBlock)P;
                        ShipsStoredPower += B.CurrentStoredPower;
                        ShipsMaxPower += B.MaxStoredPower;
                        CurrentBatteryOutput += B.CurrentOutput;
                        CurrentShipOutput -= B.CurrentInput;
                        CurrentBatteryOutput -= B.CurrentInput;

                        if (B.CurrentStoredPower == 0) Empty++;
                        else if (!(B.IsWorking)) Offline++;
                        else if (B.ChargeMode == ChargeMode.Recharge) Recharging++;
                        else Online++;
                    }
                }
            }

            if (recentStoredPower == -1) 
                recentStoredPower = ShipsStoredPower;


            float convert = 0.001F,
                  difference = recentStoredPower - ShipsStoredPower,
                  timeMulti;

            if((Runtime.UpdateFrequency & UpdateFrequency.Update1) > 0) 
                {timeMulti = 60;}
            else if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) > 0) 
                {timeMulti = 6;}
            else 
                {timeMulti = 6/10;}

            difference *= timeMulti;

            CurrentShipOutput = convert * CurrentShipOutput;
            MaxShipOutput = convert * MaxShipOutput;

            string output = " Current Power: " + ShipsStoredPower.ToString("0.0") + "/" + ShipsMaxPower.ToString("0.0") + " MWh ("
            + toPercent(ShipsStoredPower, ShipsMaxPower) + "%)";
            output += "\n H2 Reserves:   " + (getMedH2Capacity() * 100).ToString("0.00") + "%";

            output += "\n Current Output: " + CurrentShipOutput.ToString("0.00") + "/" + MaxShipOutput.ToString("0.0") +
            " GW (" + toPercent(CurrentShipOutput, MaxShipOutput) + "%)";

            float remainingTime;
            if (difference != 0) {
                remainingTime = ShipsStoredPower / difference;
                Logs.add(remainingTime);
            }
            else Logs.add();

            remainingTime = Logs.get();

            if (remainingTime < 0) {
                    output += "\n Recharged in     ";
                    remainingTime *= -1;
                    if (remainingTime > 3600) {
                        output += (remainingTime / 3600).ToString("0.") + " h ";
                        remainingTime = remainingTime % 3600;
                        output += (remainingTime / 60).ToString("0.") + " m ";
                        remainingTime = remainingTime % 60;
                        output += remainingTime.ToString("0.") + " s";
                    }
                    else if (remainingTime > 60) {
                        output += (remainingTime / 60).ToString("0.") + " m ";
                        remainingTime = remainingTime % 60;
                        output += remainingTime.ToString("0.") + " s";
                    }
                    else {
                        output += remainingTime.ToString("0.") + " s";
                    }
            }
            else {
                    output += "\n Will last for       ";
                    if (remainingTime > 3600) {
                        output += (remainingTime / 3600).ToString("0.") + " h ";
                        remainingTime = remainingTime % 3600;
                        output += (remainingTime / 60).ToString("0.") + " m ";
                        remainingTime = remainingTime % 60;
                        output += remainingTime.ToString("0.") + " s";
                    }
                    else if (remainingTime > 60) {
                        output += (remainingTime / 60).ToString("0.") + " m ";
                        remainingTime = remainingTime % 60;
                        output += remainingTime.ToString("0.") + " s";
                    }
                    else {
                        output += remainingTime.ToString("0.") + " s";
                    }
            }

            if (RNominal > 0 || ROff > 0) {
                double percent = getMedH2Capacity();
                output += "\n Cores Online:    " + RNominal + "/" + (RNominal + ROff);
            }
            else output += "\n No power cores present!";

            output += "\n Batteries:          " + Online + "/" + (Online + Empty + Recharging + Offline) + " Online";
            if (Recharging > 0) output += "\n                           " + Recharging + " Recharging";
            if (Empty > 0) output += "\n                           " + Empty + " Empty";


            long currAmm = getIceAmmount();

            if (iceAmmount - currAmm > 0) {
                float remTime = currAmm * 100 / (iceAmmount - currAmm);
                iceAmmount = currAmm;
                output += "\n Ice will last for    ";
                if (remTime > 3600) {
                    output += (remTime / 3600).ToString("0.") + " h ";
                    remTime = remTime % 3600;
                    output += (remTime / 60).ToString("0.") + " m ";
                    remTime = remTime % 60;
                    output += remTime.ToString("0.") + " s";
                }
                else if (remTime > 60) {
                    output += (remTime / 60).ToString("0.") + " m ";
                    remTime = remTime % 60;
                    output += remTime.ToString("0.") + " s";
                }
                else {
                    output += remTime.ToString("0.") + " s";
                }
            }
            else output += "\n Ice stable";


            recentStoredPower = ShipsStoredPower;

            return output;
        }

        public List<IMyTextPanel> getEnergyScreen() {
            List<IMyTextPanel> output = new List<IMyTextPanel>();
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyTerminalBlock b in temp) {
                if (b.CustomName.Contains(ScreenName) && isOnThisGrid(b.CubeGrid)) {
                    IMyTextPanel tempo = b as IMyTextPanel;
                    output.Add(tempo);
                }
            }
            temp.Clear();

            return output;
        }

        public void Output(String output) {
            foreach (IMyTextPanel EnergyScreen in getEnergyScreen()) {
                EnergyScreen.FontSize = (float)1.8;
                EnergyScreen.WriteText(output, false);
                EnergyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            Output(printEnergyInfo());
        }
    }
}
