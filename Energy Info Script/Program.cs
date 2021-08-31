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
        const bool ONLY_USE_BLOCKS_FROM_THIS_GRID = true;
        readonly string ScreenName = "[ENERGY INFO]";
        float RecentStoredPower = -1f;
        long IceAmount;
        int TimeIncrementer = 0;

        List<IMyCargoContainer> cargoContainters = new List<IMyCargoContainer>();
        List<IMyGasGenerator> gasGenerators = new List<IMyGasGenerator>();
        List<IMyGasTank> hydrogenTanks = new List<IMyGasTank>();
        List<IMyPowerProducer> powerProducers = new List<IMyPowerProducer>();
        List<IMyTextPanel> textPanels = new List<IMyTextPanel>();

        void FindCargoContainers(bool restrictive) {
            if (!restrictive) {
                GridTerminalSystem.GetBlocksOfType(cargoContainters = new List<IMyCargoContainer>());
                return;
            }
            List<IMyCargoContainer> temp = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(temp); cargoContainters = new List<IMyCargoContainer>();

            foreach (IMyCargoContainer cont in temp) if (IsOnThisGrid(cont)) cargoContainters.Add(cont);
        }

        void FindGasGenerators(bool restrictive) {
            if (!restrictive) {
                GridTerminalSystem.GetBlocksOfType(gasGenerators = new List<IMyGasGenerator>());
                return;
            }
            List<IMyGasGenerator> temp = new List<IMyGasGenerator>();
            GridTerminalSystem.GetBlocksOfType(temp); gasGenerators = new List<IMyGasGenerator>();

            foreach (IMyGasGenerator gasG in temp) if (IsOnThisGrid(gasG)) gasGenerators.Add(gasG);
        }

        void FindHydrogenTanks(bool restrictive) {
            List<IMyGasTank> temp = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(temp); hydrogenTanks = new List<IMyGasTank>();

            foreach (IMyGasTank tank in temp)
                if ((!restrictive || IsOnThisGrid(tank)) && tank.BlockDefinition.SubtypeName.Contains("HydrogenTank"))
                    hydrogenTanks.Add(tank);
        }

        void FindPowerProducers(bool restrictive) {
            if (!restrictive) {
                GridTerminalSystem.GetBlocksOfType(powerProducers = new List<IMyPowerProducer>());
                return;
            }
            List<IMyPowerProducer> temp = new List<IMyPowerProducer>();
            GridTerminalSystem.GetBlocksOfType(temp); powerProducers = new List<IMyPowerProducer>();

            foreach (IMyPowerProducer PP in temp) if (IsOnThisGrid(PP)) powerProducers.Add(PP);
        }

        void FindTextPanels(bool restrictive) {
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp); textPanels = new List<IMyTextPanel>();

            foreach (IMyTextPanel TP in temp) if ((!restrictive || IsOnThisGrid(TP)) && TP.CustomName.Contains(ScreenName)) textPanels.Add(TP);
        }

        void FindNeededBlocks(UpdateType updateSource) {
            int
                timeLimiter = (updateSource & UpdateType.Update1) > 0 ? 300 : ((updateSource & UpdateType.Update10) > 0 ? 30 : 3),
                effectiveTimeIncrementer = TimeIncrementer / timeLimiter,
                TimeIncrementerMod = TimeIncrementer % timeLimiter;
            TimeIncrementer++;

            if (effectiveTimeIncrementer > 4) {
                TimeIncrementer = 0; return;
            }
            if (TimeIncrementerMod != 0) return;

            switch (effectiveTimeIncrementer) {
                case 0: FindCargoContainers(ONLY_USE_BLOCKS_FROM_THIS_GRID); break;
                case 1: FindPowerProducers(ONLY_USE_BLOCKS_FROM_THIS_GRID); break;
                case 2: FindGasGenerators(ONLY_USE_BLOCKS_FROM_THIS_GRID); break;
                case 3: FindHydrogenTanks(ONLY_USE_BLOCKS_FROM_THIS_GRID); break;
                case 4: FindTextPanels(ONLY_USE_BLOCKS_FROM_THIS_GRID); break;
                default: TimeIncrementer = 0; return;
            }
        }

        public class Logs {
            private static float value = 0;
            public static int count = 0;
            public static float GetRemainingSecondsOfEnergy() { return value; }
            public static void Add() { count++; }
            public static void Add(float input) {
                Add();

                if ((input > 0 && value < 0) || (input < 0 && value > 0)) {
                    count = 1;
                }

                if (count != 1) { value += ((input - value) / count); }
                else
                    value = input;

                if (count > 100) count = 100;
            }
        }

        void SayMyName(string ScriptName, float textSize = 2f) {
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText("\n\n" + ScriptName);

            Me.CustomName = "[" + ScriptName + "] Script";
        }

        Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            FindCargoContainers(ONLY_USE_BLOCKS_FROM_THIS_GRID);
            FindGasGenerators(ONLY_USE_BLOCKS_FROM_THIS_GRID);
            FindHydrogenTanks(ONLY_USE_BLOCKS_FROM_THIS_GRID);
            FindPowerProducers(ONLY_USE_BLOCKS_FROM_THIS_GRID);
            FindTextPanels(ONLY_USE_BLOCKS_FROM_THIS_GRID);
            IceAmount = GetIceAmount();
            SayMyName("ENERGY INFO");
        }

        String SecondsToTimeInString(float seconds) {
            if (seconds < 0) seconds = -seconds;
            return String.Format("{0,2:00}:{1,2:00}:{2,4:00.0}", (int)(seconds / 3600), (int)((seconds % 3600) / 60), seconds % 60);
        }

        long GetIceAmount() {
            long output = 0;

            foreach (IMyCargoContainer c in cargoContainters) {
                for (int i = 0; i < c.GetInventory().ItemCount; i++)
                    if (c.GetInventory().GetItemAt(i).Value.Type.SubtypeId.Equals("Ice"))
                        output += c.GetInventory().GetItemAt(i).Value.Amount.RawValue;
            }

            foreach (IMyGasGenerator gg in gasGenerators) {
                for (int i = 0; i < gg.GetInventory().ItemCount; i++)
                    if (gg.GetInventory().GetItemAt(i).Value.Type.SubtypeId.Equals("Ice"))
                        output += gg.GetInventory().GetItemAt(i).Value.Amount.RawValue;
            }

            return output
                //     /1000000 //to get 'normal' numbers
                ;
        }

        double GetMeanHydrogenFillage() {
            double output = 0;
            if (hydrogenTanks.Count == 0) return output;
            else {
                Double tempo = Convert.ToDouble(hydrogenTanks.Count);
                foreach (IMyGasTank tank in hydrogenTanks) output += tank.FilledRatio;
                return (output / tempo);
            }
        }

        bool IsOnThisGrid(IMyCubeBlock block) { return (block != null && block.CubeGrid.Equals(Me.CubeGrid)); }

        String PrintEnergyInfo() {
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

            foreach (IMyPowerProducer P in powerProducers) {
                if (P.IsWorking) MaxShipOutput += P.MaxOutput;
                CurrentShipOutput += P.CurrentOutput;

                if (!(P is IMyBatteryBlock)) {
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

            if (RecentStoredPower == -1)
                RecentStoredPower = ShipsStoredPower;

            float convert = 1F,
                  difference = RecentStoredPower - ShipsStoredPower,
                  timeMulti;

            if ((Runtime.UpdateFrequency & UpdateFrequency.Update1) > 0) { timeMulti = 60; }
            else if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) > 0) { timeMulti = 6; }
            else { timeMulti = 6 / 10; }

            difference *= timeMulti;

            CurrentShipOutput = convert * CurrentShipOutput;
            MaxShipOutput = convert * MaxShipOutput;

            string
                output =
                String.Format("\n" +
                     "{0,-14}: {1,9:0.0}/{2,-4:0} MWh ({3,5:0.0}%)\n"
                    + "{6,-14}: {7,9:0.0}/{8,-4:0} MW  ({9,5:0.0}%)\n"
                    + "{4,-14}: {10,19}({5,5:0.0}%)",
                     "Current Power", ShipsStoredPower, ShipsMaxPower, (ShipsStoredPower * 100 / ShipsMaxPower),
                     "H2 Reserves", (GetMeanHydrogenFillage() * 100),
                     "Current Output", CurrentShipOutput, MaxShipOutput, (CurrentShipOutput * 100 / MaxShipOutput), "");
            /*/
            output += "\n              Solar: " + CurrentSolarOutput.ToString("0.00") +" MW";
            /**/
            float remainingTime;
            if (difference != 0) {
                remainingTime = ShipsStoredPower / difference;
                Logs.Add(remainingTime);
            }
            else Logs.Add();

            if (RNominal > 0 || ROff > 0)
                output += String.Format("\n{0,-14}: {1,9}/{2,-6}", "Cores Online", RNominal, RNominal + ROff);
            else
                output += "\n No power cores present!";

            output += String.Format("\n{0,-14}: {1,9}/{2,-6} {3}", "Batteries", Online, Online + Empty + Recharging + Offline, "Online");
            if (Recharging > 0) output += String.Format("\n{0,32} {1}", Recharging, "Recharging");
            if (Empty > 0) output += String.Format("\n{0,32} {1}", Empty, "Empty");


            long currAmm = GetIceAmount();

            if (IceAmount - currAmm > 0) {
                float remTime = currAmm * 100 / (IceAmount - currAmm);
                IceAmount = currAmm;
                output += String.Format("\n\n{0,-14}: {2,3}{1}", "Ice will last for", SecondsToTimeInString(remTime),"");
            }
            else output += String.Format("\n\n{0,-14}: {2,3}{1}", "Ice reserves", "stable", "");

            remainingTime = Logs.GetRemainingSecondsOfEnergy();
            string firstPart = remainingTime < 0 ? "Full power in" : "Enough power for";
            if (remainingTime != 0) output += String.Format("\n{0,-14}: {2,9}{1}", firstPart, SecondsToTimeInString(remainingTime), "");
            else output += String.Format("\n{0,-14}: {2,3}{1}", "Power reserves", "stable", "");

            RecentStoredPower = ShipsStoredPower;

            return output;
        }

        public void Output(String output) {
            foreach (IMyTextPanel EnergyScreen in textPanels) {
                EnergyScreen.FontSize = 1.1f;
                EnergyScreen.Font = "Monospace";
                EnergyScreen.WriteText(output, false);
                EnergyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            Output(PrintEnergyInfo());
            FindNeededBlocks(updateSource);
        }
    }
}
