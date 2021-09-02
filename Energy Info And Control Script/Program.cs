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
        readonly bool ONLY_USE_BLOCKS_FROM_THIS_GRID = true;
        readonly string ScreenName = "[ENERGY SCRIPT]";
        readonly int IdleCyclesBase = 2;
        float RecentStoredPower = -1f;
        long IceAmount;
        int TimeIncrementer = 0,
            EmergencyNumber = 0,
            TicksSinceLastDrop = 0,
            TicksSinceLastIncrease = 0;

        Color           CurrentScreenColor;
        readonly Logs   EnergyLogs, IceLogs;
        PowerRegister   Register;

        class PowerRegister {
            private readonly List<IMyBatteryBlock>   batteries   = new List<IMyBatteryBlock>();
            private readonly List<IMyPowerProducer>  hydroEngines= new List<IMyPowerProducer>();
            private readonly List<IMyReactor>        reactors    = new List<IMyReactor>();

            public bool
                batteriesNominal    = true,
                hydroEnabled        = true,
                reactorsEnabled     = true;

            public PowerRegister(List<IMyPowerProducer> input) {
                foreach(IMyPowerProducer PP in input) {
                    if(PP is IMyBatteryBlock) {
                        batteries.Add((IMyBatteryBlock)PP);
                        if(batteriesNominal && (!PP.Enabled || ((IMyBatteryBlock)PP).ChargeMode == ChargeMode.Recharge))
                                batteriesNominal = false;
                    }
                    else
                    if(PP is IMyReactor) {
                        reactors.Add((IMyReactor)PP);
                        if (reactorsEnabled && !PP.Enabled) reactorsEnabled = false;
                    }
                    else
                    if(!(PP is IMySolarPanel)) {
                        hydroEngines.Add(PP);
                        if (hydroEnabled && !PP.Enabled)    hydroEnabled = false;
                    }
                }

                if(batteries.Count      <= 0) batteriesNominal= false;
                if(hydroEngines.Count   <= 0) hydroEnabled    = false;
                if(reactors.Count       <= 0) reactorsEnabled = false;
            }

            public void SetBatteriesNominal(bool nominal) {
                ChargeMode charMod = nominal ? ChargeMode.Auto : ChargeMode.Recharge;
                foreach (IMyBatteryBlock block in batteries) {
                    block.Enabled       = true;
                    block.ChargeMode    = charMod;
                }
                if (batteries.Count>0) batteriesNominal = nominal;
                else batteriesNominal = false;
            }

            public void SetHydrogenCoresEnabled(bool enabled) {
                foreach (IMyPowerProducer PP in hydroEngines)
                    PP.Enabled = enabled;

                if (hydroEngines.Count > 0) hydroEnabled = enabled;
                else hydroEnabled = false;
            }

            public void SetReactorsEnabled(bool enabled) {
                foreach (IMyReactor PP in reactors)
                    PP.Enabled = enabled;

                if (reactors.Count > 0) reactorsEnabled = enabled;
                else reactorsEnabled = false;
            }

        }

        ControllerState CurrentState = ControllerState.NORMAL;

        readonly List<IMyCargoContainer>    cargoContainters    = new List<IMyCargoContainer>();
        readonly List<IMyGasGenerator>      gasGenerators       = new List<IMyGasGenerator>();
        List<IMyGasTank>                    hydrogenTanks       = new List<IMyGasTank>();
        readonly List<IMyPowerProducer>     powerProducers      = new List<IMyPowerProducer>();
        List<IMyTextPanel>                  textPanels          = new List<IMyTextPanel>();
        readonly List<IMyProductionBlock>   industrialProducers = new List<IMyProductionBlock>();

        enum ControllerState{
            AUTO,
            NORMAL,
            COMBAT,
            EMERGENCY
        }

        Type[] ArrayOfCriticalTypes = { 
            typeof(IMyPowerProducer),
            typeof(IMyShipConnector),
            typeof(IMyShipMergeBlock),
            typeof(IMyRadioAntenna),
            typeof(IMyShipController),
            typeof(IMyTextPanel),
            typeof(IMyUpgradeModule),
            typeof(IMyTextSurface),
            typeof(IMyProgrammableBlock),
            typeof(IMyBeacon),
            typeof(IMyTimerBlock),
            typeof(IMyDoor),
            typeof(IMyThrust),
            typeof(IMyGyro)
        };

        bool BlockIsntCritical(IMyFunctionalBlock A) {
            return  (!ArrayOfCriticalTypes.Contains(A.GetType()));
        }

        void EmergencyPanicButton() {
            SwitchControllersState(ControllerState.EMERGENCY);
            List<IMyFunctionalBlock>    blocks      = new List<IMyFunctionalBlock>();   FindItemsForList(blocks);
            List<IMyBeacon>             beacons     = new List<IMyBeacon>();            FindItemsForList(beacons);
            List<IMyRadioAntenna>       antennas    = new List<IMyRadioAntenna>();      FindItemsForList(antennas);
            Register.SetReactorsEnabled(true); Register.SetBatteriesNominal(true); 
            bool suitableAntennaFound = false;
            foreach (IMyFunctionalBlock A in blocks) if (BlockIsntCritical(A)) A.Enabled = false;
            foreach(IMyBeacon bcn in beacons){
                if (!suitableAntennaFound) {
                    if (bcn.IsFunctional) {
                        bcn.Enabled = true;
                        bcn.Radius = 50000f;
                        bcn.CustomName = "SHIP SHUT DOWN BY EMERGENCY PROTOCOLS";
                        suitableAntennaFound = true;
                    }
                }
                else 
                    bcn.Enabled = false;
            }
            foreach (IMyRadioAntenna ant in antennas) {
                if (!suitableAntennaFound) {
                    if (ant.IsFunctional) {
                        ant.Enabled = true; ant.EnableBroadcasting = true;  ant.ShowShipName = true;
                        ant.Radius = 50000f;
                        ant.CustomName = "SHIP SHUT DOWN BY EMERGENCY PROTOCOLS";
                         suitableAntennaFound = true;
                    }
                }
                else 
                    ant.Enabled = false;
            }
        }

        void YouKnowWhatFuckYouUnpanicsYourEmergencyButton() {
            List<IMyFunctionalBlock> blocks = new List<IMyFunctionalBlock>(); FindItemsForList(blocks);
            foreach (IMyFunctionalBlock A in blocks) if (BlockIsntCritical(A)) A.Enabled = true;
        }

        void SwitchControllersState(ControllerState state){
            if(CurrentState==ControllerState.EMERGENCY && state!=ControllerState.EMERGENCY)
                YouKnowWhatFuckYouUnpanicsYourEmergencyButton();
            CurrentState = state;
            switch(state){
                case ControllerState.AUTO:
                    CurrentScreenColor = Color.Black;
                    break;

                case ControllerState.NORMAL:
                    CurrentScreenColor = Color.Black;
                    EnableItemsInList(true, industrialProducers);
                    Register.SetHydrogenCoresEnabled(true);
                    Register.SetBatteriesNominal(true);
                    Register.SetReactorsEnabled(false);
                    break;

                case ControllerState.COMBAT:
                    CurrentScreenColor = Color.Black;
                    EnableItemsInList(false, industrialProducers);
                    Register.SetHydrogenCoresEnabled(true);
                    Register.SetBatteriesNominal(true);
                    break;

                case ControllerState.EMERGENCY: 
                    CurrentScreenColor = new Color(255,0,0);
                    break;

                default: break;
            }
        }

        void FindHydrogenTanks() {
            List<IMyGasTank> temp = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(temp); hydrogenTanks = new List<IMyGasTank>();

            foreach (IMyGasTank tank in temp)
                if ((!ONLY_USE_BLOCKS_FROM_THIS_GRID || IsOnThisGrid(tank)) && tank.BlockDefinition.SubtypeName.Contains("HydrogenTank"))
                    hydrogenTanks.Add(tank);
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

            if (effectiveTimeIncrementer > 5) {
                TimeIncrementer = 0; return;
            }
            if (TimeIncrementerMod != 0) return;

            switch (effectiveTimeIncrementer) {
                case 0: FindItemsForList(industrialProducers); break;
                case 1: FindItemsForList(cargoContainters); break;
                case 2: FindItemsForList(powerProducers); Register = new PowerRegister(powerProducers); break;
                case 3: FindItemsForList(gasGenerators); break;
                case 4: FindHydrogenTanks(); break;
                case 5: FindTextPanels(); break;
                default: TimeIncrementer = 0; return;
            }
        }

        public class Logs {
            private float value = 0;
            private int count = 0;
            public float GetValue() { return value; }
            public void Add() { count++; }
            public void Add(float input) {
                if (count++ > 100) count = 100;

                if ((input > 0 && value < 0) || (input < 0 && value > 0))
                    count = 1;          
                if (count != 1) { value += ((input - value) / count); }
                else            { value = input; }
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

        public void Save(){ Storage = CurrentState.ToString(); }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            EnergyLogs  = new Logs();
            IceLogs     = new Logs();
            FindItemsForList(cargoContainters);
            FindItemsForList(powerProducers); Register = new PowerRegister(powerProducers);
            FindItemsForList(gasGenerators);
            FindItemsForList(industrialProducers);
            FindHydrogenTanks();
            FindTextPanels();
            IceAmount = GetIceAmount();
            SayMyName("ENERGY INFO & CONTROL");
            if(!Enum.TryParse(Storage, out CurrentState))
                CurrentState = ControllerState.NORMAL;

            CurrentScreenColor = Color.Black;
        }

        void FindItemsForList<T>(List<T> list) where T: class{
            if (ONLY_USE_BLOCKS_FROM_THIS_GRID) {
                List<T> temp = new List<T>(); GridTerminalSystem.GetBlocksOfType(temp);
                list.Clear();
                foreach (T item in temp) if (IsOnThisGrid((IMyCubeBlock)item)) list.Add(item);
            }
            else
                GridTerminalSystem.GetBlocksOfType(list);
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

            return output; // /=1000000 to get 'normal' numbers
        }

        double GetMeanHydrogenFillage() {
            double output = 0;
            if (hydrogenTanks.Count == 0) return output;
            else {
                Double tanksCount = Convert.ToDouble(hydrogenTanks.Count);
                foreach (IMyGasTank tank in hydrogenTanks) output += tank.FilledRatio;
                return (output / tanksCount);
            }
        }

        bool IsOnThisGrid(IMyCubeBlock block) { return (block != null && block.CubeGrid.Equals(Me.CubeGrid)); }

        String PrintEvaluateAndReactToEnergyInfo() {
            float 
                ShipsStoredPower    = 0,
                ShipsMaxPower       = 0,
                MaxShipOutput       = 0,
                CurrentBatteryOutput= 0,
                CurrentShipOutput   = 0;

            double
                MeanHydrogenFillage;

            int Online      = 0,
                Recharging  = 0,
                Empty       = 0,
                Offline     = 0,
                RNominal    = 0,
                ROff        = 0;

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
                     "H2 Reserves", (MeanHydrogenFillage = (GetMeanHydrogenFillage() * 100)),
                     "Current Output", CurrentShipOutput, MaxShipOutput, (CurrentShipOutput * 100 / MaxShipOutput), "");

            if (difference != 0)
                    EnergyLogs.Add(ShipsStoredPower / difference); 
            else    EnergyLogs.Add();

            if (RNominal > 0 || ROff > 0)
                    output += String.Format("\n{0,-14}: {1,9}/{2,-6}", "Cores Online", RNominal, RNominal + ROff);
            else    output += "\n No power cores present!";

            output += String.Format("\n{0,-14}: {1,9}/{2,-6} {3}", "Batteries", Online, Online + Empty + Recharging + Offline, "Online");
            if (Recharging > 0) output += String.Format("\n{2,26}{0,-6} {1}", Recharging, "Recharging", ""); else output+="\n";
            if (Empty > 0) output += String.Format("\n{2,26}{0,-6} {1}", Empty, "Empty", ""); else output+="\n";

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
            if (remainingTime > 0)  
                    output += String.Format("\n{0,-14}: {2,9}{1}", "Ice reserves", SecondsToTimeInString(remainingTime), "");
            else    output += String.Format("\n{0,-14}: {2,3}{1}", "Ice reserves", remainingTime == 0? "stable":"rising", "");

            float
                outputPercent   = 100 * CurrentShipOutput / MaxShipOutput,
                powerPercent    = 100 * ShipsStoredPower / ShipsMaxPower;

            if (CurrentState == ControllerState.AUTO) {
                output += "\n\nAUTOMATIC POWER CONTROL";
                if (outputPercent > 90f || powerPercent < 10f) {
                    if ((TicksSinceLastIncrease += 5) < 120) { TicksSinceLastDrop = 0; }
                    else {
                        TicksSinceLastIncrease = 0; TicksSinceLastDrop = 0;
                        if (!Register.batteriesNominal) {
                            Register.SetBatteriesNominal(true); EmergencyNumber = 1;
                        }
                        else if (!Register.hydroEnabled) {
                            Register.SetHydrogenCoresEnabled(true); EmergencyNumber = 2;
                        }
                        else if (ItemsInListAreEnabled(industrialProducers)) {
                            EnableItemsInList(false, industrialProducers); EmergencyNumber = 3;
                        }
                        else if (!Register.reactorsEnabled) {
                            Register.SetReactorsEnabled(true); EmergencyNumber = 4;
                        }
                        else if (powerPercent < 5f && MeanHydrogenFillage < 10f) {
                            EmergencyPanicButton(); EmergencyNumber = 5;
                        }
                    }
                }
                else {
                    if (outputPercent < 10f && powerPercent > 20f) {
                        if (TicksSinceLastDrop++ < 120) { TicksSinceLastIncrease = 0; }
                        else { 
                            TicksSinceLastIncrease = 0; TicksSinceLastDrop = 0;
                            if (Register.reactorsEnabled) {
                                Register.SetReactorsEnabled(false); EmergencyNumber = -1;
                            }
                            else if (!ItemsInListAreEnabled(industrialProducers)) {
                                EnableItemsInList(true, industrialProducers); EmergencyNumber = -2;
                            }
                            else{
                                else if (Register.hydroEnabled && Register.batteriesNominal) {
                                    Register.SetHydrogenCoresEnabled(false); EmergencyNumber = -3;
                                }
                                else if (outputPercent < 3 && powerPercent <50f && MeanHydrogenFillage > 0.85D) {
                                    Register.SetHydrogenCoresEnabled(true);
                                    Register.SetBatteriesNominal(false);
                                    EmergencyNumber = -4;
                                }
                            }
                        }
                    }
                    if(Register.batteriesNominal){
                        if(Register.hydroEnabled && MeanHydrogenFillage < 0.25D) 
                            Register.SetHydrogenCoresEnabled(false);
                    }
                    else{
                        if(Register.hydroEnabled && MeanHydrogenFillage < 0.5D)
                            Register.SetBatteriesNominal(true);
                    }
                }

                string loadingBar = "";
                if      (EmergencyNumber < 0 || TicksSinceLastDrop>TicksSinceLastIncrease)
                    for (int i = 0; i < (120 - TicksSinceLastDrop) / 3; i++) loadingBar += "|";
                else if (EmergencyNumber > 0 || TicksSinceLastDrop<TicksSinceLastIncrease)
                    for (int i = 0; i < TicksSinceLastIncrease / 3; i++) loadingBar += "|";

                output += String.Format("\n\n{0}{2} [{1,-40}]", EmergencyNumber>0?"-":"+", loadingBar, EmergencyNumber * (EmergencyNumber<0? -1:1));
            }
            else {
                if (CurrentState == ControllerState.EMERGENCY) output += "\n\nSHIP IS IN EMERGENCY MODE";
                else {
                    if (CurrentState == ControllerState.COMBAT)     output += "\n\nPOWER REROUTED TO COMBAT SYSTEMS";
                    else if (CurrentState == ControllerState.NORMAL)output += "\n\nPOWER SYSTEMS USING DEFAULT SETTINGS.";

                    if(powerPercent <= 20f && (Register.hydroEnabled == false || MeanHydrogenFillage < 10f)) {
                        int RedValueOfNewColor = (int)(200 - ((powerPercent - 10f)*20));
                        if (powerPercent < 10f) SwitchControllersState(ControllerState.AUTO);
                        CurrentScreenColor = new Color(RedValueOfNewColor,0,0);
                    }
                }
            }
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
                    case "EMON":
                        EmergencyPanicButton();
                        break;

                    case "EMOFF":
                        if(CurrentState == ControllerState.Emergency) SwitchControllersState(ControllerState.NORMAL);
                        break;

                    default:
                        ControllerState state;
                        if (Enum.TryParse(command[0], out state)) SwitchControllersState(state);
                        break;
                }
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0){
                Output(PrintEvaluateAndReactToEnergyInfo());
                FindNeededBlocks(updateSource);
            }
            else
                EvaluateInputArgument(argument);
        }
    }
}
