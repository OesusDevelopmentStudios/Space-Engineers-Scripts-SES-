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

        enum State {
            Locked,
            Ready,
            Unlocked,

            No_Connector
        }

        State currentState;

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public bool IsOnThisGrid(IMyCubeBlock block) {
            if (block != null && block.CubeGrid.Equals(Me.CubeGrid))
                return true;
            else
                return false;
        }

        List<IMyShipConnector> list = new List<IMyShipConnector>();

        public void SetDamp(bool dampOn) {
            List<IMyShipController> controls = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controls);

            if (controls.Count <= 0) return;

            IMyShipController SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in controls) {
                if (IsOnThisGrid(controler) && controler.IsWorking) {
                    SHIP_CONTROLLER = controler;
                    if (controler.IsMainCockpit) break;
                }
            }

            if (SHIP_CONTROLLER == null) return;

            if (dampOn) {
                /// Problematic part
                if (SHIP_CONTROLLER.GetShipSpeed() > 0d) {
                    //SHIP_CONTROLLER.DampenersOverride = false;
                }
                else
                    SHIP_CONTROLLER.DampenersOverride = true;
            }
            else {
                SHIP_CONTROLLER.DampenersOverride = false;
            }

        }

        public string CheckState() {
            List<IMyShipConnector> list = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(list);
            State state = State.No_Connector;
            foreach (IMyShipConnector con in list) {
                if (IsOnThisGrid(con)){
                    this.list.Add(con);
                    if(con.Status == MyShipConnectorStatus.Connected) {
                        state = State.Locked; break;
                    }
                    else 
                    if(con.Status == MyShipConnectorStatus.Connectable) {
                        state = State.Ready;
                    }
                    else
                    if (state == State.No_Connector) {
                        state = State.Unlocked;
                    }
                }
            }
            currentState = state;
            return state.ToString();
        }

        public void SetBatteries(bool recharge) {
            List<IMyBatteryBlock> temp      = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyBatteryBlock batt in temp) {
                if (IsOnThisGrid(batt)) {
                    if (recharge) 
                        batt.ChargeMode = ChargeMode.Recharge;
                    else
                        batt.ChargeMode = ChargeMode.Auto;
                }
            }
        }

        public void SetH2Tanks(bool stockpile) {
            List<IMyGasTank> temp   = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyGasTank tank in temp) {
                if (tank.Capacity > 100000f && IsOnThisGrid(tank)) {
                    tank.Stockpile = stockpile;
                }
            }
        }

        public void Run() {
            switch (currentState) {
                case State.Ready:
                    SetBatteries(true);
                    SetH2Tanks(true);
                    SetDamp(false);
                    foreach (IMyShipConnector con in list) con.Connect();
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case State.Locked:
                    SetBatteries(false);
                    SetH2Tanks(false);
                    SetDamp(true);
                    foreach (IMyShipConnector con in list) con.Disconnect();
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case State.No_Connector:
                case State.Unlocked:
                    break;
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Update10 | UpdateType.Update100)) > 0) {Echo(CheckState());}
            else {Run();}
        }
    }
}
