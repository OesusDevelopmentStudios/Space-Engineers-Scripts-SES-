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

        int timeIncrementer = 0;

        List<IMyShipConnector>  shipConnectors      = new List<IMyShipConnector>();
        List<IMyShipController> shipControllers     = new List<IMyShipController>();
        List<IMyGasTank>        shipHydrogenTanks   = new List<IMyGasTank>();
        List<IMyBatteryBlock>   shipBatteries       = new List<IMyBatteryBlock>();

        void FindShipConnectors(){
            List<IMyShipConnector> temp = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(temp); shipConnectors = new List<IMyShipConnector>();

            foreach(IMyShipConnector con in temp) if(IsOnThisGrid(con)) shipConnectors.Add(con);
        }

        void FindShipControllers(){
            List<IMyShipController> temp = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(temp); shipControllers = new List<IMyShipController>();

            foreach(IMyShipController con in temp) if(IsOnThisGrid(con)) shipControllers.Add(con);
        }

        void FindShipHydrogenTanks(){
            List<IMyGasTank> temp = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(temp); shipHydrogenTanks = new List<IMyGasTank>();

            foreach(IMyGasTank tank in temp) if(IsOnThisGrid(tank) && tank.BlockDefinition.SubtypeName.Contains("HydrogenTank")) shipHydrogenTanks.Add(tank);
        }

        void FindShipBatteries(){
            List<IMyBatteryBlock> temp = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(temp); shipBatteries = new List<IMyBatteryBlock>();

            foreach(IMyBatteryBlock bat in temp) if(IsOnThisGrid(bat)) shipBatteries.Add(bat);
        }

        void FindNeededShipBlocks(UpdateType updateSource){
            int 
                timeLimiter = (updateSource & UpdateType.Update10)>0? 30:3, 
                effectiveTimeIncrementer = timeIncrementer/timeLimiter,
                timeIncrementerMod = timeIncrementer%timeLimiter;
            timeIncrementer++;

            if(effectiveTimeIncrementer > 3){
                timeIncrementer = 0; return;
            }
            if(timeIncrementerMod!=0) return;

            switch(effectiveTimeIncrementer){
                case 0: FindShipConnectors(); break;
                case 1: FindShipControllers(); break;
                case 2: FindShipBatteries(); break;
                case 3: FindShipHydrogenTanks(); break;
                default: timeIncrementer = 0; return;
            }         
        }

        State currentState;
        enum State {
            Locked,
            Ready,
            Unlocked,

            No_Connector
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            FindShipBatteries();
            FindShipConnectors();
            FindShipControllers();
            FindShipHydrogenTanks();
            Echo(GetCurrentStateName());
        }

        bool IsOnThisGrid(IMyCubeBlock block) { return (block != null && block.CubeGrid.Equals(Me.CubeGrid)); }

        void SetDampenersOnline(bool dampOn) {
            if (shipControllers.Count <= 0) return;

            IMyShipController SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in shipControllers) 
                if (controler.IsWorking) { SHIP_CONTROLLER = controler; if (controler.IsMainCockpit) break; }

            if (SHIP_CONTROLLER != null)
            if (dampOn) { if (SHIP_CONTROLLER.GetShipSpeed() <= 0d) SHIP_CONTROLLER.DampenersOverride = true; }
            else SHIP_CONTROLLER.DampenersOverride = false;
        }

        string GetCurrentStateName() {
            State state = State.No_Connector;
            foreach (IMyShipConnector con in shipConnectors) {
                if      (con.Status == MyShipConnectorStatus.Connected)   { state = State.Locked; break; }
                else if (con.Status == MyShipConnectorStatus.Connectable)   state = State.Ready;
                else if (state == State.No_Connector)                       state = State.Unlocked;
            }
            currentState = state;
            return state!=State.No_Connector? state.ToString() : "No connector";
        }

        void SetAllBatteriesToRecharge(bool recharge) {
            foreach (IMyBatteryBlock batt in shipBatteries) {
                if (recharge) 
                    batt.ChargeMode = ChargeMode.Recharge;
                else
                    batt.ChargeMode = ChargeMode.Auto;
            }
        }

        void SetAllHydrogenTanksToStockpile(bool stockpile) { foreach(IMyGasTank tank in shipHydrogenTanks) tank.Stockpile = stockpile; }

        void TryToChangeState() {
            switch (currentState) {
                case State.Ready:
                    SetAllBatteriesToRecharge(true);
                    SetAllHydrogenTanksToStockpile(true);
                    SetDampenersOnline(false);
                    foreach (IMyShipConnector con in shipConnectors) con.Connect();
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case State.Locked:
                    SetAllBatteriesToRecharge(false);
                    SetAllHydrogenTanksToStockpile(false);
                    SetDampenersOnline(true);
                    foreach (IMyShipConnector con in shipConnectors) con.Disconnect();
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case State.No_Connector:
                case State.Unlocked:
                    break;
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Update10 | UpdateType.Update100)) > 0) {
                Echo(GetCurrentStateName());
                FindNeededShipBlocks(updateSource);
            }
            else TryToChangeState();
        }
    }
}
