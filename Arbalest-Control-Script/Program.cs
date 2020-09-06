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

        /// CONSTANTS
        public const int 
            ACCELERATORS_NOMINAL_NUMBER = 4,
            CONSTRUCTORS_NOMINAL_NUMBER = 4;

        public const string
            DEFAULT_ARBALEST_TAG        = "AKC";
        /// 

        Dictionary<int, LauncherSegment>launchers;
        List<IMyAirtightHangarDoor>     doors;
        List<IMyTextPanel>              screens;

        class LauncherSegment {
            IMyShipMergeBlock           merger;

            bool                        accelEnabled;

            List<IMyGravityGenerator>   accelerators;
            List<IMyShipWelder>         constructors;

            public LauncherSegment(IMyShipMergeBlock merger = null, List<IMyGravityGenerator> accelerators = null, List<IMyShipWelder> constructors = null) {
                this.merger = merger;
                if (accelerators != null) this.accelerators = accelerators; else this.accelerators = new List<IMyGravityGenerator>();
                if (constructors != null) this.constructors = constructors; else this.constructors = new List<IMyShipWelder>();
            }

            public void enableMerger(bool enable) {this.merger.Enabled = enable;}

            public void switchAccels() {
                this.accelEnabled = !this.accelEnabled;
                if (this.accelEnabled) {
                    foreach(IMyGravityGenerator accel in accelerators) {
                        accel.GravityAcceleration = 9.81f;
                        accel.FieldSize = new Vector3D(12.5d, 150d, 12.5d);
                    }
                }
                else {
                    foreach (IMyGravityGenerator accel in accelerators) {
                        accel.GravityAcceleration = 0f;
                        accel.FieldSize = new Vector3D(1d, 1d, 1d);
                    }
                }
            }

            public void enableAccels(bool enable) {
                if (this.accelEnabled != enable) switchAccels();
            }

        }

        public Program() {
            launchers = new Dictionary<int, LauncherSegment>();
            doors = new List<IMyAirtightHangarDoor>();
        }

        bool isOnSameGrid(IMyCubeBlock A, IMyCubeBlock B = null) {
            if (B == null) B = Me;

            if (A.CubeGrid.Equals(B.CubeGrid)) return true;

            return false;
        }

        void findLaunchers(){
            List<IMyShipMergeBlock> mergers = new List<IMyShipMergeBlock>();
            GridTerminalSystem.GetBlocksOfType(mergers);
            foreach(IMyShipMergeBlock merger in mergers) {
                if(isOnSameGrid(merger) && merger.CustomName.StartsWith("[" + DEFAULT_ARBALEST_TAG + "-")) {
                    int number=-1;
                    if(int.TryParse(merger.CustomName.Substring(2+DEFAULT_ARBALEST_TAG.Length), out number)) {
                    
                    }
                }
            }
        }

        bool findScreens() {
            screens = new List<IMyTextPanel>();
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyTextPanel screen in temp) {
                if (isOnSameGrid(screen) && screen.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG + "]")) screens.Add(screen);
            }
            if (screens.Count > 0) return true;
            return false;
        }

        void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();

            if (screens == null || screens.Count == 0)
                if (!findScreens()) return;

            foreach(IMyTextPanel screen in screens) {
                screen.WriteText(message, append);
            }
        }

        /*/
        public void Save() {

        }
        /**/

        public void Main(string argument, UpdateType updateSource) {

        }

    }

}
