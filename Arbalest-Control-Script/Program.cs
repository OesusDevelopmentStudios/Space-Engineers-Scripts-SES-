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

            public bool                 busy;

            bool                        accelEnabled;

            List<IMyGravityGenerator>   accelerators;
            List<IMyShipWelder>         constructors;

            public LauncherSegment(IMyShipMergeBlock merger = null, List<IMyGravityGenerator> accelerators = null, List<IMyShipWelder> constructors = null) {
                this.merger = merger;
                this.accelEnabled = true;
                this.busy   = false;
                if (accelerators != null) this.accelerators = accelerators; else this.accelerators = new List<IMyGravityGenerator>();
                if (constructors != null) this.constructors = constructors; else this.constructors = new List<IMyShipWelder>();
            }

            public void EnableMerger(bool enable) {this.merger.Enabled = enable;}

            public void EnableAccels(bool enable) {
                if (this.accelEnabled != enable) SwitchAccels();
            }

            public void SwitchAccels() {
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

            public void SetAccels(List<IMyGravityGenerator> accelerators) {
                this.accelerators = new List<IMyGravityGenerator>();
                this.accelerators.AddList(accelerators);
                this.EnableAccels(true);
                this.EnableAccels(false);
            }

            public void SetConstructors(List<IMyShipWelder> constructors) {
                this.constructors = new List<IMyShipWelder>();
                this.constructors.AddList(constructors);
            }

        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            launchers = new Dictionary<int, LauncherSegment>();
            doors = new List<IMyAirtightHangarDoor>();
        }

        bool IsOnSameGrid(IMyCubeBlock A, IMyCubeBlock B = null) {
            if (B == null) B = Me;

            if (A.CubeGrid.Equals(B.CubeGrid)) return true;

            return false;
        }

        List<IMyShipMergeBlock> GetMergers() {
            List<IMyShipMergeBlock> 
                temp    = new List<IMyShipMergeBlock>(), 
                output  = new List<IMyShipMergeBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach(IMyShipMergeBlock merge in temp) {
                if (IsOnSameGrid(merge) && merge.CustomName.StartsWith("[" + DEFAULT_ARBALEST_TAG + "-")) output.Add(merge);
            }

            return output;
        }

        List<IMyGravityGenerator> GetAccelerators(int num) {
            List<IMyGravityGenerator>
                temp = new List<IMyGravityGenerator>(),
                output = new List<IMyGravityGenerator>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyGravityGenerator gravGen in temp) {
                if (IsOnSameGrid(gravGen) && gravGen.CustomName.StartsWith("[" + DEFAULT_ARBALEST_TAG + "-" + num + "]")) output.Add(gravGen);
            }

            return output;
        }
        List<IMyShipWelder> GetConstructors(int num) {
            List<IMyShipWelder>
                temp = new List<IMyShipWelder>(),
                output = new List<IMyShipWelder>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyShipWelder welder in temp) {
                if (IsOnSameGrid(welder) && welder.CustomName.StartsWith("[" + DEFAULT_ARBALEST_TAG + "-" + num + "]")) output.Add(welder);
            }

            return output;
        }

        void FindLaunchers(){
            List<IMyShipMergeBlock> mergers = GetMergers();
            List<IMyShipWelder>    construc;
            List<IMyGravityGenerator> accel;
            foreach (IMyShipMergeBlock merger in mergers) {
                int number=-1;
                if (int.TryParse(merger.CustomName.Substring(2 + DEFAULT_ARBALEST_TAG.Length), out number)) {
                    if (launchers.ContainsKey(number)) Log("There is more than one Launcher with number " + number);
                    else {
                        construc= GetConstructors(number);
                        accel   = GetAccelerators(number);

                        launchers.Add(number,new LauncherSegment(merger,accel,construc));
                    }
                }
                else Log("There was a parsing error in 'findLaunchers' function");
            }
        }

        bool FindScreens() {
            screens = new List<IMyTextPanel>();
            List<IMyTextPanel> temp = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyTextPanel screen in temp) {
                if (IsOnSameGrid(screen) && screen.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG + "]")) screens.Add(screen);
            }
            if (screens.Count > 0) return true;
            return false;
        }

        void ChangeScreenColor() { ChangeScreenColor(Color.Black); }
        void ChangeScreenColor(Color color) {
            if (screens == null || screens.Count == 0)
                if (!FindScreens()) return;

            foreach (IMyTextPanel screen in screens) {
                screen.BackgroundColor = color;
            }
        }

        void Log(object input, bool error = true) {
            string message = input is string ? (string)input : input.ToString();

            if (error) ChangeScreenColor(Color.Red);

            Me.CustomData += "\n" + message;
        }

        void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();

            if (screens == null || screens.Count == 0)
                if (!FindScreens()) return;

            foreach(IMyTextPanel screen in screens) {
                screen.WriteText(message, append);
                screen.ContentType = ContentType.TEXT_AND_IMAGE;
                ChangeScreenColor();
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
