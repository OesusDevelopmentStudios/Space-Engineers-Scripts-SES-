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
        int TimeIncrementer = 0, IdleCyclesBase = 2;

        Logs EnergyLogs, IceLogs;

        ControllerState CurrentState = ControllerState.NORMAL;

        List<IMyCargoContainer> cargoContainters = new List<IMyCargoContainer>();
        List<IMyGasGenerator> gasGenerators = new List<IMyGasGenerator>();
        List<IMyGasTank> hydrogenTanks = new List<IMyGasTank>();
        List<IMyPowerProducer> powerProducers = new List<IMyPowerProducer>();
        List<IMyTextPanel> textPanels = new List<IMyTextPanel>();

        enum ControllerState{
            AUTO,
            NORMAL,
            COMBAT,
            EMERGENCY
        }

        void SwitchControllersState(ControllerState state){
            CurrentState = state;
            switch(state){
                case ControllerState.AUTO:

                    break;

                case ControllerState.NORMAL:

                    break;

                case ControllerState.COMBAT:

                    break;

                case ControllerState.EMERGENCY:

                    break;

                default:

                    break;
            }
        }

        void FindCargoContainers() {
            if (!ONLY_USE_BLOCKS_FROM_THIS_GRID) {
                GridTerminalSystem.GetBlocksOfType(cargoContainters = new List<IMyCargoContainer>());
                return;
            }
            List<IMyCargoContainer> temp = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(temp); cargoContainters = new List<IMyCargoContainer>();

            foreach (IMyCargoContainer cont in temp) if (IsOnThisGrid(cont)) cargoContainters.Add(cont);
        }

        void FindGasGenerators() {
            if (!ONLY_USE_BLOCKS_FROM_THIS_GRID) {
                GridTerminalSystem.GetBlocksOfType(gasGenerators = new List<IMyGasGenerator>());
                return;
            }
            List<IMyGasGenerator> temp = new List<IMyGasGenerator>();
            GridTerminalSystem.GetBlocksOfType(temp); gasGenerators = new List<IMyGasGenerator>();

            foreach (IMyGasGenerator gasG in temp) if (IsOnThisGrid(gasG)) gasGenerators.Add(gasG);
        }

        void FindHydrogenTanks() {
            List<IMyGasTank> temp = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(temp); hydrogenTanks = new List<IMyGasTank>();

            foreach (IMyGasTank tank in temp)
                if ((!ONLY_USE_BLOCKS_FROM_THIS_GRID || IsOnThisGrid(tank)) && tank.BlockDefinition.SubtypeName.Contains("HydrogenTank"))
                    hydrogenTanks.Add(tank);
        }

        void FindPowerProducers() {
            if (!ONLY_USE_BLOCKS_FROM_THIS_GRID) {
                GridTerminalSystem.GetBlocksOfType(powerProducers = new List<IMyPowerProducer>());
                return;
            }
            List<IMyPowerProducer> temp = new List<IMyPowerProducer>();
            GridTerminalSystem.GetBlocksOfType(temp); powerProducers = new List<IMyPowerProducer>();

            foreach (IMyPowerProducer PP in temp) if (IsOnThisGrid(PP)) powerProducers.Add(PP);
        }

        void FindTextPanels() {
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp); textPanels = new List<IMyTextPanel>();

            foreach (IMyTextPanel TP in temp) if ((!ONLY_USE_BLOCKS_FROM_THIS_GRID || IsOnThisGrid(TP)) && TP.CustomName.Contains(ScreenName)) textPanels.Add(TP);
        }

        void FindNeededBlocks(UpdateType updateSource) {
            int
                timeLimiter = (updateSource & UpdateType.Update1) > 0 ? (1+IdleCyclesBase)*100 : ((updateSource & UpdateType.Update10) > 0 ? (1+IdleCyclesBase)*10 : (1+IdleCyclesBase)),
                effectiveTimeIncrementer = TimeIncrementer / timeLimiter,
                TimeIncrementerMod = TimeIncrementer % timeLimiter;
            TimeIncrementer++;

            if (effectiveTimeIncrementer > 4) {
                TimeIncrementer = 0; return;
            }
            if (TimeIncrementerMod != 0) return;

            switch (effectiveTimeIncrementer) {
                case 0: FindCargoContainers(); break;
                case 1: FindPowerProducers(); break;
                case 2: FindGasGenerators(); break;
                case 3: FindHydrogenTanks(); break;
                case 4: FindTextPanels(); break;
                default: TimeIncrementer = 0; return;
            }
        }

        public class Logs {
            private float value = 0;
            public int count = 0;
            public float GetValue() { return value; }
            public void Add() { count++; }
            public void Add(float input) {
                Add();

                if ((input > 0 && value < 0) || (input < 0 && value > 0)) count = 1;

                if (count != 1) { value += ((input - value) / count); }
                else            { value = input; }

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

        public void Save(){
            Storage = CurrentState.ToString();
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            EnergyLogs  = new Logs();
            IceLogs     = new Logs();
            FindCargoContainers();
            FindGasGenerators();
            FindHydrogenTanks();
            FindPowerProducers();
            FindTextPanels();
            IceAmount = GetIceAmount();
            SayMyName("ENERGY INFO & CONTROL");
            if(!Enum.TryParse<ControllerState>(Storage, out CurrentState)))
                CurrentState = ControllerState.NORMAL;
        }

        void EnableItemsInList<T>(bool enable, List<T> list) where T:IMyFunctionalBlock {
            foreach(T item in list) item.Enabled = enable;
        }

        bool ItemsInListAreEnabled<T>(List<T> list) where T:IMyFunctionalBlock {
            foreach(T item in list) if(!item.Enabled) return false;
            return true;
        }

        String SecondsToTimeInString(float seconds) {
            if (seconds < 0) seconds = -seconds;
            return String.Format("{0,2:00}:{1,2:00}:{2,4:00.0}", (int)(seconds / 3600), (int)((seconds % 3600) / 60), seconds % 60);
        }

        long GetIceAmount() {
            long output = 0;
            IMyInventory inventory;
            MyInventoryItem? item;
            foreach (IMyCargoContainer c in cargoContainters) {
                inventory = c.GetInventory();
                for (int i = 0; i < inventory.ItemCount; i++){
                    item = inventory.GetItemAt(i);
                    if (item.Value.Type.SubtypeId.Equals("Ice"))
                        output += item.Value.Amount.RawValue;
                }
            }

            foreach (IMyGasGenerator c in gasGenerators) {
                inventory = c.GetInventory();
                for (int i = 0; i < inventory.ItemCount; i++){
                    item = inventory.GetItemAt(i);
                    if (item.Value.Type.SubtypeId.Equals("Ice"))
                        output += item.Value.Amount.RawValue;
                }
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
                    if (P.IsWorking) 
                            RNominal++;
                    else    ROff++;
                }
                else {
                    IMyBatteryBlock B = (IMyBatteryBlock)P;
                    ShipsStoredPower        += B.CurrentStoredPower;
                    ShipsMaxPower           += B.MaxStoredPower;
                    CurrentBatteryOutput    += B.CurrentOutput;
                    CurrentShipOutput       -= B.CurrentInput;
                    CurrentBatteryOutput    -= B.CurrentInput;

                    if (B.CurrentStoredPower == 0) Empty++;
                    else if (!(B.IsWorking)) Offline++;
                    else if (B.ChargeMode == ChargeMode.Recharge) Recharging++;
                    else Online++;
                }
            }

            if (RecentStoredPower == -1)
                RecentStoredPower = ShipsStoredPower;

            float 
                convert = 1F,
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
                    +"{6,-14}: {7,9:0.0}/{8,-4:0} MW  ({9,5:0.0}%)\n"
                    +"{4,-14}: {10,19}({5,5:0.0}%)",
                     "Current Power", ShipsStoredPower, ShipsMaxPower, (ShipsStoredPower * 100 / ShipsMaxPower),
                     "H2 Reserves", (GetMeanHydrogenFillage() * 100),
                     "Current Output", CurrentShipOutput, MaxShipOutput, (CurrentShipOutput * 100 / MaxShipOutput), "");

            if (difference != 0)
                    EnergyLogs.Add(ShipsStoredPower / difference); 
            else    EnergyLogs.Add();

            if (RNominal > 0 || ROff > 0)
                    output += String.Format("\n{0,-14}: {1,9}/{2,-6}", "Cores Online", RNominal, RNominal + ROff);
            else    output += "\n No power cores present!";

            output += String.Format("\n{0,-14}: {1,9}/{2,-6} {3}", "Batteries", Online, Online + Empty + Recharging + Offline, "Online");
            if (Recharging > 0) output += String.Format("\n{0,32} {1}", Recharging, "Recharging"); else output+="\n";
            if (Empty > 0) output += String.Format("\n{0,32} {1}", Empty, "Empty"); else output+="\n";

            float remainingTime = EnergyLogs.GetValue();
            string firstPart = remainingTime < 0 ? "Full power in" : "Enough power for";
            if (remainingTime != 0) 
                    output += String.Format("\n{0,-14}: {2,9}{1}", firstPart, SecondsToTimeInString(remainingTime), "");
            else    output += String.Format("\n{0,-14}: {2,3}{1}", "Power reserves", "stable", "");

            long currAmm = GetIceAmount();
            difference = (IceAmount - currAmm) * timeMulti;
            if(difference!=0)
                    IceLogs.Add(currAmm / difference);
            else    IceLogs.Add();

            remainingTime = IceLogs.GetValue();
            firstPart = remainingTime < 0 ? "Ice reserves":"Ice reserves depleted in";
            if (remainingTime > 0)  
                    output += String.Format("\n{0,-14}: {2,9}{1}", firstPart, SecondsToTimeInString(remainingTime), "");
            else    output += String.Format("\n{0,-14}: {2,3}{1}", firstPart, remainingTime == 0? "stable":"rising", "");
            
            RecentStoredPower = ShipsStoredPower;
            IceAmount = currAmm;

            return output;
        }

        void Output(String output) {
            foreach (IMyTextPanel EnergyScreen in textPanels) {
                EnergyScreen.FontSize = 1.1f;
                EnergyScreen.Font = "Monospace";
                EnergyScreen.WriteText(output, false);
                EnergyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            }
        }

        void EvaluateInputArgument(string argument){
            string[] command = argument.ToUpper().Split(' ');
            if(command.Length>0){
                switch(command[0]){
                    default:
                        foreach(ControllerState state in ControllerState.)
                        break;
                }
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if(updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100) > 0){
                Output(PrintEnergyInfo());
                FindNeededBlocks(updateSource);
            }
            else{
                EvaluateInputArgument(argument);
            }
        }
    }
}
