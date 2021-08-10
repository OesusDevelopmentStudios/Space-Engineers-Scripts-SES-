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
            if (index < Alphabet.Count()) return Alphabet[index];
            else {
                string output = (Alphabet[(index / Alphabet.Count())-1]) + (Alphabet[index % Alphabet.Count()]);
                return output;
            }
        }

        //================================================
        public void GasTanks  (bool thisOnly = true) {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(tanks);
            int OxyTanks = 0,
                H2Tanks  = 0,
                smolH2   = 0;

            foreach (IMyGasTank tank in tanks) {
                if (tank.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(tank)) { 
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
        }
        public void OxyTanks  (bool thisOnly = true) {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(tanks);
            int OxyTanks = 0;

            foreach (IMyGasTank tank in tanks) {
                if (tank.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(tank)) {
                    if (!tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) {
                        tank.CustomName = ShipName + "/Oxygen Tank " + GetAlphabet(OxyTanks++);
                        tank.ShowInInventory = true;
                        tank.ShowInTerminal = false;
                        tank.ShowInToolbarConfig = false;
                    }
                }
            }
        }
        public void H2Tanks   (bool thisOnly = true) {
            List<IMyGasTank> tanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(tanks);
            int increm = 0;

            foreach (IMyGasTank tank in tanks) {
                if (tank.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(tank)) {
                    if (tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) {
                        tank.CustomName = ShipName + "/Hydrogen Tank " + GetAlphabet(increm++);
                        tank.ShowInInventory = true;
                        tank.ShowInTerminal = false;
                        tank.ShowInToolbarConfig = false;
                    }
                }
            }
        }
        //================================================
        public void H2Gens    (bool thisOnly = true) {
            List<IMyGasGenerator> gens = new List<IMyGasGenerator>();
            int increm = 0;
            GridTerminalSystem.GetBlocksOfType(gens);
            foreach (IMyGasGenerator gen in gens) {
                if (gen.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(gen)) {
                    gen.CustomName = ShipName + "/H2 Generator " + GetAlphabet(increm++);
                    gen.ShowInInventory = true;
                    gen.ShowInTerminal = false;
                    gen.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Cargos    (bool thisOnly = true) {
            List<IMyCargoContainer> conts = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(conts);
            int Cont = 0;

            foreach (IMyCargoContainer cont in conts) {
                if (cont.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(cont)) {
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
        }
        //================================================
        public void PowerProds(bool thisOnly = true) {
            List<IMyPowerProducer> prods = new List<IMyPowerProducer>();
            GridTerminalSystem.GetBlocksOfType(prods);
            int reactorNo = 0;
            int BatteryNo = 0;

            foreach (IMyPowerProducer prod in prods) {
                if (prod.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(prod)) {
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
        }
        public void Batteries (bool thisOnly = true) {
            List<IMyBatteryBlock> bats = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(bats);
            int BatteryNo = 0;
            foreach (IMyBatteryBlock bat in bats) {
                if (bat.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(bat)) {
                    bat.CustomName = ShipName + "/Battery "+ ++BatteryNo;
                    bat.ShowInInventory = false;
                    bat.ShowInTerminal = false;
                    bat.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Gyros     (bool thisOnly = true) {
            List<IMyGyro> gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType(gyros);
            int GyroNo = 0;
            foreach (IMyGyro gyro in gyros) {
                if (gyro.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(gyro)) {
                    gyro.CustomName = ShipName + "/Gyroscope " + ++GyroNo;
                    gyro.ShowInTerminal = false;
                    gyro.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Weapons   (bool thisOnly = true) {
            List<IMyUserControllableGun> weps = new List<IMyUserControllableGun>();
            GridTerminalSystem.GetBlocksOfType(weps);
            int 
                ASG = 0,
                ML  = 0,
                GG  = 0,
                PDT = 0;

            foreach (IMyUserControllableGun wep in weps) {
                if (wep.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(wep)) {
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
        }
        //================================================
        public void Antennas  (bool thisOnly = true) {
            List<IMyBeacon> becs = new List<IMyBeacon>();
            GridTerminalSystem.GetBlocksOfType(becs);
            foreach (IMyBeacon bec in becs) {
                if (bec.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(bec)) {
                    bec.CustomName = ShipName + "/Beacon";
                    bec.HudText = ShipName;
                    bec.ShowInTerminal = true;
                    bec.ShowInToolbarConfig = true;
                }
            }

            List<IMyRadioAntenna> ants = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(ants);
            foreach (IMyRadioAntenna ant in ants) {
                if (ant.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(ant)) {
                    ant.CustomName = ShipName + "/Antenna";
                    ant.HudText = ShipName;
                    ant.ShowInTerminal = true;
                    ant.ShowInToolbarConfig = true;
                }
            }
        }
        //================================================
        public void Connectors(bool thisOnly = true) {
            List<IMyShipConnector> items = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyShipConnector item in items) {
                if (item.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(item)) {
                    item.CustomName = ShipName + "/Connector";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Assemblers(bool thisOnly = true) {
            List<IMyAssembler> items = new List<IMyAssembler>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyAssembler item in items) {
                if (item.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(item)) {
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
        }
        //================================================
        public void Refineriers(bool thisOnly = true) {
            List<IMyRefinery> items = new List<IMyRefinery>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyRefinery item in items) {
                if (item.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(item)) {
                    item.CustomName = ShipName + "/Refinery";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void GravGens(bool thisOnly = true) {
            List<IMyGravityGeneratorBase> items = new List<IMyGravityGeneratorBase>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyGravityGeneratorBase item in items) {
                if (item.CustomName.Contains(IGNORE))
                    continue;

                if (!thisOnly || IsOnThisGrid(item)) {
                    item.CustomName = ShipName + "/Gravity Generator";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void CryoChambers(bool thisOnly = true) {
            List<IMyCryoChamber> items = new List<IMyCryoChamber>();
            GridTerminalSystem.GetBlocksOfType(items);

            int cryoNo = 0;

            foreach (IMyCryoChamber item in items) {
                if (item.CustomName.Contains(IGNORE)) continue;
                if (!thisOnly || IsOnThisGrid(item)) {
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
        }
        //================================================
        public void AirVents(bool thisOnly = true) {
            List<IMyAirVent> items = new List<IMyAirVent>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyAirVent item in items) {
                if (!thisOnly || IsOnThisGrid(item)) {
                    item.CustomName = ShipName + "/Air Vent";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void ButtonPanels(bool thisOnly = true) {
            List<IMyButtonPanel> items = new List<IMyButtonPanel>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyButtonPanel item in items) {
                if (!thisOnly || IsOnThisGrid(item)) {
                    item.CustomName = ShipName + "/Button Panel";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Doors(bool thisOnly = true) {
            List<IMyDoor> items = new List<IMyDoor>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyDoor item in items) {
                if (!thisOnly || IsOnThisGrid(item)) {
                    if(!(item is IMyAirtightHangarDoor)) item.CustomName = ShipName + "/Door";
                    item.ShowInTerminal = false;
                    item.ShowInToolbarConfig = false;
                }
            }
        }
        //================================================
        public void Modules(bool thisOnly = true) {
            List<IMyUpgradeModule> items = new List<IMyUpgradeModule>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyUpgradeModule item in items) {
                if (!thisOnly || IsOnThisGrid(item)) {
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
        }
        //================================================
        public void CockpitTrash(bool thisOnly = true) {
            List<IMyCockpit> items = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(items);

            foreach (IMyCockpit item in items) {
                if (!thisOnly || IsOnThisGrid(item)) {
                    if (!item.BlockDefinition.SubtypeName.Contains("Cockpit")) {
                        item.ShowInInventory = false;
                        item.ShowInTerminal = false;
                        item.ShowInToolbarConfig = false;
                    }
                }
            }
        }
        //================================================
        //IMyUpgradeModule

        public bool IsOnThisGrid(IMyCubeBlock block) {
            if (block != null && block.CubeGrid.Equals(Me.CubeGrid))
                return true;
            else
                return false;
        }
               
        public void executeNameAll() {
            switch (allInc) {
                case 0:
                    GasTanks(notAll);
                    break;

                case 2:
                    Cargos(notAll);
                    break;

                case 4:
                    PowerProds(notAll);
                    break;

                case 6:
                    GasTanks(notAll);
                    break;

                case 8:
                    Weapons(notAll);
                    break;

                case 10:
                    Antennas(notAll);
                    CockpitTrash(notAll);
                    break;

                case 12:
                    Gyros(notAll);
                    break;

                case 14:
                    Assemblers(notAll);
                    break;

                case 16:
                    Connectors(notAll);
                    break;

                case 18:
                    Refineriers(notAll);
                    break;

                case 20:
                    GravGens(notAll);
                    break;

                case 22:
                    CryoChambers(notAll);
                    break;

                case 24:
                    AirVents(notAll);
                    break;

                case 26:
                    ButtonPanels(notAll);
                    break;

                case 28:
                    Doors(notAll);
                    break;

                case 30:
                    Modules(notAll);
                    H2Gens(notAll);
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
                switch (arg) {
                    case "all":
                    case "ship":
                        allInc = 0;
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        break;

                    case "gastanks":
                    case "tanks":
                        GasTanks(notAll);
                        break;

                    case "oxytanks":
                    case "o2tanks":
                        OxyTanks(notAll);
                        break;

                    case "hydrotanks":
                    case "h2tanks":
                        H2Tanks(notAll);
                        break;

                    case "gens":
                    case "h2gens":
                    case "generators":
                        H2Gens(notAll);
                        break;

                    case "cargo":
                    case "containers":
                        Cargos(notAll);
                        break;

                    case "power":
                        PowerProds(notAll);
                        break;

                    case "batteries":
                        Batteries(notAll);
                        break;

                    case "gyros":
                    case "kebab":
                        Gyros(notAll);
                        break;

                    case "weapons":
                        Weapons(notAll);
                        break;

                    case "antennas":
                        Antennas(notAll);
                        break;
                }
            }
        }
    }
}
