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

        Color COLOR_BLACK = new Color(0, 0, 0);

        public const string
            DEFAULT_ARBALEST_TAG        = "AKC";
        /// 

        Dictionary<int, LauncherSegment>launchers;
        List<IMyAirtightHangarDoor>     doors;
        List<IMyTextPanel>              screens;

        List<Job> schedule      = new List<Job>();

        Color lastColor     =  new Color(0, 0, 0);

        public enum JobType {
            LOAD,       // MERGER ON , ACC OFF
            MIDSTATE,   // MERGER OFF, ACC OFF
            ACCELERATE, // MERGER OFF, ACC ON

            OPEN_DOOR,
            CLOSE_DOOR
        }

        class Job {
            public JobType  type;
            public bool     lnch;
            public int      TTJ ;
            public int      no  ;

            public Job(JobType type, int TTJ, int no=-1) {
                this.type   = type;
                this.TTJ    = TTJ;
                this.no     = no;
                if (no == -1)   lnch = false;
                else            lnch = true;
            }
        }

        class Register {
            static List<Job> schedule = new List<Job>();

            public static void Initialize() {
                schedule = new List<Job>();
            }

            public static void Add(Job job) {
                schedule.Add(job);
                if(schedule.Count>1) 
                    schedule = schedule.OrderBy(o => o.TTJ).ToList();
            }

            public static Job Tick() {
                if (schedule.Count <= 0) return null;

                for (int i = 0; i < schedule.Count; i++) schedule[i].TTJ--;

                if (schedule[0].TTJ <= 0) {
                    Job current = schedule[0];
                    schedule.RemoveAt(0);
                    return current;
                }

                return null;
            }
        }

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
                        accel.GravityAcceleration = 10f;
                        accel.FieldSize = new Vector3D(14d, 150d, 14d);
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
            Me.CustomData = "";
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            launchers = new Dictionary<int, LauncherSegment>();
            doors = new List<IMyAirtightHangarDoor>();
            FindLaunchers(); FindDoors();
            Register.Initialize();
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
                if (IsOnSameGrid(merge) && merge.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG)) output.Add(merge);
            }

            return output;
        }

        List<IMyGravityGenerator> GetAccelerators(int num) {
            List<IMyGravityGenerator>
                temp = new List<IMyGravityGenerator>(),
                output = new List<IMyGravityGenerator>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyGravityGenerator gravGen in temp) {
                if (IsOnSameGrid(gravGen) && gravGen.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG + "-" + num + "]")) output.Add(gravGen);
            }

            return output;
        }
        List<IMyShipWelder> GetConstructors(int num) {
            List<IMyShipWelder>
                temp = new List<IMyShipWelder>(),
                output = new List<IMyShipWelder>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyShipWelder welder in temp) {
                if (IsOnSameGrid(welder) && welder.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG + "-" + num + "]")) output.Add(welder);
            }

            return output;
        }

        void FindDoors() {
            List<IMyAirtightHangarDoor> 
                temp    = new List<IMyAirtightHangarDoor>();
                doors   = new List<IMyAirtightHangarDoor>();

            GridTerminalSystem.GetBlocksOfType(temp);

            foreach(IMyAirtightHangarDoor door in temp) {
                if(IsOnSameGrid(door) && door.CustomName.Contains("[" + DEFAULT_ARBALEST_TAG + "]")) {
                    doors.Add(door);
                }
            }
        }

        void FindLaunchers(){
            List<IMyShipMergeBlock>     mergers = GetMergers();
            List<IMyShipWelder>         construc;
            List<IMyGravityGenerator>   accel;
            foreach (IMyShipMergeBlock merger in mergers) {
                int     number=-1;
                if (int.TryParse(merger.CustomData, out number)) {
                    if (launchers.ContainsKey(number)) Log("There is more than one Launcher with number " + number);
                    else {
                        construc= GetConstructors(number);
                        accel   = GetAccelerators(number);

                        LauncherSegment temp = new LauncherSegment(merger, accel, construc);

                        launchers.Add(number, temp);
                        temp.EnableAccels(false);
                        //temp.EnableMerger(true);

                        Log("A new Launcher found: "+number + " " + construc.Count + " " + accel.Count, false);
                    }
                }
                else Log("There was a parsing error in 'findLaunchers' function: "+ merger.CustomData);
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

        const int STEP = 8;
        void SetNextColor() {
            int 
                R = lastColor.R > STEP ? (lastColor.R - STEP) : 0, 
                G = lastColor.G > STEP ? (lastColor.G - STEP) : 0, 
                B = lastColor.B > STEP ? (lastColor.B - STEP) : 0;

            ChangeScreenColor(new Color(R, G, B));
        }
        void ChangeScreenColor() { ChangeScreenColor(COLOR_BLACK); }
        void ChangeScreenColor(Color color) {
            if (screens == null || screens.Count == 0)
                if (!FindScreens()) return;

            foreach (IMyTextPanel screen in screens) {
                screen.BackgroundColor = color;
            }

            lastColor = color;
        }

        void Log(object input, bool error = true) {
            string message = input is string ? (string)input : input.ToString();

            if (error) ChangeScreenColor(Color.Red);

            Me.CustomData += message + "\n";
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
        public enum JobType {
            LOAD,       // MERGER ON , ACC OFF
            MIDSTATE,   // MERGER OFF, ACC OFF
            ACCELERATE, // MERGER OFF, ACC ON
        }
        /**/

        void DoTheJob(Job job) {
            if (job.lnch) {
                LauncherSegment launcher;
                if (!launchers.TryGetValue(job.no, out launcher)) return;
                switch (job.type) {
                    case JobType.LOAD:
                        launcher.EnableAccels(false);
                        launcher.EnableMerger(true);
                        break;

                    case JobType.MIDSTATE:
                        launcher.EnableAccels(true);
                        launcher.EnableMerger(true);
                        break;

                    case JobType.ACCELERATE:
                        launcher.EnableAccels(true);
                        launcher.EnableMerger(false);
                        break;
                }
            }
            else {
                switch (job.type) {
                    case JobType.OPEN_DOOR:
                        foreach(IMyAirtightHangarDoor door in doors) {
                            if( door.Status == DoorStatus.Closed || 
                                door.Status == DoorStatus.Closing ) 
                                door.OpenDoor();
                        }
                        break;

                    case JobType.CLOSE_DOOR:
                        foreach (IMyAirtightHangarDoor door in doors) {
                            if( door.Status == DoorStatus.Open || 
                                door.Status == DoorStatus.Opening ) 
                                door.CloseDoor();
                        }
                        break;
                }
            }
        }

        /*/
        LOAD
        MIDSTATE
        ACCELERATE
        OPEN_DOOR
        CLOSE_DOOR
        /**/

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0) {
                if (!lastColor.Equals(COLOR_BLACK)) SetNextColor();
                Job current = Register.Tick();
                if (current != null) { DoTheJob(current); }
            }
            else
            if ((updateSource & UpdateType.IGC) > 0) {

            }
            else {
                if (argument.Length > 0) {
                    string[] args = argument.ToLower().Split(' ');
                    switch (args[0]) {
                        case "open":
                            Register.Add(new Job(JobType.OPEN_DOOR, 1));
                            break;

                        case "close":
                            Register.Add(new Job(JobType.CLOSE_DOOR, 1));
                            break;

                        case "load":
                            if (args.Length > 1) {
                                int lnchNo;
                                if(int.TryParse(args[1], out lnchNo) && launchers.ContainsKey(lnchNo)) {
                                    Register.Add(new Job(JobType.LOAD, 1, lnchNo));
                                }
                            }
                            break;

                        case "mid":
                            if (args.Length > 1) {
                                int lnchNo;
                                if (int.TryParse(args[1], out lnchNo) && launchers.ContainsKey(lnchNo)) {
                                    Register.Add(new Job(JobType.MIDSTATE, 1, lnchNo));
                                }
                            }
                            break;

                        case "accel":
                            if (args.Length > 1) {
                                int lnchNo;
                                if (int.TryParse(args[1], out lnchNo) && launchers.ContainsKey(lnchNo)) {
                                    Register.Add(new Job(JobType.ACCELERATE, 1, lnchNo));
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}
