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
            CONSTRUCTORS_NOMINAL_NUMBER = 4,
            LAUNCH_SPACING = 3, ///ticks between launches in a salvo (6 being one second, apparently)
            
            MAINTEANCE_FREQUENCY = 60;

        Color COLOR_BLACK = new Color(0, 0, 0);

        public const string
            DEFAULT_ARBALEST_TAG        = "AKC";
        /// 

        Dictionary<int, LauncherSegment>launchers;
        List<IMyAirtightHangarDoor>     doors;
        List<IMyTextPanel>              screens;

        List<Job> schedule      = new List<Job>();

        Color lastColor     =  new Color(0, 0, 0);

        int     DECOLOR_STEP = 8,
                MAINTEANCE_NUM = 0;

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

            public Job(JobType type, int TTJ=1, int no=-1) {
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

            public static int GetSize() {return schedule.Count;}

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

            public int AccelCount() { return this.accelerators.Count; }

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

            public int ConstrCount() { return this.constructors.Count; }

            public void SetConstructors(List<IMyShipWelder> constructors) {
                this.constructors = new List<IMyShipWelder>();
                this.constructors.AddList(constructors);
            }
            /**
            public string GenerateDiagnostics() {
                string output = "------------------------\nLauncher Diagnostics:";

                for(int i=0; i<accelerators.Count; i++) {
                    output += "\nAccelerator "+(i+1)+" - ("+accelerators[i].CustomName+") Functional: "+ accelerators[i].IsFunctional + " Enabled: "+ accelerators[i].Enabled;
                }

                output += "\n";

                for (int i = 0; i < constructors.Count; i++) {
                    output += "\nConstructor " + (i + 1) + " - (" + constructors[i].CustomName + ") Functional: " + constructors[i].IsFunctional + " Enabled: " + constructors[i].Enabled;
                }


                return output+"\n------------------------";
            }
            /**/

        }

        public Program() {
            Me.CustomData = "";
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            launchers = new Dictionary<int, LauncherSegment>();
            doors = new List<IMyAirtightHangarDoor>();
            FindLaunchers(); FindDoors();
            Register.Initialize();
        }
        /**
        int GetDoorStatus() { /// it returns the number of ticks until the missile is o.k. to launch
            float openness = 1f;
            foreach(IMyAirtightHangarDoor door in doors) {
                if (door != null) {

                }
            }

        }
        /**/

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
                    if (launchers.ContainsKey(number)) Error("There is more than one Launcher with number " + number);
                    else {
                        construc= GetConstructors(number);
                        accel   = GetAccelerators(number);

                        LauncherSegment temp = new LauncherSegment(merger, accel, construc);

                        launchers.Add(number, temp);
                        temp.EnableAccels(false);
                        //temp.EnableMerger(true);

                        Log("A new Launcher found: "+number + " " + construc.Count + " " + accel.Count,false);
                    }
                }
                else Error("There was a parsing error in 'findLaunchers' function: "+ merger.CustomData);
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

        void SetNextColor() {
            int 
                R = lastColor.R > DECOLOR_STEP ? (lastColor.R - DECOLOR_STEP) : 0, 
                G = lastColor.G > DECOLOR_STEP ? (lastColor.G - DECOLOR_STEP) : 0, 
                B = lastColor.B > DECOLOR_STEP ? (lastColor.B - DECOLOR_STEP) : 0;

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

        void CheckAvailability() {
            LauncherSegment launcher;
            foreach (int key in launchers.Keys) {
                if(launchers.TryGetValue(key,out launcher)) {
                    launcher.SetAccels(GetAccelerators(key));
                    launcher.SetConstructors(GetConstructors(key));
                }
            }
        }

        void Error(object input) {
            string message = input is string ? (string)input : input.ToString();

            ChangeScreenColor(Color.Red);

            DECOLOR_STEP = 8;

            Me.CustomData += message + "\n";
        }

        void Log(object input, bool colorIt = true) {
            string message = input is string ? (string)input : input.ToString();

            if (colorIt) {
                ChangeScreenColor(Color.Yellow);
                DECOLOR_STEP = 16;
            }

            Me.CustomData += message + "\n";
        }

        string GenerateStatus() {
            string output = "";
            List<int> keys = new List<int>();

            foreach(int key in launchers.Keys) {
                keys.Add(key);
            }
            keys = keys.OrderBy(o => o).ToList();

            for (int i = 0; i < keys.Count; i++) {
                LauncherSegment launcher;
                if (launchers.TryGetValue(keys[i], out launcher)) {

                    if (i!=0) { output += "\n\n"; }

                    output += 
                        "Launcher " + keys[i] + " - "+ (launcher.busy? "BUSY":"STDBY") + 
                        (launcher.ConstrCount() == CONSTRUCTORS_NOMINAL_NUMBER ? "" : ("\nCONSTR: " + launcher.ConstrCount() + "/" + CONSTRUCTORS_NOMINAL_NUMBER)) +
                        (launcher.AccelCount()  == ACCELERATORS_NOMINAL_NUMBER ? "" : ("\nACCEL: " +  launcher.AccelCount()  + "/" + ACCELERATORS_NOMINAL_NUMBER));
                }
            }


            return output;
        }

        int GetTimeTillFine() {
            float openness = 1f;
            FindDoors();
            foreach (IMyAirtightHangarDoor door in doors) {
                if (!door.IsFunctional) {
                    Error("Error: One or more of the doors is not functional, which would make launch risky if not impossible.");
                    Output("UNABLE TO COMPLY");
                    return -1;
                }
                if(door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing) {
                    door.OpenDoor();
                }
                if(door.Status != DoorStatus.Open) {if (openness > door.OpenRatio) openness = door.OpenRatio;}
            }
            if (openness == 0) return 36;
            else 
            if (openness < 0.5f) {
                int TTO;

                TTO = (int)(((0.5f - openness) / 0.5f) * 36);

                return TTO;
            }

            return 0;
        }

        void PrepareSalvo() {
            int timeTillLaunch = GetTimeTillFine(); if (timeTillLaunch < 0) return;

            List<int> keys = new List<int>();

            foreach (int key in launchers.Keys) {
                keys.Add(key);
            }
            keys = keys.OrderBy(o => o).ToList();

            for (int i=0; i<keys.Count; i++) {
                ScheduleLaunch(keys[i], timeTillLaunch + (i * LAUNCH_SPACING));
            }
        }

        void PrepareLaunch(int key) {
            int timeTillLaunch = GetTimeTillFine(); if (timeTillLaunch < 0) return;

            ScheduleLaunch(key, timeTillLaunch);
        }

        void ScheduleLaunch(int key, int TTL = 0) {
            LauncherSegment launcher;
            if (launchers.TryGetValue(key,out launcher)) {
                if (launcher.busy) {
                    Error("Error: One or more launchers was already on the Register.");
                    return;
                }
                launcher.busy = true;

                Register.Add(new Job(JobType.ACCELERATE, TTL, key));
                Register.Add(new Job(JobType.LOAD, TTL+24, key));

            }
            else Log("Error: A launcher with a code '" + key + "' does not exist.");
        }

        void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();

            if (screens == null || screens.Count == 0)
                if (!FindScreens()) return;

            foreach(IMyTextPanel screen in screens) {
                screen.WriteText(message, append);
                screen.ContentType = ContentType.TEXT_AND_IMAGE;
            }
        }

        void DoTheJob(Job job) {
            if (job.lnch) {
                LauncherSegment launcher;
                if (!launchers.TryGetValue(job.no, out launcher)) return;
                switch (job.type) {
                    case JobType.LOAD:
                        launcher.EnableAccels(false);
                        launcher.EnableMerger(true);
                        launcher.busy = false;
                        break;

                    case JobType.MIDSTATE:
                        launcher.EnableAccels(false);
                        launcher.EnableMerger(false);
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

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0) {
                if (MAINTEANCE_NUM++ >= MAINTEANCE_FREQUENCY) {
                    MAINTEANCE_NUM = 0;
                    CheckAvailability();
                }
                if (!lastColor.Equals(COLOR_BLACK)) SetNextColor();
                Output(GenerateStatus());
                Job current = Register.Tick();
                if (current != null) { DoTheJob(current); }
                if (Register.GetSize() <= 0) {
                    DoTheJob(new Job(JobType.CLOSE_DOOR));
                }
            }
            else
            if ((updateSource & UpdateType.IGC) > 0) {

            }
            else {
                if (argument.Length > 0) {
                    string[] args = argument.ToLower().Split(' ');
                    switch (args[0]) {
                        case "open":
                            Register.Add(new Job(JobType.OPEN_DOOR));
                            break;

                        case "close":
                            Register.Add(new Job(JobType.CLOSE_DOOR));
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

                        case "fire":
                            if (args.Length > 1) {
                                int lnchNo;
                                if (int.TryParse(args[1], out lnchNo)) {
                                    PrepareLaunch(lnchNo);
                                }
                            }
                            else PrepareSalvo();
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
