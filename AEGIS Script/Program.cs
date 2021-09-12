using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        /////////////////////////////////////////////////// DEOBJECTIFIED AEGIS ///////////////////////////////////////////////////
        bool AEGISIsOnline          = true;     /// All Functionality is on
        bool AEGISUseRadarData      = false;
        readonly bool AEGISTargetsNeutrals   = false;

        readonly Dictionary<long, Entry> AMTargets = new Dictionary<long, Entry>();   // Anti Missiles Targets
        void AddToAMT(Entry entry) {
            if (AMTargets.ContainsKey(entry.Id)) SendToAEGIS(entry);
            else if(PrepareForLaunch(entry.Id)) AMTargets.Add(entry.Id, entry);
        }
        bool TryGetAMT(long key, out Entry entry) { return (AMTargets.TryGetValue(key, out entry)); }

        readonly Dictionary<long, Entry> CGTargets = new Dictionary<long, Entry>();    // Contact Guns Targets
        void AddToCGT(Entry entry) {
            if (CGTargets.ContainsKey(entry.Id)) CGTargets.Remove(entry.Id);
            CGTargets.Add(entry.Id, entry);
        }

        List<Entry> GetSortedCGTargets() {
            List<Entry> output = new List<Entry>();
            foreach (Entry entry in CGTargets.Values) output.Add(entry);

            return output.OrderByDescending(o => o.Threat).ToList();
        }

        void EvaluateTarget(Entry entry) {
            if (entry.Threat < 0d) throw new Exception(entry.Comment);
            else
            if ((entry.Threat = CalculateThreat(entry)) > 0) 
                AddToCGT(entry);
        }

        double CalculateThreat(Entry entry) {
            if (!entry.Relation.Equals(Relation.FRIEND) && (Ship_Controller != null || Program.MyInstance.SetShipController())) {
                Vector3D
                    EnPos = entry.Position,
                    EnVel = entry.Velocity,
                    MyPos = Ship_Controller.GetPosition(),
                    MyVel = Ship_Controller.GetShipVelocities().LinearVelocity,
                    DangerousHeading;

                if (!EnVel.IsValid()) throw new Exception("Enemy's velocity vector is invalid.");

                double
                    EnSpd = EnVel.Length(),
                    MySpd = MyVel.Length(),
                    Distance,
                    baseThreat = entry.Relation.Equals(Relation.HOSTILE) ? 1d : (AEGISTargetsNeutrals? 1d:0d);

                //AEGISTargetsNeutrals

                DangerousHeading = Vector3D.Subtract(EnPos, MyPos); Distance = DangerousHeading.Length();
                if (Distance == 0) Distance = 0.1d;

                // since we've written down lengths of the velocity vectors, we can safely normalize them now.
                EnVel = Vector3D.Normalize(EnVel);
                MyVel = Vector3D.Normalize(MyVel);

                if (EnSpd <= 10) return baseThreat*((Distance>=1000)? 0d:(1000/Distance)); // the object either does not move or moves at low speeds, so it is not an active danger to the ship (probably)
                else {
                    Vector3D 
                        AbsDev1, // Absolute Deviation, a Vector between Enemy-Us and Enemy-Estimated position after SecondsToImpact seconds
                        AbsDev2,
                        EnProjPos,
                        MyProjPos;

                    double 
                        SecondsToImpact1, SecondsToImpact2;

                    // Variant One - Detecting blindly following objects (also true if the ship is stationary)
                    SecondsToImpact1 = Distance / EnSpd;
                    EnProjPos = Vector3D.Add(EnPos, Vector3D.Multiply(EnVel, EnSpd * SecondsToImpact1));
                    AbsDev1 = Vector3D.Subtract(EnProjPos, MyPos); // 500m or less should probably trigger a response

                    // Variant Two - Detecting objects that either predict or will find themselves on the path of the ship
                    MyProjPos = ApplyTarSpd(EnPos, Vector3D.Multiply(EnVel, EnSpd), MyPos, Vector3D.Multiply(MyVel, MySpd));
                    SecondsToImpact2 = MySpd > 0 ? (Vector3D.Subtract(MyPos, MyProjPos).Length() / MySpd) : 60;
                    EnProjPos = Vector3D.Add(EnPos, Vector3D.Multiply(EnVel, EnSpd * SecondsToImpact2));
                    AbsDev2 = Vector3D.Subtract(EnProjPos, MyProjPos);

                    double
                        threat,
                        adjAbsDev = MySpd > 0 ? AbsDev2.Length() : AbsDev1.Length(),
                        worstCaseSTI = MySpd > 0 ? (SecondsToImpact1 < SecondsToImpact2 ? SecondsToImpact1 : SecondsToImpact2) : SecondsToImpact1;


                    if ((AbsDev1.Length() <= 500 || AbsDev2.Length() <= 500)) {
                        if (Distance >= 1000) {
                            baseThreat = 100d;
                            AddToAMT(entry);
                        }
                        PriorityMessage(
                            String.Format(
                                "{0}\n{1}\n{2}\n{3}\n{4}",
                                entry.Relation.Equals(Relation.HOSTILE)?"HOSTILE":"NEUTRAL","ENTITY ON","INTERCEPT","COURSE",
                                new Bearing(Ship_Controller.Position, Ship_Controller.WorldMatrix,EnPos).ToString()
                            )
                        );
                    }
                    else baseThreat *= entry.Relation.Equals(Relation.HOSTILE) ? 10 : 1;

                    return (threat = 10 * ((baseThreat * EnSpd * EnSpd) / (adjAbsDev * worstCaseSTI))) > 1d ? threat : 1d;
                }


            }
            else return entry.Relation.Equals(Relation.HOSTILE) ? 1d : 0d;
        }
 
        Vector3D NOTHING = new Vector3D(44, 44, 44);
        Vector3D ApplyTarSpd(Vector3D position, Vector3D speed, Vector3D myPosition, Vector3D myVel) {
            double
                mySpeed = myVel.Length(),
                enSpeed = speed.Length(),
                multiplier;

            //position = Vector3D.Add(position, Vector3D.Multiply(speed,4 / 60));

            if (enSpeed > 0) {
                Vector3D output = GetProjectedPos(position, speed, myPosition, myVel);
                if (!output.Equals(NOTHING)) {
                    return output;
                }
            }

            multiplier = (mySpeed != 0 && enSpeed != 0) ? (enSpeed / mySpeed) : 0;

            Vector3D
                addition = Vector3D.Multiply(speed, multiplier);

            return Vector3D.Add(position, addition);
        }
        Vector3D GetProjectedPos(Vector3D enPos, Vector3D enSpeed, Vector3D myPos, Vector3D mySpeed) {
            /// do not enter if enSpeed is a "0" vector, or if our speed is 0
            double speed = mySpeed.Length(); if(speed<=0) speed = 1d;
            Vector3D
                A = myPos,
                B = enPos;

            double
                t = speed / enSpeed.Length(),           //t -> b = a*t  
                projPath,                               //b
                dist = Vector3D.Distance(A, B),         //c
                cos = InterCosine(enSpeed, Vector3D.Subtract(enPos, myPos)),

                //delta = 4 * dist * dist * ((1 / (t * t)) + (cos * cos) - 1);
                delta = 4 * (dist * dist) * ((t * t * cos * cos) - (t * t) + 1);

            if (delta < 0) {
                return NOTHING;
            }
            else
            if (delta == 0) {
                //projPath = -1 * (2 * dist * cos) / (2 * (((t * t) - 1) / (t * t)));
                projPath = ((t * dist * cos) / ((t * t) - 1));
            }
            else {
                //if (t == 0) return NOTHING;
                //else
                //if (t == 1) projPath = (dist) / (2 * cos);
                //else {
                //    projPath = ((2 * dist * cos - Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                //    if (projPath < 0) {
                //        projPath = ((2 * dist * cos + Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                //    }
                //}
                if ((projPath = (((2 * t * dist * cos) + Math.Sqrt(delta)) / (2 * ((t * t) - 1)))) < 0) {
                    projPath = (((2 * t * dist * cos) - Math.Sqrt(delta)) / (2 * ((t * t) - 1)));
                }
            }
            mySpeed = Vector3D.Normalize(mySpeed);
            mySpeed = Vector3D.Multiply(mySpeed, projPath);

            return Vector3D.Add(myPos, mySpeed);
        }
        double InterCosine(Vector3D first, Vector3D second) {
            double
                scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                productOfLengths = first.Length() * second.Length();

            return scalarProduct / productOfLengths;
        }
        ////////////////////////////////////////////////////// END OF AEGIS ///////////////////////////////////////////////////////

        const string RADAR_CONTROLLER_SCRIPT_NAME = "RADAR";
        IMyProgrammableBlock Radar_Controller;

        public static IMyShipController Ship_Controller;

        List<IMyProgrammableBlock> turrets;
        List<IMyLargeTurretBase> genericTurrets;
        List<IMyTextPanel> screens;
        List<Entry> targets;

        List<Job> schedule = new List<Job>();

        public static Program MyInstance;

        const string
            TURRET_BASE = "AEG-",
            MY_PREFIX = "AEGIS";

        string content = "";

        int timeNo = 0,
            ticksWOOrders = 0; // ticks w/o orders

        readonly IMyBroadcastListener AEGISListener;

        enum Relation {
            FRIEND,
            HOSTILE,
            NEUTRAL
        }

        class Entry {
            public long Id;
            public Relation Relation;
            public Vector3D Position;
            public Vector3D Velocity;
            public double Threat;
            public string Comment;

            public Entry(Entry Entry, double Threat) {
                this.Id = Entry.Id;
                this.Relation = Entry.Relation;
                this.Position = Entry.Position;
                this.Velocity = Entry.Velocity;
                this.Threat = Threat;
            }

            public Entry(MyDetectedEntityInfo entity) {
                this.Id = entity.EntityId;
                this.Relation = OfficialRelationToRelation(entity.Relationship);
                this.Position = entity.Position;
                this.Velocity = entity.Velocity;
                this.Threat = 0;
                this.Comment = "Generated with Official entity";
            }

            public Entry(string input) {
                if (input.Length <= 0) {
                    this.Threat = -1;
                    Comment = "input string does not exist.";
                    return;
                }
                int i = 0;
                string[]
                    content;

                content = input.Split(';');
                try {
                    double px, py, pz, vx, vy, vz;
                    Relation = LetterToRelation(content[i].Substring(0, 1));
                    Id = long.Parse(content[i++].Substring(1));
                    px = double.Parse(content[i++]) / 10;
                    py = double.Parse(content[i++]) / 10;
                    pz = double.Parse(content[i++]) / 10;
                    vx = double.Parse(content[i++]) / 10;
                    vy = double.Parse(content[i++]) / 10;
                    vz = double.Parse(content[i++]) / 10;
                    Position = new Vector3D(px, py, pz);
                    Velocity = new Vector3D(vx, vy, vz);
                    Threat = 0;
                }
                catch (Exception e) {
                    e.ToString();
                    this.Relation = Relation.NEUTRAL;
                    this.Position = new Vector3D();
                    this.Velocity = new Vector3D();
                    this.Comment = "i==" + i + " '" + input + "'";
                    this.Threat = -1;
                }
            }

            private Relation OfficialRelationToRelation(MyRelationsBetweenPlayerAndBlock relation) {
                switch (relation) {
                    case MyRelationsBetweenPlayerAndBlock.Enemies:
                        return Relation.HOSTILE;

                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    case MyRelationsBetweenPlayerAndBlock.Friends:
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        return Relation.FRIEND;

                    default:
                        return Relation.NEUTRAL;
                }
            }

            private Relation LetterToRelation(String letter) {
                switch (letter) {
                    case "F": return Relation.FRIEND;
                    case "N": return Relation.NEUTRAL;
                    case "H": return Relation.HOSTILE;

                    default: throw new Exception("Letter is not one of the base ones: 'F','N','H': '" + letter + "'.");
                }
            }
        }

        public class Bearing {
            private readonly double yaw, pitch;

            public Bearing(double yaw, double pitch) {
                this.yaw = yaw;
                this.pitch = pitch;
            }

            public Bearing(Vector3D position, MatrixD matrix, Vector3D target) {
                // "forward" and "right" are vectors relative to the starship. in order for our calculations to work, we need to create a relative position of the thing we are calculating bearing for
                target = Vector3D.Subtract(target, position);
                Vector3D X = new Vector3D(matrix.Forward.Y * matrix.Right.Z - matrix.Forward.Z * matrix.Right.Y, matrix.Forward.Z * matrix.Right.X - matrix.Forward.X * matrix.Right.Z, matrix.Forward.X * matrix.Right.Y - matrix.Forward.Y * matrix.Right.X);
                double t = -1 * (X.X * target.X + X.Y * target.Y + X.Z * target.Z) / (X.X * X.X + X.Y * X.Y + X.Z * X.Z);
                Vector3D pointOnShipsPlane = new Vector3D((X.X * t) + target.X, (X.Y * t) + target.Y, (X.Z * t) + target.Z);

                this.yaw = GetAngleBetweenVectors(matrix.Forward, pointOnShipsPlane, matrix.Right, matrix.Left);
                this.pitch = GetAngleBetweenVectors(pointOnShipsPlane, target, matrix.Up, matrix.Down);
            }

            double GetAngleBetweenVectors(Vector3D first, Vector3D second, Vector3D fHalf, Vector3D sHalf) {
                // since we want a full 0-360 result, we need to implement a vector which will tell us in which half of the full spectrum we are

                double fl = first.Length(), sl = second.Length();
                if (fl > sl) { Vector3D.Multiply(second, fl / sl); }
                else
                if (sl > fl) { Vector3D.Multiply(first, sl / fl); }

                double angle = GetAngleBetweenVectors(first, second);

                return (Vector3D.Distance(second, fHalf) < Vector3D.Distance(second, sHalf)) ? angle : (angle > 0.5 ? 360 - angle : angle);
            }

            double GetAngleBetweenVectors(Vector3D first, Vector3D second) {
                double cos = first.Dot(second) / (first.Length() * second.Length());
                double rad = Math.Acos(cos);

                return 180 * rad / Math.PI;
            }

            override
            public string ToString() {
                return string.Format("{0,3:0.}-{1,-3:0.}", yaw, pitch);
            }
        }


        string GetFullScriptName(string ScriptName) { return "[" + ScriptName + "] Script"; }
        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = GetFullScriptName(ScriptName);
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText("\n\n" + ScriptName);
        }

        bool IsOnThisGrid(IMyCubeBlock block) { return Me.CubeGrid.Equals(block.CubeGrid); }

        bool SetRadarController() {
            /// this should be okay if there are no ships with Radar Control Script docked to the main ship
            Radar_Controller = GridTerminalSystem.GetBlockWithName(GetFullScriptName(RADAR_CONTROLLER_SCRIPT_NAME)) as IMyProgrammableBlock;

            /// if the programmable block we picked is not from this ship, we commence the search to find it anyway
            if (Radar_Controller != null && !IsOnThisGrid(Radar_Controller)) {
                List<IMyProgrammableBlock> temp = new List<IMyProgrammableBlock>();
                Radar_Controller = null;
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyProgrammableBlock prog in temp) {
                    if (IsOnThisGrid(prog) && prog.CustomName.Contains(RADAR_CONTROLLER_SCRIPT_NAME)) {
                        Radar_Controller = prog; return true;
                    }
                }
            }
            /// and if we fail... welp, we can just inform the rest of the script that we can't do nothing
            return Radar_Controller != null;
        }

        bool SetShipController() {
            List<IMyShipController> controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controllers);
            Ship_Controller = null;

            foreach(IMyShipController controller in controllers) {
                if (IsOnThisGrid(controller)) {
                    if (Ship_Controller == null || controller.IsMainCockpit) {
                        Ship_Controller = controller;
                        if (controller.IsMainCockpit) return true;
                    }
                }
            }

            return Ship_Controller != null;
        }

        public void Save() {
            Storage = AEGISUseRadarData + ";" + AEGISIsOnline + ";" + AEGISTargetsNeutrals;
            string stringToFormat = "{0};{1};{2}";
            Storage = String.Format(stringToFormat,AEGISUseRadarData,AEGISIsOnline,AEGISTargetsNeutrals);
        }

        public Program() {
            Program.MyInstance = this;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SetRadarController();
            SetShipController();
            SayMyName(MY_PREFIX);
            GetMeTheTurrets();
            int i = 0;
            try {
                string[] data = Storage.Split(';');
                AEGISUseRadarData = bool.Parse(data[i++]);
                AEGISIsOnline = bool.Parse(data[i++]);
                AEGISTargetsNeutrals = bool.Parse(data[i++]);
            }
            catch (Exception e) {
                e.ToString();
                int f = 0;
                if (++f >= i) AEGISUseRadarData = false;
                if (++f >= i) AEGISIsOnline = true;
                if (++f >= i) AEGISTargetsNeutrals = false;
            }
            AEGISListener = IGC.RegisterBroadcastListener(MY_PREFIX);
            AEGISListener.SetMessageCallback();
        }

        public void GetMeTheTurrets() {
            List<IMyProgrammableBlock>
                temp = new List<IMyProgrammableBlock>();
            turrets = new List<IMyProgrammableBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyProgrammableBlock block in temp) {
                if (IsOnThisGrid(block) && block.CustomName.Contains(TURRET_BASE) 
                    //&& !block.Equals(Me)
                    )
                    turrets.Add(block);
            }

            List<IMyLargeTurretBase>
                tmp = new List<IMyLargeTurretBase>();
            genericTurrets = new List<IMyLargeTurretBase>();

            GridTerminalSystem.GetBlocksOfType(tmp);
            foreach (IMyLargeTurretBase block in tmp) {
                if (IsOnThisGrid(block))
                    genericTurrets.Add(block);
            }
        }

        void SendToAEGIS(Entry entry) { SendToAEGIS(entry.Position, entry.Velocity, entry.Id.ToString()); }
        void SendToAEGIS(Vector3D vec, Vector3D vec2, string tag) {SendToAEGIS(vec.X, vec.Y, vec.Z, vec2.X, vec2.Y, vec2.Z, tag);}
        void SendToAEGIS(double X1, double Y1, double Z1, double X2 , double Y2 , double Z2 , string tag) { SendToAEGIS(tag, "TARSET;" + X1 + ";" + Y1 + ";" + Z1 + ";" + X2 + ";" + Y2 + ";" + Z2);}


        void SendToAEGIS(string tag, string message) { IGC.SendBroadcastMessage(tag, message); }

        void AbortAllLaunches() {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.Contains("ANTI-")) {
                    pb.TryRun("LAUNCHABORT");
                }
            }
        }

        void Abort(bool selfDestruct = false) {
            schedule.Clear();
            AbortAllLaunches();
            if (!selfDestruct) return;
            else {
                foreach(long key in AMTargets.Keys) 
                    SendToAEGIS(key.ToString(), "ABORT");
            }
        }

        void ProcessJobs() {
            if (schedule.Count > 0 && schedule[0].TTJ <= 0) {
                Job curr = schedule[0];
                schedule.RemoveAt(0);
                string name = "ANTI-";
                IMyDoor antiDoor = GridTerminalSystem.GetBlockWithName("[NO-RENAME] Anti Door " + curr.misNo) as IMyDoor;
                switch (curr.type) {
                    case JobType.OpenDoor:
                            if (antiDoor != null) antiDoor.OpenDoor();
                        break;

                    case JobType.Launch:
                        IMyProgrammableBlock missile = GridTerminalSystem.GetBlockWithName(name + curr.misNo) as IMyProgrammableBlock;
                        if (missile == null) {
                            string message = "ABORTING LAUNCH: MISSILE DOES NOT EXIST: \"" + name + curr.misNo + "\"";
                            Output(message);
                            Function(false);
                            //ErrorOutput(message);
                            return;
                        }
                        else {
                            Entry target;
                            long id;

                            if (curr.code.Length > 0 && long.TryParse(curr.code, out id) && TryGetAMT(id, out target)) {
                                missile.TryRun("prep " + target.Position.X + " " + target.Position.Y + " " + target.Position.Z + " " + curr.code);
                            }
                        }
                        break;

                    case JobType.CloseDoor:
                            if (antiDoor != null) antiDoor.CloseDoor();
                        break;
                }
            }
            foreach (Job job in schedule) --job.TTJ;
        }

        public enum JobType {
            OpenDoor,
            Launch,
            CloseDoor
        }

        public class Job {
            public JobType  type;
            public string   code;
            public int      misNo,  // set if the job is allocated to a specific missile
                            TTJ;    // "TicksToJob"


            public Job(JobType type, int TTJ, int misNo, string code = "") {
                this.type   = type;
                this.TTJ    = TTJ;
                this.misNo  = misNo;
                this.code   = code;
            }
        }

        void SortJobs() {schedule = schedule.OrderBy(o => o.TTJ).ToList();}

        void AddAJob(Job job) {
            schedule.Add(job);
        }

        public bool PrepareForLaunch(long code, int launchsize = 1) {
            return PrepareForLaunch(code.ToString(), launchsize);
        }

        public bool PrepareForLaunch(string code, int launchSize = 1) {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            int counter = 0;
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("ANTIMISSILE-")) {
                    string toParse = pb.CustomName.Substring(12);
                    int missNo;
                    if (int.TryParse(toParse, out missNo)) {
                        pb.CustomName = "ANTI-" + missNo;
                        AddAJob(new Job(JobType.OpenDoor, 10 + counter * 10, missNo));
                        AddAJob(new Job(JobType.Launch, /*200 +*/ counter * 10, missNo, code));
                        AddAJob(new Job(JobType.CloseDoor, 75 + counter * 10, missNo));
                        SortJobs();
                    }
                    if (++counter >= launchSize) return true;
                }
            }
            return false;
        }

        public void ProcessData(string data) {
            data = ParseInput(data);
            foreach (IMyProgrammableBlock block in turrets) {
                block.CustomData = data;
                //Echo(data);
                block.TryRun("reg");
            }
        }

        public void ShareDirectCommand(string data) {
            foreach (IMyProgrammableBlock block in turrets) {
                block.TryRun(data);
            }
        }

        int PriorityMessageTimer = 0;
        void PriorityMessage(object input) {
            PriorityMessageTimer = 120;
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (IsOnThisGrid(screen) && screen.CustomName.Contains("[AEGIS]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) {
                screen.Alignment = TextAlignment.CENTER;
                screen.FontSize = 2.7f;
                screen.BackgroundColor = Color.Red;

                screen.WriteText("\n\n\n"+message, false);
            }
        }

        void Output(object input, bool append = false) {
            if (PriorityMessageTimer > 0) {
                PriorityMessageTimer--;
                return;
            }
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (IsOnThisGrid(screen) && screen.CustomName.Contains("[AEGIS]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) {
                screen.Alignment = TextAlignment.LEFT;
                screen.FontSize = 1f;
                screen.BackgroundColor = Color.Black;

                screen.WriteText(message, append);
            }
            //Echo(message);
        }

        string Stringify(Entry entity, bool forMe = true) {
            return
                (forMe ? (entity.Relation.ToString().Substring(0, 1))  + entity.Id.ToString() + ";" : "") +
                string.Format("{0:0.}", Math.Round(entity.Position.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Z * 10)) + ";" +

                string.Format("{0:0.}", Math.Round((double)entity.Velocity.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Z * 10))
                + ";" + string.Format("{0:0.0}", entity.Threat)
                + "\n";
        }


        public static string RelationToLetter(MyRelationsBetweenPlayerAndBlock relation) {
            switch (relation) {
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    return "H";

                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                case MyRelationsBetweenPlayerAndBlock.Friends:
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    return "F";

                default:
                    return "N";
            }
        }

        string Stringify(MyDetectedEntityInfo entity) {
            return
                entity.IsEmpty() ?
                "" :
                Stringify(new Entry(entity));
        }

        public string GetGenericTargettingData() {
            string output = "";
            Dictionary<long, MyDetectedEntityInfo> targets = new Dictionary<long, MyDetectedEntityInfo>();

            foreach (IMyLargeTurretBase turret in genericTurrets) {
                MyDetectedEntityInfo entity = turret.GetTargetedEntity();
                if (!targets.ContainsKey(entity.EntityId)) {
                    targets.Add(entity.EntityId, entity);
                    output += Stringify(entity);
                }
            }

            targets.Clear();
            return output;
        }

        public string ParseInput(string input) {
            CGTargets.Clear();
            string output = "";
            string[] content = input.Split('\n');
            for (int i = 0; i < content.Length; i++) {
                if (content[i].Length <= 0) continue;
                try {EvaluateTarget(new Entry(content[i]));}
                catch(Exception e) {
                    string comment = e.ToString();
                    Echo("Error: "+comment);
                    Output("Error: " + comment + "\n");
                    Function(false); 
                    return "";
                }
            }

            targets = GetSortedCGTargets();
            foreach (Entry ent in targets) output += Stringify(ent, false) + "\n";
            return output;
        }

        public void Function(bool yes) {
            AEGISIsOnline = yes;
            if (yes)
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            else {
                AEGISUseRadarData = false;
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Abort(true);
                ShareDirectCommand("tar");
            }
        }

        int ProgressIncrementer = 0;
        string ProgressShower() {
            switch (ProgressIncrementer++ / 15) {
                case 0: return "/";
                case 1: return "-";
                case 2: return "\\";
                case 3: return "|";
                default:
                    ProgressIncrementer = 0;
                    return "|";
            }
        }

        void ParseTurretData(string input) {
            if (input.Length <= 0) return;
            string[] rows = input.Split('\n');
            for(int i = 0; i<rows.Length; i++) {
                string[] data = rows[i].Split(';');
                int id;
                if (data.Length > 1 && int.TryParse(data[0], out id)) {
                    if (TurretStatuses.ContainsKey(id)) {
                        TurretStatuses.Remove(id);
                    }
                    TurretStatuses.Add(id, data[1]);
                }
                else {
                    string output = "";
                    for (int o = 0; o < data.Length; o++) output+="'"+data[o]+"' ";
                }
            }

        }

        readonly Dictionary<int, string> TurretStatuses = new Dictionary<int, string>();
        string GetTurretStatus() {
            string output = "", data;

            foreach(int key in TurretStatuses.Keys.OrderBy(o => o).ToList())
                if(TurretStatuses.TryGetValue(key, out data))
                    output += string.Format("AEG-{0}: {1}\n", key, data);

            return output;
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {
                        case "reg":
                            if (!AEGISUseRadarData) {
                                content = "";
                                return;
                            }
                            if (Radar_Controller == null && !SetRadarController()) break;

                            content = Radar_Controller.CustomData;
                            break;

                        case "terminate":
                        case "off":
                        case "quit":
                        case "stop":
                            Function(false);
                            break;

                        case "on":
                        case "start":
                            Function(true);
                            break;


                        case "aegis":
                            if (args.Length < 2)
                                AEGISUseRadarData = !AEGISUseRadarData;
                            else {
                                switch (args[1]) {
                                    case "on":
                                    case "up":
                                    case "true":
                                        if (!AEGISIsOnline) Function(true);
                                        AEGISUseRadarData = true;
                                        break;

                                    case "off":
                                    case "down":
                                    case "false":
                                        AEGISUseRadarData = false;
                                        break;

                                    case "terminate":
                                        Function(false);
                                        break;
                                }
                            }
                            break;
                    }
                }
            }
            else
            if ((updateSource & UpdateType.IGC) > 0) {
                if (AEGISListener != null && AEGISListener.HasPendingMessage) {
                    MyIGCMessage message = AEGISListener.AcceptMessage();
                    if (message.Tag.Equals(MY_PREFIX)) {
                        string data = (string)message.Data;
                        string[] bits = data.Split(';');

                        if (bits.Length <= 0) return;
                        switch (bits[0].ToUpper()) {
                            case "BOOM":
                                long id;
                                if (bits.Length > 1 && long.TryParse(bits[1], out id)) {
                                    AMTargets.Remove(id);
                                }
                                break;
                        }
                    }
                }
                return;
            }
            else {
                string status;
                ParseTurretData(Me.CustomData);
                Me.CustomData = "";

                if (AEGISIsOnline && AEGISUseRadarData)
                    status = "AEGIS online.";
                else if (AEGISIsOnline && !AEGISUseRadarData)
                    status = "AEGIS in passive mode.";
                else
                    status = "AEGIS offline";

                status += string.Format("\nTracking {0,2} object{1,1}  {2}", CGTargets.Count, CGTargets.Count==1? "":"s", ProgressShower());
                if(AEGISTargetsNeutrals) status += "\nincluding neutral targets.";
                status += "\n\n" + GetTurretStatus();

                if (content.Length > 0 && AEGISUseRadarData) {
                    ProcessData(content + GetGenericTargettingData());
                    ticksWOOrders = 0;
                    content = "";
                    Output(status);
                }
                else {
                    if (!AEGISUseRadarData || ticksWOOrders >= 10) {
                        ProcessData(GetGenericTargettingData());
                        Output(status);
                    }
                    else ticksWOOrders++;
                }

                if (timeNo++ >= 600) {
                    timeNo = 0;
                    GetMeTheTurrets();
                }
                ProcessJobs();
            }
        }
    }
}