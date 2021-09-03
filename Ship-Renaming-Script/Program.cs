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
        public Program() {
            string name = Me.CubeGrid.CustomName;
            string[] split = name.Split(' ');
            if (!(split.Length > 1 && split[1].ToUpper().Equals("GRID"))) 
                ShipName = name;
            else 
                ShipName = "Ship";
        }

        const string IGNORE = "[NO-RENAME]";
        string   ShipName   = "";
        string[] Alphabet   = {"A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"};

        public string GetAlphabet(int index) {
            int count = Alphabet.Count();
            if (index < count) return Alphabet[index];
            else {
                string output = (Alphabet[(index / count)-1]) + (Alphabet[index % count]);
                return output;
            }
        }

        List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
        void FindAllBlocks(bool thisOnly = true){
            List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(temp); allBlocks = new List<IMyTerminalBlock>();
            foreach(IMyTerminalBlock block in temp)
                if((!thisOnly || IsOnThisGrid(block)) && !block.CustomName.Contains(IGNORE)) allBlocks.Add(block);
        }

        void FindItemsForList<T>(List<T> list) where T: IMyTerminalBlock{
            list.Clear();
            foreach(IMyTerminalBlock block in allBlocks)
                if(block is T) list.Add((T)block);
        }

        //================================================
        public void GasTanks() {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            FindItemsForList(tanks);
            int OxyTanks = 0,
                H2Tanks  = 0,
                smolH2   = 0;

            foreach (IMyGasTank tank in tanks) {
                if (tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) {
                    if(tank.BlockDefinition.SubtypeName.Contains("LargeHydrogenTank"))
                        tank.CustomName = ShipName+"/H2 Tank "+ GetAlphabet(H2Tanks++);
                    else
                        tank.CustomName = ShipName + "/Small H2 Tank " + GetAlphabet(smolH2++);
                    tank.ShowInInventory = true;
                    tank.ShowInTerminal = false;
                    tank.ShowInToolbarConfig = false;
                }
                else {
                    tank.CustomName = ShipName + "/Oxygen Tank " + GetAlphabet(OxyTanks++);
                    tank.ShowInInventory = true;
                    tank.ShowInTerminal = false;
                    tank.ShowInToolbarConfig = false;
                }
            }
        }
        public void OxyTanks() {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            FindItemsForList(tanks);
            int OxyTanks = 0;

            foreach (IMyGasTank tank in tanks) {
                if (!tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) {
                    tank.CustomName = ShipName + "/Oxygen Tank " + GetAlphabet(OxyTanks++);
                    tank.ShowInInventory = true;
                    tank.ShowInTerminal = false;
                    tank.ShowInToolbarConfig = false;
                }
            }
        }
        public void H2Tanks() {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            FindItemsForList(tanks);
            int H2Tanks = 0,
                smolH2 = 0;

            foreach (IMyGasTank tank in tanks) {
                if (!thisOnly || IsOnThisGrid(tank)) {
                    if (tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) {
                        if(tank.BlockDefinition.SubtypeName.Contains("LargeHydrogenTank"))
                            tank.CustomName = ShipName + "/H2 Tank " + GetAlphabet(H2Tanks++);
                        else
                            tank.CustomName = ShipName + "/Small H2 Tank " + GetAlphabet(smolH2++);
                        tank.ShowInInventory = true;
                        tank.ShowInTerminal = false;
                        tank.ShowInToolbarConfig = false;
                    }
                }
            }
        }
        //================================================
        public void H2Gens() {
            List<IMyGasGenerator> gens = new List<IMyGasGenerator>();
            int increm = 0;
            FindItemsForList(gens);
            foreach (IMyGasGenerator gen in gens) {
                gen.CustomName = ShipName + "/H2 Generator " + GetAlphabet(increm++);
                gen.ShowInInventory = true;
                gen.ShowInTerminal = false;
                gen.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Cargos() {
            List<IMyCargoContainer> conts = new List<IMyCargoContainer>();
            FindItemsForList(conts);
            int Cont = 0;

            foreach (IMyCargoContainer cont in conts) {
                switch (cont.BlockDefinition.SubtypeName) {
                    case "LargeBlockLargeContainer":
                    case "SmallBlockMediumContainer":
                    case "SmallBlockLargeContainer":
                        cont.CustomName = ShipName + "/Cargo Container " + GetAlphabet(Cont++);
                        cont.ShowInInventory = true;
                        cont.ShowInTerminal = false;
                        cont.ShowInToolbarConfig = false;
                        break;
                    case "SmallBlockSmallContainer":
                    case "LargeBlockSmallContainer":
                        cont.CustomName = ShipName + "/Cargo Entry Point";
                        cont.ShowInInventory = true;
                        cont.ShowInTerminal = false;
                        cont.ShowInToolbarConfig = false;
                        break;
                    default:
                        // trash.
                        cont.ShowInInventory = false;
                        cont.ShowInTerminal = false;
                        cont.ShowInToolbarConfig = false;
                        break;
                }
            }
        }
        //================================================
        public void PowerProds() {
            List<IMyPowerProducer> prods = new List<IMyPowerProducer>();
            FindItemsForList(prods);
            int reactorNo = 0;
            int BatteryNo = 0;

            foreach (IMyPowerProducer prod in prods) {
                if(prod is IMyReactor) {
                    prod.CustomName = ShipName + "/Nuclear Reactor " + GetAlphabet(reactorNo++);
                    prod.ShowInInventory = true;
                    prod.ShowInTerminal = false;
                    prod.ShowInToolbarConfig = false;
                }
                else
                if (prod is IMySolarPanel) {
                    prod.CustomName = ShipName + "/Solar Panel";
                    prod.ShowInInventory = false;
                    prod.ShowInTerminal = false;
                    prod.ShowInToolbarConfig = false;
                }
                else
                if (prod is IMyBatteryBlock) {
                    prod.CustomName = ShipName + "/Battery " + ++BatteryNo;
                    prod.ShowInInventory = false;
                    prod.ShowInTerminal = false;
                    prod.ShowInToolbarConfig = false;
                }
                else {
                    prod.CustomName = ShipName + "/Hydrogen Power Core";
                    prod.ShowInInventory = false;
                    prod.ShowInTerminal = false;
                    prod.ShowInToolbarConfig = false;
                }
            }
        }
        public void Batteries() {
            List<IMyBatteryBlock> bats = new List<IMyBatteryBlock>();
            FindItemsForList(bats);
            int BatteryNo = 0;
            foreach (IMyBatteryBlock bat in bats) {
                bat.CustomName = ShipName + "/Battery "+ ++BatteryNo;
                bat.ShowInInventory = false;
                bat.ShowInTerminal = false;
                bat.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Gyros() {
            List<IMyGyro> gyros = new List<IMyGyro>();
            FindItemsForList(gyros);
            int GyroNo = 0;
            foreach (IMyGyro gyro in gyros) {
                gyro.CustomName = ShipName + "/Gyroscope " + ++GyroNo;
                gyro.ShowInTerminal = false;
                gyro.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Weapons() {
            List<IMyUserControllableGun> weps = new List<IMyUserControllableGun>();
            FindItemsForList(weps);
            int 
                ASG = 0,
                ML  = 0,
                GG  = 0,
                PDT = 0;

            foreach (IMyUserControllableGun wep in weps) {
                if (wep is IMyLargeGatlingTurret) {
                    wep.CustomName = ShipName + "/Anti-Ship Gun " + GetAlphabet(ASG++);
                    wep.ShowInInventory = true;
                    wep.ShowInTerminal = false;
                    wep.ShowInToolbarConfig = false;
                }
                else
                if (wep is IMySmallMissileLauncher || wep is IMySmallMissileLauncherReload) {
                    wep.CustomName = ShipName + "/Missile Launcher " + GetAlphabet(ML++);
                    wep.ShowInInventory = true;
                    wep.ShowInTerminal = false;
                    wep.ShowInToolbarConfig = false;
                }
                else
                if (wep is IMySmallGatlingGun) {
                    wep.CustomName = ShipName + "/Gatling Gun " + GetAlphabet(GG++);
                    wep.ShowInInventory = true;
                    wep.ShowInTerminal = false;
                    wep.ShowInToolbarConfig = false;
                }
                else 
                if (wep is IMyLargeInteriorTurret) {
                    wep.CustomName = ShipName + "/Point-Defence-Turret " + GetAlphabet(PDT++);
                    wep.ShowInInventory = true;
                    wep.ShowInTerminal = false;
                    wep.ShowInToolbarConfig = false;
                }
                else {
                    wep.CustomName = ShipName + "/.Undefined Weapon";
                    wep.ShowInInventory = true;
                    wep.ShowInTerminal = false;
                    wep.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Antennas() {
            List<IMyBeacon> becs = new List<IMyBeacon>();
            FindItemsForList(becs);
            foreach (IMyBeacon bec in becs) {
                bec.CustomName = ShipName + "/Beacon";
                bec.HudText = ShipName;
                bec.ShowInTerminal = true;
                bec.ShowInToolbarConfig = true;
            }

            List<IMyRadioAntenna> ants = new List<IMyRadioAntenna>();
            FindItemsForList(ants);
            foreach (IMyRadioAntenna ant in ants) {
                ant.CustomName = ShipName + "/Antenna";
                ant.HudText = ShipName;
                ant.ShowInTerminal = true;
                ant.ShowInToolbarConfig = true;
            }
        }
        //================================================
        public void Connectors() {
            List<IMyShipConnector> items = new List<IMyShipConnector>();
            FindItemsForList(items);

            int con = 0;

            foreach (IMyShipConnector item in items) {
                item.CustomName = ShipName + "/Connector " + GetAlphabet(con++);;
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Assemblers() {
            List<IMyAssembler> items = new List<IMyAssembler>();
            FindItemsForList(items);

            foreach (IMyAssembler item in items) {
                if (item.BlockDefinition.SubtypeName.Contains("Assembler")) {
                    if (item.BlockDefinition.SubtypeName.Contains("Large")) {
                        item.CustomName = ShipName + "/Assembler";
                        item.ShowInTerminal = false;
                        item.ShowInToolbarConfig = false;
                    }
                    else {
                        item.CustomName = ShipName + "/Basic Assembler";
                        item.ShowInTerminal = false;
                        item.ShowInToolbarConfig = false;
                    }
                }
                else {
                    item.CustomName = ShipName + "/Survival Kit";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Refineriers() {
            List<IMyRefinery> items = new List<IMyRefinery>();
            FindItemsForList(items);

            foreach (IMyRefinery item in items) {
                item.CustomName = ShipName + "/Refinery";
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void GravGens() {
            List<IMyGravityGeneratorBase> items = new List<IMyGravityGeneratorBase>();
            FindItemsForList(items);

            foreach (IMyGravityGeneratorBase item in items) {
                item.CustomName = ShipName + "/Gravity Generator";
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void CryoChambers() {
            List<IMyCryoChamber> items = new List<IMyCryoChamber>();
            FindItemsForList(items);

            int cryoNo = 0;

            foreach (IMyCryoChamber item in items) {
                if (item.BlockDefinition.SubtypeName.Contains("BlockCryoChamber")) {
                    item.CustomName = ShipName + "/Cryo Chamber " + GetAlphabet(cryoNo++);
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
                else {
                    item.CustomName = ShipName + "/Bed";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void AirVents() {
            List<IMyAirVent> items = new List<IMyAirVent>();
            FindItemsForList(items);

            foreach (IMyAirVent item in items) {
                item.CustomName = ShipName + "/Air Vent";
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void ButtonPanels() {
            List<IMyButtonPanel> items = new List<IMyButtonPanel>();
            FindItemsForList(items);

            foreach (IMyButtonPanel item in items) {
                item.CustomName = ShipName + "/Button Panel";
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Doors() {
            List<IMyDoor> items = new List<IMyDoor>();
            FindItemsForList(items);

            foreach (IMyDoor item in items) {
                if(!(item is IMyAirtightHangarDoor)) item.CustomName = ShipName + "/Door";
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void Modules() {
            List<IMyUpgradeModule> items = new List<IMyUpgradeModule>();
            FindItemsForList(items);

            foreach (IMyUpgradeModule item in items) {
                if (item.BlockDefinition.SubtypeName.Contains("Productivity")) { 
                    item.CustomName = ShipName + "/Speed Module";
                }
                else
                if (item.BlockDefinition.SubtypeName.Contains("Effectiveness")) {
                    item.CustomName = ShipName + "/Yield Module";
                }
                else {
                    item.CustomName = ShipName + "/Power Module";
                }
                item.ShowInTerminal = false;
                item.ShowInToolbarConfig = false;
            }
        }
        //================================================
        public void CockpitTrash() {
            List<IMyCockpit> items = new List<IMyCockpit>();
            FindItemsForList(items);

            foreach (IMyCockpit item in items) {
                if (!item.BlockDefinition.SubtypeName.Contains("Cockpit")) {
                    item.ShowInInventory = false;
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================

        public bool IsOnThisGrid(IMyCubeBlock block) { return (block != null && block.CubeGrid.Equals(Me.CubeGrid)); }
               
        public void executeNameAll() {
            switch (allInc) {
                case 0:
                    GasTanks();
                    break;

                case 2:
                    Cargos();
                    break;

                case 4:
                    PowerProds();
                    break;

                case 6:
                    GasTanks();
                    break;

                case 8:
                    Weapons();
                    break;

                case 10:
                    Antennas();
                    CockpitTrash();
                    break;

                case 12:
                    Gyros();
                    break;

                case 14:
                    Assemblers();
                    break;

                case 16:
                    Connectors();
                    break;

                case 18:
                    Refineriers();
                    break;

                case 20:
                    GravGens();
                    break;

                case 22:
                    CryoChambers();
                    break;

                case 24:
                    AirVents();
                    break;

                case 26:
                    ButtonPanels();
                    break;

                case 28:
                    Doors();
                    break;

                case 30:
                    Modules();
                    H2Gens();
                    break;

                case 31:
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    break;
            }
            allInc++;

            Echo((allInc * 100 / 32) + "% Completed");
        }

        bool notAll;
        int allInc = 0;

        public void Main(string argument, UpdateType updateSource) {
            String[] eval = argument.ToLower().Split(' ');
            if (argument.Equals("")) {
                executeNameAll();
            }
            if (eval[0].Equals("name") || eval[0].Equals("rename")) {
                string arg = eval.Length > 1 ? eval[1] : "all";
                notAll = eval.Length > 2 ? (eval[2].Equals("unlimited")||eval[2].Equals("all") ? false:true) : true;
                FindAllBlocks(notAll);
                switch (arg) {
                    case "all":
                    case "ship":
                        allInc = 0;
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        break;

                    case "gastanks":
                    case "tanks":
                        GasTanks();
                        break;

                    case "oxytanks":
                    case "o2tanks":
                        OxyTanks();
                        break;

                    case "hydrotanks":
                    case "h2tanks":
                        H2Tanks();
                        break;

                    case "gens":
                    case "h2gens":
                    case "generators":
                        H2Gens();
                        break;

                    case "cargo":
                    case "containers":
                        Cargos();
                        break;

                    case "power":
                        PowerProds();
                        break;

                    case "batteries":
                        Batteries();
                        break;

                    case "gyros":
                    case "kebab":
                        Gyros();
                        break;

                    case "weapons":
                        Weapons();
                        break;

                    case "antennas":
                        Antennas();
                        break;
                }
            }
        }
    }
}
