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

        const string  
            RadarCode   = "[RADAR]",
            missileTag = "MISSILE-CHN", 
            misCMDTag = "MISSILE_COMMAND-CHN";

        readonly IMyBroadcastListener
            misCMDListener;

        int     MAX_WAIT    = 900;
        const int   MAX_TSLB = 5;

        float   currExt, currRPM;

        Entry curTarget;

        List<Job> schedule = new List<Job>();

        bool DetectPlayers        ;
        bool DetectFloatingObjects;
        bool DetectSmallShips     ;
        bool DetectLargeShips     ;
        bool DetectStations       ;
        bool DetectSubgrids       ;
        bool DetectAsteroids      ;

        bool DetectOwner          ;
        bool DetectFriendly       ;
        bool DetectNeutral        ;
        bool DetectEnemy          ;

        List<IMySensorBlock> Radars;
        List<IMyMotorStator> RadRots;
        List<IMyTextPanel> Screens;

        public enum JobType {
            OpenDoor,
            Launch,
            CloseDoor
        }

        public class Job {
            public JobType  type;
            public int      misNo, // set if the job is allocated to a specyfic missile
                            TTJ; // "TicksToJob"


            public Job(JobType type, int TTJ, int misNo) {
                this.type   = type;
                this.TTJ    = TTJ;
                this.misNo  = misNo;
            }
        }

        public class Entry {
            public long id;
            public int timeNum;
            public Vector3D location;
            public MyDetectedEntityType type;
            public MyRelationsBetweenPlayerAndBlock relation;

            public void Increment() {this.timeNum++;}

            public Entry(MyDetectedEntityInfo entity) {
                this.timeNum    = 0;
                this.id         = entity.EntityId;
                this.location   = entity.Position;
                this.type       = entity.Type;
                this.relation   = entity.Relationship;
            }

            public Entry(Vector3D coords) : this(coords.X, coords.Y, coords.Z) { }
            public Entry(double X, double Y, double Z) {
                this.timeNum = 42044469;
                this.id = -1;
                this.location = new Vector3D(X, Y, Z);
                this.type = MyDetectedEntityType.LargeGrid;
                this.relation = MyRelationsBetweenPlayerAndBlock.Enemies;
            }

        }

        public class Register {
            static List<Entry> content = new List<Entry>();
            static IMyProgrammableBlock Me;
            static int    MAX_WAIT;

            public static void SetMe(IMyProgrammableBlock Me) { Register.Me = Me;  }
            public static void SetMax(int MAX) { Register.MAX_WAIT = MAX; }


            public static Entry Get(int index) {return content.Count > index ? content[index] : null;}

            public static void Add(MyDetectedEntityInfo entity) {
                int current = 0, target = -1;
                foreach (Entry ent in content) {
                    if (ent.id == entity.EntityId) {target = current; break;}
                    current++;
                }
                Entry temp = new Entry(entity);
                if (target == -1) {content.Add(temp);}
                else {content[target] = temp;}

                content = content.OrderByDescending(o => o.relation).ThenBy(o => GetDistance(o.location)).ToList();
            }

            public static void IncrementAll() {
                List<Entry> temp = new List<Entry>();
                foreach (Entry ent in content) {
                    ent.Increment();
                    if (ent.timeNum < MAX_WAIT) temp.Add(ent);
                }
                content = new List<Entry>(temp);
            }

            public static string PrintOut() {
                string output = "TARGETS:";

                for(int i = 0; i < content.Count; i++) {
                    output += "\n"+ ((i+1<10)? " "+(i+1):""+(i+1)) + ") " + content[i].relation.ToString().Substring(0,3).ToUpper() + " " + content[i].type.ToString().Substring(0, 5).ToUpper() + " " + Convert(content[i].location) + " " + String.Format("{0:0.#}",((float)content[i].timeNum/60f)) + "s";
                }

                return output;
            }

            static double GetDistance(Vector3D location) {
                return Vector3D.Subtract(Me.CubeGrid.GetPosition(), location).Length();
            }

            static string Convert(Vector3D location) {
                double temporal = GetDistance(location);
                string output;

                if (temporal >= 1000d) {
                    temporal /= 1000d;
                    output = String.Format("{0:0.##} KM", temporal);
                }
                else {
                    output = String.Format("{0:0.#} M", temporal);
                }

                return output;
            }
        }

        public void Save() {
            Storage = currExt + ";" + currRPM + ";" + 
            DetectPlayers + ";" + DetectFloatingObjects + ";" + 
            DetectSmallShips + ";" + DetectLargeShips + ";" + DetectStations + ";" + DetectSubgrids + ";" + DetectAsteroids + ";" + 
            DetectOwner + ";" + DetectFriendly + ";" + DetectNeutral + ";" + DetectEnemy;
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Register.SetMe(Me);
            SetMax(900);
            GetRadars();
            if (Storage.Length > 0) {
                try {
                    string[] args = Storage.Split(';');
                    int i = 0;
                    currExt                 = float.Parse(args[i++]);
                    currRPM                 = float.Parse(args[i++]);
                    DetectPlayers           = bool .Parse(args[i++]);
                    DetectFloatingObjects   = bool .Parse(args[i++]);
                    DetectSmallShips        = bool .Parse(args[i++]);
                    DetectLargeShips        = bool .Parse(args[i++]);
                    DetectStations          = bool .Parse(args[i++]);
                    DetectSubgrids          = bool .Parse(args[i++]);
                    DetectAsteroids         = bool .Parse(args[i++]);
                    DetectOwner             = bool .Parse(args[i++]);
                    DetectFriendly          = bool .Parse(args[i++]);
                    DetectNeutral           = bool .Parse(args[i++]);
                    DetectEnemy             = bool .Parse(args[i++]);
                }
                catch(Exception e) {
                    e.ToString();
                    currExt = 8000f;
                    currRPM = 3f;
                    DetectPlayers = false;
                    DetectFloatingObjects = false;
                    DetectSmallShips = true;
                    DetectLargeShips = true;
                    DetectStations = true;
                    DetectSubgrids = false;
                    DetectAsteroids = false;

                    DetectOwner = false;
                    DetectFriendly = false;
                    DetectNeutral = true;
                    DetectEnemy = true;
                }
                SetRadars(currExt, currRPM);
            }
            else SetRadars(8000f,3f);
            GetScreens(); 
            misCMDListener = IGC.RegisterBroadcastListener(misCMDTag);
            misCMDListener.SetMessageCallback();
        }

        public void SendCoords(Vector3D vec) { SendCoords(vec.X, vec.Y, vec.Z); }
        public void SendCoords(Vector3D vec, Vector3D vec2) { SendCoords(vec.X, vec.Y, vec.Z, vec2.X, vec2.Y, vec2.Z); }

        public void SendCoords(double X1, double Y1, double Z1, double X2=0, double Y2=0, double Z2=0) { IGC.SendBroadcastMessage(missileTag, "TARSET;" + X1 + ";" + Y1 + ";" + Z1 + ";" + X2 + ";" + Y2 + ";" + Z2); }

        bool IsOnThisGrid(IMyCubeBlock block) {
            if (block.CubeGrid.Equals(Me.CubeGrid)) 
                return true;
            else 
                return false;
        }

        void SetMax(int MAX) {
            MAX_WAIT = MAX;
            Register.SetMax(MAX_WAIT);
        }

        int TSLB = 0;

        void Detect() {
            bool AllRight = true;
            TSLB++;
            List<MyDetectedEntityInfo> Detected;
            foreach (IMySensorBlock rad in Radars) {
                if (rad != null) {
                    Detected = new List<MyDetectedEntityInfo>();
                    rad.DetectedEntities(Detected);
                    foreach (MyDetectedEntityInfo entity in Detected) {
                        Register.Add(entity);
                        if (curTarget != null) {
                            if (entity.EntityId.Equals(curTarget.id) && curTarget.timeNum!= 42044469) {
                                //TODO: BE VOCAL ABOUT IT
                                curTarget = new Entry(entity);
                                if (TSLB > (MAX_TSLB-1)) {
                                    TSLB = 0;
                                    SendCoords(entity.Position,entity.Velocity);
                                }
                            }
                        }
                    }
                }
                else AllRight = false;
            }

            if (!AllRight) GetRadars();
        }

        void SetRadars() {SetRadars(currExt, currRPM);}

        void SetRadRPM(float tarRPM) {
            float detExt = 8000f;
            if (Radars.Count > 0) { detExt = Radars[0].LeftExtend; }
            else { GetRadars(); }
            SetRadars(detExt, tarRPM);
        }

        void SetRadExt(float detExt) {
            float tarRPM = 3f;
            if (RadRots.Count > 0) {
                tarRPM = RadRots[0].TargetVelocityRPM;
                tarRPM = tarRPM > 0 ? tarRPM : -tarRPM;
            }
            else { GetRadars(); }

            SetRadars(detExt, tarRPM);
        }

        void SetRadars(float detExt, float tarRPM) {
            if (detExt <= 0 || detExt > 8000) detExt = 8000;
            if (tarRPM <= 0 || tarRPM > 30) tarRPM = 30;

            foreach (IMyMotorStator rot in RadRots) {
                rot.TargetVelocityRPM = rot.TargetVelocityRPM > 0 ? tarRPM : -tarRPM;
                rot.Enabled = true;
            }

            float detThcc = (5f * (float)(Math.PI) * detExt * tarRPM / 7200f);

            foreach (IMySensorBlock rad in Radars) {
                rad.BackExtend = detExt;
                rad.FrontExtend = detExt;
                rad.LeftExtend = detExt;
                rad.RightExtend = detExt;
                rad.TopExtend = detThcc;
                rad.BottomExtend = detThcc;

                rad.DetectPlayers           = DetectPlayers;
                rad.DetectFloatingObjects   = DetectFloatingObjects;
                rad.DetectSmallShips        = DetectSmallShips;
                rad.DetectLargeShips        = DetectLargeShips;
                rad.DetectStations          = DetectStations;
                rad.DetectSubgrids          = DetectSubgrids;
                rad.DetectAsteroids         = DetectAsteroids;


                rad.DetectOwner             = DetectOwner;
                rad.DetectFriendly          = DetectFriendly;
                rad.DetectNeutral           = DetectNeutral;
                rad.DetectEnemy             = DetectEnemy;
            }
            currExt = detExt;
            currRPM = tarRPM;

            float newMax = 60f / tarRPM; /// w sekundach
            if (newMax < 10) newMax = 10; 

            SetMax((int)newMax * 60); /// w tickach
        }

        void GetRadars() {
            List<IMySensorBlock> temp 
                    = new List<IMySensorBlock>();
            Radars  = new List<IMySensorBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMySensorBlock rad in temp) {
                if (
                    rad.CustomName.Contains(RadarCode)
                    ) Radars.Add(rad);
            }

            List<IMyMotorStator> temp2
                    = new List<IMyMotorStator>();
            RadRots = new List<IMyMotorStator>();

            GridTerminalSystem.GetBlocksOfType(temp2);
            foreach (IMyMotorStator rad in temp2) {
                if (
                    rad.CustomName.Contains(RadarCode)
                    ) RadRots.Add(rad);
            }
        }

        void GetScreens() {
            List<IMyTextPanel> temp
                    = new List<IMyTextPanel>();
            Screens = new List<IMyTextPanel>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyTextPanel scr in temp) {
                if (IsOnThisGrid(scr) && scr.CustomName.Contains(RadarCode)) Screens.Add(scr);
            }
        }

        string PrintOut() {
            return 
                "Radar no: " + Radars.Count + "   Screen no: " + Screens.Count +"\n"+
                "Sensor range: "+currExt+" m Buoy RPM: "+currRPM;
        }

        void Output(object input, bool append = false) {
            bool AllRight = true;
            string message = input is string ? (string)input : input.ToString();
            foreach(IMyTextPanel screen in Screens) {
                if (screen != null) {
                    screen.ContentType = ContentType.TEXT_AND_IMAGE;
                    screen.WriteText(message,append);
                }
                else AllRight = false;
            }
            //Echo(message);
            if (!AllRight) GetScreens();
        }

        void PrepareForLaunch(int launchSize = -1) {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            int maxMssle = 0;
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("MISSILE-")) {
                    string toParse = pb.CustomName.Substring(8);
                    int missNo;
                    try { missNo = int.Parse(toParse); }
                    catch (Exception e) { missNo = 0; e.ToString(); }
                    maxMssle = maxMssle > missNo ? maxMssle : missNo;
                }
            }

            if (launchSize > 0 && launchSize < maxMssle) maxMssle = launchSize;

            for(int i = 1; i<=maxMssle; i++) {
                AddAJob(new Job(JobType.OpenDoor, i * 10, i));
                AddAJob(new Job(JobType.Launch,200 + i * 10, i));
                AddAJob(new Job(JobType.CloseDoor, 700 + i * 10, i));
            }
        }

        void Abort(bool selfDestruct = false) {
            schedule.Clear();
            curTarget = null;
        }

        void PrepareGPSStrike(double X, double Y, double Z) {
            curTarget = new Entry(X, Y, Z);
            PrepareForLaunch();
        }

        void AddAJob(Job job) {
            schedule.Add(job);
            schedule = schedule.OrderBy(o => o.TTJ).ToList();
        }

        void DoYourJob() {
            if (schedule.Count > 0 && schedule[0].TTJ <= 0) {
                Job curr = schedule[0];
                schedule.RemoveAt(0);
                IMyAirtightHangarDoor siloDoor = GridTerminalSystem.GetBlockWithName("Silo Door " + curr.misNo) as IMyAirtightHangarDoor;
                switch (curr.type) {
                    case JobType.OpenDoor:
                        if (siloDoor != null) siloDoor.OpenDoor();
                        else Abort();
                        break;

                    case JobType.Launch:
                        IMyProgrammableBlock missile = GridTerminalSystem.GetBlockWithName("MISSILE-" + curr.misNo) as IMyProgrammableBlock;
                        if (curTarget == null || missile == null || siloDoor == null) {
                            string message = "ABORTING LAUNCH: TAR:" + (curTarget == null) + " MSSL:" + (missile == null) + " DR:" + (siloDoor == null);
                            Output(message, true); Echo(message);
                            return;
                        }
                        missile.Enabled = false;
                        missile.Enabled = true;
                        missile.TryRun("prep " + curTarget.location.X + " " + curTarget.location.Y + " " + curTarget.location.Z);
                        break;

                    case JobType.CloseDoor:
                        siloDoor.CloseDoor();
                        break;
                }
            }
        }

        void IncrementAll() {
            Register.IncrementAll();
            foreach (Job job in schedule) job.TTJ--;
        }

        //try { } catch(Exception e) { e.ToString(); return; }
        public void Main(string argument, UpdateType updateSource) {
            if((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0) {
                Detect();
                Output(PrintOut() + "\n" + Register.PrintOut());
                DoYourJob();
                IncrementAll();
            }
            else
            if ((updateSource & UpdateType.IGC) > 0) {
                if (misCMDListener != null && misCMDListener.HasPendingMessage) {
                    MyIGCMessage message = misCMDListener.AcceptMessage();
                    if (message.Tag.Equals(misCMDTag)) {
                        string data = (string)message.Data;
                        string[] bits = data.Split(';');

                        if (bits.Length <= 0) return;
                        switch (bits[0].ToUpper()) {
                            case "SALVO":
                                if (bits.Length > 3) {
                                    curTarget = new Entry(float.Parse(bits[1]), float.Parse(bits[2]), float.Parse(bits[3]));
                                    PrepareForLaunch();
                                }
                                break;

                            case "ABORT":
                                IGC.SendBroadcastMessage(missileTag, "ABORT");
                                break;
                        }
                    }
                }
                return;
            }
            else {
                if (argument.Length > 0) {
                    string[] args = argument.ToLower().Split(' ');
                    float detExt = 8000, tarRPM = 3;
                    switch (args[0]) {
                        case "ext":
                        case "range":
                            try { detExt = args.Length > 1 ? float.Parse(args[1]) : 8000f; } catch(Exception e) { e.ToString(); }
                            Echo("set: " + detExt + " " + tarRPM);
                            SetRadExt(detExt);
                            break;

                        case "rpm":
                            try { tarRPM = args.Length > 1 ? float.Parse(args[1]) : 3f; } catch(Exception e) { e.ToString(); }
                            Echo("set: " + detExt + " " + tarRPM);
                            SetRadRPM(tarRPM);
                            break;

                        case "rad":
                        case "set":
                            try { detExt = args.Length > 1 ? float.Parse(args[1]) : 8000f; } catch (Exception e) { e.ToString(); }
                            try { tarRPM = args.Length > 2 ? float.Parse(args[2]) : 3f; }   catch (Exception e) { e.ToString(); }
                            Echo("set: " + detExt + " " + tarRPM);
                            SetRadars(detExt, tarRPM);
                            break;

                        case "gpsstrike":
                        case "gpstrike":
                        case "gps":
                            double X, Y, Z;
                            try { X = double.Parse(args[1]); } catch (Exception e) { e.ToString(); return; }
                            try { Y = double.Parse(args[2]); } catch (Exception e) { e.ToString(); return; }
                            try { Z = double.Parse(args[3]); } catch (Exception e) { e.ToString(); return; }

                            PrepareGPSStrike(X, Y, Z);
                            break;

                        case "attack":
                        case "fire":
                            if (args.Length < 2) {
                                if (curTarget != null) PrepareForLaunch();
                            }
                            else {
                                int index, magnitude = -1;
                                try { index = int.Parse(args[1]) - 1; } catch (Exception e) { e.ToString(); return; }
                                Entry target = Register.Get(index); if (target == null) return;
                                curTarget = target;

                                if (args.Length > 2) {
                                    try { magnitude = int.Parse(args[2]); } catch (Exception e) { e.ToString(); magnitude = -1; }
                                }
                                PrepareForLaunch(magnitude);
                            }

                            break;

                        case "abort":
                            Abort(); 
                            IGC.SendBroadcastMessage(missileTag, "ABORT");
                            break;

                        case "detect":
                            if (args.Length > 1) {
                                switch (args[1]) {
                                    case "players":
                                    case "characters":
                                        DetectPlayers = true;
                                        break;

                                    case "objects":
                                    case "garbage":
                                        DetectFloatingObjects = true;
                                        break;

                                    case "smallships":
                                    case "small":
                                        DetectSmallShips = true;
                                        break;

                                    case "largeships":
                                    case "large":
                                        DetectLargeShips = true;
                                        break;

                                    case "stations":
                                    case "static":
                                        DetectStations = true;
                                        break;

                                    case "sub":
                                    case "subgrids":
                                        DetectSubgrids = true;
                                        break;

                                    case "asteroids":
                                    case "comets":
                                        Output("\nChyba Cię coś generalnie popierdoliło, byku XD", true);
                                        //DetectAsteroids = true;
                                        break;

                                    case "own":
                                    case "owner":
                                    case "my":
                                    case "mine":
                                        DetectOwner = true;
                                        break;

                                    case "friendly":
                                    case "fac":
                                    case "green":
                                    case "allies":
                                    case "ally":
                                        DetectFriendly = true;
                                        break;

                                    case "noo":
                                    case "neutral":
                                        DetectNeutral = true;
                                        break;

                                    case "enemy":
                                    case "ene":
                                    case "red":
                                        DetectEnemy = true;
                                        break;
                                        
                                    case "default":
                                        DetectPlayers = false;
                                        DetectFloatingObjects = false;
                                        DetectSmallShips = true;
                                        DetectLargeShips = true;
                                        DetectStations = true;
                                        DetectSubgrids = false;
                                        DetectAsteroids = false;

                                        DetectOwner = false;
                                        DetectFriendly = false;
                                        DetectNeutral = true;
                                        DetectEnemy = true;
                                        break;
                                }
                                SetRadars();
                            }
                            break;

                        case "ignore":
                            if (args.Length > 1) {
                                switch (args[1]) {
                                    case "players":
                                    case "characters":
                                        DetectPlayers = false;
                                        break;

                                    case "objects":
                                    case "garbage":
                                        DetectFloatingObjects = false;
                                        break;

                                    case "smallships":
                                    case "small":
                                        DetectSmallShips = false;
                                        break;

                                    case "largeships":
                                    case "large":
                                        DetectLargeShips = false;
                                        break;

                                    case "stations":
                                    case "static":
                                        DetectStations = false;
                                        break;

                                    case "sub":
                                    case "subgrids":
                                        DetectSubgrids = false;
                                        break;

                                    case "asteroids":
                                    case "comets":
                                        DetectAsteroids = false;
                                        break;

                                    case "own":
                                    case "owner":
                                    case "my":
                                    case "mine":
                                        DetectOwner = false;
                                        break;

                                    case "friendly":
                                    case "fac":
                                    case "green":
                                    case "allies":
                                    case "ally":
                                        DetectFriendly = false;
                                        break;

                                    case "noo":
                                    case "neutral":
                                        DetectNeutral = false;
                                        break;

                                    case "enemy":
                                    case "ene":
                                    case "red":
                                        DetectEnemy = false;
                                        break;

                                    case "default":
                                        DetectPlayers = false;
                                        DetectFloatingObjects = false;
                                        DetectSmallShips = true;
                                        DetectLargeShips = true;
                                        DetectStations = true;
                                        DetectSubgrids = false;
                                        DetectAsteroids = false;

                                        DetectOwner = false;
                                        DetectFriendly = false;
                                        DetectNeutral = true;
                                        DetectEnemy = true;
                                        break;
                                }
                                SetRadars();
                            }
                            break;
                    }
                }
            }
        }
    }
}
