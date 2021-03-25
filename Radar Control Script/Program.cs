﻿using Sandbox.Game.EntityComponents;
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
using VRageRender.Utils;
using System.Diagnostics;

namespace IngameScript {
    partial class Program : MyGridProgram {

        const string
            RadarCode = "[RADAR]",
            MY_PREFIX = "RADAR",
            missileTag = "MISSILE-CHN",
            misCMDTag = "MISSILE_COMMAND-CHN";

        readonly IMyBroadcastListener
            misCMDListener;

        int MAX_WAIT = 900;

        float currExt, currRPM;

        Entry curTarget;

        List<Job> schedule = new List<Job>();

        bool DetectPlayers;
        bool DetectFloatingObjects;
        bool DetectSmallShips;
        bool DetectLargeShips;
        bool DetectStations;
        bool DetectSubgrids;
        bool DetectAsteroids;

        bool DetectOwner;
        bool DetectFriendly;
        bool DetectNeutral;
        bool DetectEnemy;

        List<IMySensorBlock> Radars;
        List<IMyMotorStator> RadRots;
        List<IMyTextPanel> Screens;

        static IMyShipController Ship_Controller;

        bool AEGIS;
        const string TURRET_CONTROLLER_SCRIPT_NAME = "AEGIS";
        IMyProgrammableBlock Turret_Controller;

        public enum JobType {
            OpenDoor,
            Launch,
            CloseDoor
        }

        public class Job {
            public JobType type;
            public string code;
            public bool anti;
            public int misNo, // set if the job is allocated to a specific missile
                    TTJ; // "TicksToJob"


            public Job(JobType type, int TTJ, int misNo, bool anti = false, string code = "") {
                this.type = type;
                this.TTJ = TTJ;
                this.misNo = misNo;
                this.anti = anti;
                this.code = code;
            }
        }

        public class Entry {
            public long id;
            public int timeNum;
            public int TSB;
            public double threat;
            public Vector3D location;
            public Vector3D velocity;
            public MyDetectedEntityType type;
            public MyRelationsBetweenPlayerAndBlock relation;

            public void Increment() {
                this.timeNum++;
                this.TSB++;
            }

            public Entry(MyDetectedEntityInfo entity) {
                this.timeNum = 0;
                this.TSB = 0;
                this.id = entity.EntityId;
                this.location = entity.Position;
                this.velocity = entity.Velocity;
                this.type = entity.Type;
                this.relation = entity.Relationship;
            }

            public void Update(MyDetectedEntityInfo entity) {
                this.location = entity.Position;
                this.velocity = entity.Velocity;
            }

            public bool ShouldBC() {
                if (this.TSB > 1) {
                    this.TSB = 0;
                    return true;
                }
                return false;
            }

            public string Stringify() {
                return 
                    String.Format("{0:0.}", Math.Round(location.X * 10)) + ";" + 
                    String.Format("{0:0.}", Math.Round(location.Y * 10)) + ";" + 
                    String.Format("{0:0.}", Math.Round(location.Z * 10)) + ";" + 

                    String.Format("{0:0.}", Math.Round(velocity.X * 10)) + ";" + 
                    String.Format("{0:0.}", Math.Round(velocity.Y * 10)) + ";" + 
                    String.Format("{0:0.}", Math.Round(velocity.Z * 10));
            }

            public Entry(Vector3D coords) : this(coords.X, coords.Y, coords.Z) { }
            public Entry(double X, double Y, double Z) {
                this.timeNum = 42044469;
                this.TSB = 0;
                this.id = -1;
                this.location = new Vector3D(X, Y, Z);
                this.velocity = new Vector3D(0, 0, 0);
                this.type = MyDetectedEntityType.LargeGrid;
                this.relation = MyRelationsBetweenPlayerAndBlock.Enemies;
            }

        }

        public class Register {
            static List<Entry> content = new List<Entry>();
            static IMyProgrammableBlock Me;
            static int MAX_WAIT;

            public static void SetMe(IMyProgrammableBlock Me) { Register.Me = Me; }
            public static void SetMax(int MAX) { Register.MAX_WAIT = MAX; }

            public static Entry Get(int index) { return content.Count > index ? content[index] : null; }

            public static void Add(MyDetectedEntityInfo entity) {
                int current = 0, target = -1;
                foreach (Entry ent in content) {
                    if (ent.id == entity.EntityId) { target = current; break; }
                    current++;
                }
                Entry temp = new Entry(entity);
                if (target == -1) { content.Add(temp); }
                else { content[target] = temp; }

                content = content.OrderByDescending(o => o.relation).ThenBy(o => GetDistance(o.location)).ToList();
            }

            public static List<Entry> GetRefList() {
                List<Entry> output = new List<Entry>(Register.content);
                return output;
            }

            public static void IncrementAll() {
                List<Entry> temp = new List<Entry>();
                foreach (Entry ent in content) {
                    ent.Increment();
                    if (ent.timeNum < MAX_WAIT) {
                        temp.Add(ent);
                    }
                }
                content = new List<Entry>(temp);
            }

            public static string PrintOut() {
                string output = "TARGETS:";

                Vector3D
                    position = Ship_Controller.GetPosition();

                for (int i = 0; i < content.Count; i++) {
                    output += "\n" + ((i + 1 < 10) ? " " + (i + 1) : "" + (i + 1)) + ") " + RelationToAbbreviation(content[i].relation) + " " + TypeToLetter(content[i].type) + " " + new Bearing(position, Ship_Controller.WorldMatrix, content[i].location).ToString() + " " + Convert(content[i].location);
                }

                return output;
            }

            static double GetDistance(Vector3D location) {
                return Vector3D.Subtract(Me.GetPosition(), location).Length();
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

        public class Bearing {
            private double yaw, pitch;

            public Bearing(double yaw, double pitch) {
                this.yaw = yaw;
                this.pitch = pitch;
            }

            public Bearing(Vector3D position, MatrixD matrix, Vector3D target) {
                // "forward" and "right" are vectors relative to the starship. in order for our calculations to work, we need to create a relative position of the thing we are calculating bearing for
                target = Vector3D.Subtract(target, position);
                Vector3D X = new Vector3D(matrix.Forward.Y * matrix.Right.Z - matrix.Forward.Z * matrix.Right.Y, matrix.Forward.Z * matrix.Right.X - matrix.Forward.X * matrix.Right.Z, matrix.Forward.X * matrix.Right.Y - matrix.Forward.Y * matrix.Right.X);
                double t = -1*(X.X*target.X+X.Y*target.Y+X.Z*target.Z)/(X.X*X.X+X.Y*X.Y+X.Z*X.Z);
                Vector3D pointOnShipsPlane = new Vector3D((X.X*t)+target.X,(X.Y*t)+target.Y,(X.Z*t)+target.Z);

                this.yaw = GetAngleBetweenVectors(matrix.Forward, pointOnShipsPlane, matrix.Right, matrix.Left);
                this.pitch = GetAngleBetweenVectors(pointOnShipsPlane, target, matrix.Up, matrix.Down);
            }

            override
            public string ToString() {
                return string.Format("{0:0.}", yaw)+"-"+ string.Format("{0:0.}", pitch);
            }
        }

        public static double GetAngleBetweenVectors(Vector3D first, Vector3D second, Vector3D fHalf, Vector3D sHalf) {
            // since we want a full 0-360 result, we need to implement a vector which will tell us in which half of the full spectrum we are

            double fl = first.Length(), sl = second.Length();
            if (fl > sl) { Vector3D.Multiply(second, fl / sl); }
            else
            if (sl > fl) { Vector3D.Multiply(first, sl / fl); }

            double angle = GetAngleBetweenVectors(first, second);

            return (Vector3D.Distance(second, fHalf) < Vector3D.Distance(second, sHalf)) ? angle : (angle > 0.5 ? 360 - angle: angle);
        }

        public static double GetAngleBetweenVectors(Vector3D first, Vector3D second) {

            double cos = first.Dot(second) / (first.Length()*second.Length());
            double rad = Math.Acos(cos);

            //Program.program.ErrorOutput(cos+" "+rad);

            return 180*rad/Math.PI;
        }

        bool GetControllingBlock() {
            List<IMyShipController> controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controllers);
            IMyShipController output = null;
            foreach (IMyShipController ctrl in controllers) {
                if (IsOnThisGrid(ctrl) && ctrl.IsWorking) {
                    output = ctrl;
                    if (ctrl.IsMainCockpit) break;
                }
            }
            Ship_Controller = output;
            return output != null;
        }

        public static string RelationToLetter(MyRelationsBetweenPlayerAndBlock relation) {return RelationToAbbreviation(relation).Substring(0, 1);}

        public static string RelationToAbbreviation(MyRelationsBetweenPlayerAndBlock relation) {
            switch (relation) {
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    return "HOS";

                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                case MyRelationsBetweenPlayerAndBlock.Friends:
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    return "FRI";

                default:
                    return "NEU";
            }
        }

        public static string TypeToLetter(MyDetectedEntityType type) {
            switch (type) {
                case MyDetectedEntityType.Planet:
                case MyDetectedEntityType.Asteroid: 
                    return "AST"; //astral
                case MyDetectedEntityType.CharacterHuman:
                case MyDetectedEntityType.CharacterOther:
                    return "ORG"; //organic
                case MyDetectedEntityType.LargeGrid: 
                    return "LRG"; //large grid
                case MyDetectedEntityType.Meteor:
                case MyDetectedEntityType.Missile:
                    return "PRJ"; //projectile
                case MyDetectedEntityType.SmallGrid: 
                    return "SML"; //small grid
                case MyDetectedEntityType.FloatingObject:
                case MyDetectedEntityType.Unknown:
                case MyDetectedEntityType.None:
                default:
                    return "MSC"; //misc.
            }
        }

        public void Save() {
            Storage = currExt + ";" + currRPM + ";" +
            DetectPlayers + ";" + DetectFloatingObjects + ";" +
            DetectSmallShips + ";" + DetectLargeShips + ";" + DetectStations + ";" + DetectSubgrids + ";" + DetectAsteroids + ";" +
            DetectOwner + ";" + DetectFriendly + ";" + DetectNeutral + ";" + DetectEnemy + ";" 
            //+ AEGIS.isOnline
            ;
        }

        string GetFullScriptName(string ScriptName) {return "[" + ScriptName + "] Script";}
        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = GetFullScriptName(ScriptName);
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Register.SetMe(Me);
            SayMyName(MY_PREFIX);
            ErrorOutput("", false);
            SetMax(900);
            GetRadars();
            GetControllingBlock();
            GetTurretController();
            int i = 0;
            try {
                string[] args           = Storage.Split(';');
                currExt                 = float.Parse(args[i++]);
                currRPM                 = float.Parse(args[i++]);

                DetectPlayers           = bool.Parse(args[i++]);
                DetectFloatingObjects   = bool.Parse(args[i++]);
                DetectSmallShips        = bool.Parse(args[i++]);
                DetectLargeShips        = bool.Parse(args[i++]);
                DetectStations          = bool.Parse(args[i++]);
                DetectSubgrids          = bool.Parse(args[i++]);
                DetectAsteroids         = bool.Parse(args[i++]);

                DetectOwner             = bool.Parse(args[i++]);
                DetectFriendly          = bool.Parse(args[i++]);
                DetectNeutral           = bool.Parse(args[i++]);
                DetectEnemy             = bool.Parse(args[i++]);

                AEGIS                   = bool.Parse(args[i++]);
            }
            catch (Exception e) {
                e.ToString();
                int f = 0;

                if (++f >= i) currExt               = 8000f;
                if (++f >= i) currRPM               = 3f;

                if (++f >= i) DetectPlayers         = false;
                if (++f >= i) DetectFloatingObjects = false;
                if (++f >= i) DetectSmallShips      = true;
                if (++f >= i) DetectLargeShips      = true;
                if (++f >= i) DetectStations        = true;
                if (++f >= i) DetectSubgrids        = false;
                if (++f >= i) DetectAsteroids       = false;

                if (++f >= i) DetectOwner           = false;
                if (++f >= i) DetectFriendly        = false;
                if (++f >= i) DetectNeutral         = true;
                if (++f >= i) DetectEnemy           = true;

                if (++f >= i) AEGIS                 = true;
            }
            SetRadars(currExt, currRPM);
            GetScreens();
            misCMDListener = IGC.RegisterBroadcastListener(misCMDTag);
            misCMDListener.SetMessageCallback();
        }

        public void SendToAEGIS(MyDetectedEntityInfo entity) {
            SendToAEGIS(entity.Position, entity.Velocity, entity.EntityId.ToString());
        }

        public void SendToAEGIS(Vector3D vec, Vector3D vec2, string tag) {
            SendCoords(vec.X, vec.Y, vec.Z, vec2.X, vec2.Y, vec2.Z, tag);
        }

        public void SendCoords(Vector3D vec) { SendCoords(vec.X, vec.Y, vec.Z); }
        public void SendCoords(Vector3D vec, Vector3D vec2) { SendCoords(vec.X, vec.Y, vec.Z, vec2.X, vec2.Y, vec2.Z); }

        public void SendCoords(double X1, double Y1, double Z1, double X2 = 0, double Y2 = 0, double Z2 = 0, string tag = missileTag) { IGC.SendBroadcastMessage(tag, "TARSET;" + X1 + ";" + Y1 + ";" + Z1 + ";" + X2 + ";" + Y2 + ";" + Z2); }

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

        void Detect() {
            bool AllRight = true;
            List<MyDetectedEntityInfo> Detected;
            foreach (IMySensorBlock rad in Radars) {
                if (rad != null) {
                    Detected = new List<MyDetectedEntityInfo>();
                    rad.DetectedEntities(Detected);
                    foreach (MyDetectedEntityInfo entity in Detected) {
                        Register.Add(entity);
                        if (curTarget != null) {
                            if (entity.EntityId.Equals(curTarget.id) && curTarget.timeNum != 42044469) {
                                curTarget.Update(entity);
                                //if (curTarget.ShouldBC()) {
                                SendCoords(entity.Position, entity.Velocity);
                                //}
                            }
                        }
                    }
                }
                else AllRight = false;
            }

            if (Turret_Controller != null && AEGIS) {
                string output = "";
                List<Entry> targets = Register.GetRefList();

                foreach(Entry entry in targets) {
                    output += 
                       RelationToLetter(entry.relation) + entry.id + ";" + entry.Stringify() + "\n";
                }
                if (targets.Count > 0) { 
                    Me.CustomData = output;
                    Echo(targets.Count + " " + Turret_Controller.TryRun("reg") + "\n" + output);
                }
            }

            if (!AllRight) GetRadars();
        }

        void GetTurretController() {
            List<IMyProgrammableBlock> blocks = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks);
            foreach (IMyProgrammableBlock block in blocks) {
                if(IsOnThisGrid(block) && block.CustomName.Equals(GetFullScriptName(TURRET_CONTROLLER_SCRIPT_NAME))) {
                    Turret_Controller = block;
                    return;
                }
            }
        }

        void SetRadars() { SetRadars(currExt, currRPM); }

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
                rad.PlayProximitySound = false;

                rad.BackExtend = detExt;
                rad.FrontExtend = detExt;
                rad.LeftExtend = detExt;
                rad.RightExtend = detExt;
                rad.TopExtend = detThcc;
                rad.BottomExtend = detThcc;

                rad.DetectPlayers = DetectPlayers;
                rad.DetectFloatingObjects = DetectFloatingObjects;
                rad.DetectSmallShips = DetectSmallShips;
                rad.DetectLargeShips = DetectLargeShips;
                rad.DetectStations = DetectStations;
                rad.DetectSubgrids = DetectSubgrids;
                rad.DetectAsteroids = DetectAsteroids;

                rad.DetectOwner = DetectOwner;
                rad.DetectFriendly = DetectFriendly;
                rad.DetectNeutral = DetectNeutral;
                rad.DetectEnemy = DetectEnemy;
            }
            currExt = detExt;
            currRPM = tarRPM;

            float newMax = 60f / tarRPM; /// w sekundach

            SetMax((int)newMax * 60); /// w tickach
        }

        void GetRadars() {
            List<IMySensorBlock> temp
                    = new List<IMySensorBlock>();
            Radars = new List<IMySensorBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMySensorBlock rad in temp) {
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

        List<IMyTextPanel> GetErrorScreens() {
            List<IMyTextPanel> temp
                    = new List<IMyTextPanel>();

            List<IMyTextPanel> screens = new List<IMyTextPanel>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyTextPanel scr in temp) {
                if (IsOnThisGrid(scr) && scr.CustomName.Contains("[ERROR]")) screens.Add(scr);
            }

            return screens;
        }

        void GetScreens() {
            List<IMyTextPanel> temp
                    = new List<IMyTextPanel>();
            Screens = new List<IMyTextPanel>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyTextPanel scr in temp) {
                if (IsOnThisGrid(scr) && scr.CustomName.Contains(RadarCode)) Screens.Add(scr);
                scr.Font = "Monospace";
                scr.FontSize = 0.75f;
            }
        }

        string PrintOut() {
            return
                "Radar no: " + Radars.Count + "   Screen no: " + Screens.Count + "\n"
                + "Sensor range: " + currExt + " m Buoy RPM: " + currRPM + "\n"
                + "AEGIS: " + (AEGIS?"ON":"OFF")
                //+ "AEGIS: " + (AEGIS.isOnline ? "ON, TRACKING " + AEGIS.GetTarNo() + " OBJECTS.\n " + AEGIS.launchAttempts + " LAUNCH ATTEMPTS SO FAR." : "OFF") + "\n"

                + "\n";
        }

        void Output(object input, bool append = false) {
            bool AllRight = true;
            string message = input is string ? (string)input : input.ToString();
            foreach (IMyTextPanel screen in Screens) {
                if (screen != null) {
                    screen.ContentType = ContentType.TEXT_AND_IMAGE;
                    screen.WriteText(message, append);
                }
                else AllRight = false;
            }
            //Echo(message);
            if (!AllRight) GetScreens();
        }

        void ErrorOutput(object input, bool append = true) {
            bool AllRight = true;
            string message = input is string ? (string)input : input.ToString();
            message += "\n";
            foreach (IMyTextPanel screen in GetErrorScreens()) {
                if (screen != null) {
                    screen.ContentType = ContentType.TEXT_AND_IMAGE;
                    screen.WriteText(message, append);
                }
                else AllRight = false;
            }
            //Echo(message);
            if (!AllRight) GetScreens();
        }

        void AbortAllLaunches() {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.Contains("-")) {
                    pb.TryRun("LAUNCHABORT");
                }
            }
        }

        void PrepareForLaunch(int launchSize = 0) {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            int counter = 0;
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("MISSILE-")) {
                    string toParse = pb.CustomName.Substring(8);
                    int missNo;
                    try { missNo = int.Parse(toParse); }
                    catch (Exception e) { missNo = 0; e.ToString(); }
                    if (missNo != 0) {
                        AddAJob(new Job(JobType.OpenDoor, missNo * 10, missNo));
                        AddAJob(new Job(JobType.Launch, 200 + missNo * 10, missNo));
                        AddAJob(new Job(JobType.CloseDoor, 700 + missNo * 10, missNo));
                    }
                    else continue;
                    if (launchSize != 0 && ++counter >= launchSize) return;
                }
            }
        }

        void Abort(bool selfDestruct = false) {
            schedule.Clear();
            if (selfDestruct) {
                IGC.SendBroadcastMessage(missileTag, "ABORT");
            }
            AbortAllLaunches();
            curTarget = null;
        }

        void PrepareGPSStrike(double X, double Y, double Z) {
            curTarget = new Entry(X, Y, Z);
            PrepareForLaunch();
        }

        void SortJobs() {
            schedule = schedule.OrderBy(o => o.TTJ).ToList();
        }

        void AddAJob(Job job) {
            schedule.Add(job);
            SortJobs();
        }

        void BumpMyMissile(int missNo) {
            foreach (Job job in schedule) {
                if (job.misNo == missNo && job.type == JobType.Launch) {
                    job.TTJ = 1;
                    SortJobs();
                }
            }
        }

        void DoYourJob() {
            if (schedule.Count > 0 && schedule[0].TTJ <= 0) {
                Job curr = schedule[0];
                schedule.RemoveAt(0);
                string name = curr.anti ? "ANTI-" : "MISSILE-";
                IMyAirtightHangarDoor siloDoor = GridTerminalSystem.GetBlockWithName("Silo Door " + curr.misNo) as IMyAirtightHangarDoor;
                IMyDoor antiDoor = GridTerminalSystem.GetBlockWithName("Anti Door " + curr.misNo) as IMyDoor;
                switch (curr.type) {
                    case JobType.OpenDoor:
                        if (!curr.anti) {
                            if (siloDoor != null) siloDoor.OpenDoor();
                            else {
                                ErrorOutput("NO SILO DOOR \"" + ("Silo Door " + curr.misNo) + "\"");
                                BumpMyMissile(curr.misNo);
                                //Abort();
                            }
                        }
                        else {
                            if (antiDoor != null) antiDoor.OpenDoor();
                            else {
                                ErrorOutput("NO ANTI DOOR \"" + ("Anti Door " + curr.misNo) + "\"");
                                //Abort();
                            }
                        }
                        break;

                    case JobType.Launch:
                        IMyProgrammableBlock missile = GridTerminalSystem.GetBlockWithName(name + curr.misNo) as IMyProgrammableBlock;
                        if (missile == null) {
                            string message = "ABORTING LAUNCH: MISSILE DOES NOT EXIST: \"" + name + curr.misNo + "\"";
                            ErrorOutput(message);
                            return;
                        }
                        else {
                            if (!curr.anti) { 
                                if (curTarget == null) {
                                    string message = "ABORTING LAUNCH: TARGET DOES NOT EXIST.";
                                    Abort();
                                    ErrorOutput(message);
                                    return;
                                }
                                else {
                                    missile.TryRun("prep " + curTarget.location.X + " " + curTarget.location.Y + " " + curTarget.location.Z);
                                }
                            }
                        }
                        break;


                    case JobType.CloseDoor:
                        if (!curr.anti) {
                            if (siloDoor != null) siloDoor.CloseDoor();
                        }
                        else {
                            if (antiDoor != null) antiDoor.CloseDoor();
                        }
                        break;
                }
            }
        }

        static string Format(double input, int afterPoint = 1) {
            string addition = "";

            for (int i = 0; i < afterPoint; i++) addition += "#";

            return string.Format("{0:0." + addition + "}", input);
        }

        static string Format(float input, int afterPoint = 1) {
            string addition = "";

            for (int i = 0; i < afterPoint; i++) addition += "#";

            return string.Format("{0:0." + addition + "}", input);
        }

        static string Format(Vector3D input) {
            return "(" + Format(input.X) + "," + Format(input.Y) + "," + Format(input.Z) + ")";
        }

        void IncrementAll() {
            Register.IncrementAll();
            if (curTarget != null) {
                curTarget.Increment();
                if (curTarget.TSB > 2 * MAX_WAIT) curTarget = null;
            }
            foreach (Job job in schedule) job.TTJ--;
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0) {
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
                                    try {
                                        curTarget = new Entry(float.Parse(bits[1]), float.Parse(bits[2]), float.Parse(bits[3]));
                                        PrepareForLaunch();
                                    }
                                    catch (Exception e) {
                                        Echo(e.ToString());
                                    }
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
                    int index;
                    Entry target;
                    switch (args[0]) {
                        case "ext":
                        case "range":
                            try { detExt = args.Length > 1 ? float.Parse(args[1]) : 8000f; } catch (Exception e) { e.ToString(); }
                            Echo("set: " + detExt + " " + tarRPM);
                            SetRadExt(detExt);
                            break;

                        case "rpm":
                            try { tarRPM = args.Length > 1 ? float.Parse(args[1]) : 3f; } catch (Exception e) { e.ToString(); }
                            Echo("set: " + detExt + " " + tarRPM);
                            SetRadRPM(tarRPM);
                            break;

                        case "rad":
                        case "set":
                            try { detExt = args.Length > 1 ? float.Parse(args[1]) : 8000f; } catch (Exception e) { e.ToString(); }
                            try { tarRPM = args.Length > 2 ? float.Parse(args[2]) : 3f; } catch (Exception e) { e.ToString(); }
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

                        case "get":
                            if (args.Length < 2) {
                                index = 0;
                            }
                            else {
                                try { index = int.Parse(args[1]) - 1; } catch (Exception e) { e.ToString(); return; }
                            }
                            target = Register.Get(index); if (target == null) return;

                            ErrorOutput("Position of [" + (index + 1) + "]: " + Format(target.location));

                            break;


                        case "attack":
                        case "fire":
                            if (args.Length < 2) {
                                if (curTarget != null) PrepareForLaunch();
                            }
                            else {
                                int magnitude = 0;
                                try { index = int.Parse(args[1]) - 1; } catch (Exception e) { e.ToString(); return; }
                                target = Register.Get(index); if (target == null) return;
                                curTarget = target;

                                if (args.Length > 2) {
                                    try { magnitude = int.Parse(args[2]); } catch (Exception e) { e.ToString(); magnitude = -1; }
                                }
                                PrepareForLaunch(magnitude);
                            }

                            break;

                        case "abort":
                            Abort(true);
                            break;

                        case "aegis":
                            if (args.Length < 2)
                                AEGIS = !AEGIS;
                            else {
                                switch (args[1]) {
                                    case "on":
                                    case "up":
                                    case "true":
                                        AEGIS = true;
                                        break;

                                    case "off":
                                    case "down":
                                    case "false":
                                    case "terminate":
                                        AEGIS = false;
                                        break;
                                }
                            }
                            if (Turret_Controller != null) 
                                Turret_Controller.TryRun(argument);
                            else 
                                Echo("Turret Controller cannot be reached.");
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
                                        // OJ NIE NIE BYCZQ -1
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

                                    default:
                                        Echo("Term unknown: '" + args[1]+"'");
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

                                    default:
                                        Echo("Term unknown: '" + args[1] + "'");
                                        break;
                                }
                                SetRadars();
                            }
                            break;

                        default:
                            Echo("Term unknown: '" + args[0] + "'");
                            break;
                    }
                }
            }
        }
    }
}
