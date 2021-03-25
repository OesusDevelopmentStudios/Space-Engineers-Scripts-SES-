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

        const string RADAR_CONTROLLER_SCRIPT_NAME = "RADAR";
        IMyProgrammableBlock Radar_Controller;

        public static IMyShipController Ship_Controller;

        List<IMyProgrammableBlock> turrets;
        List<IMyLargeTurretBase> genericTurrets;
        List<IMyTextPanel> screens;
        List<Entry> targets;

        List<Job> schedule = new List<Job>();

        readonly IMyBroadcastListener AEGISListener;

        public static Program MyInstance;

        const string
            TURRET_BASE = "AEG-",
            MY_PREFIX = "AEGIS";

        string content = "";

        int timeNo = 0,
            ticksWOOrders = 0; // ticks w/o orders

        string GetFullScriptName(string ScriptName) { return "[" + ScriptName + "] Script"; }
        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = GetFullScriptName(ScriptName);
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText("\n\n" + ScriptName);
        }

        public static bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        bool SetRadarController() {
            /// this should be okay if there are no ships with Radar Control Script docked to the main ship
            Radar_Controller = GridTerminalSystem.GetBlockWithName(GetFullScriptName(RADAR_CONTROLLER_SCRIPT_NAME)) as IMyProgrammableBlock;

            /// if the programmable block we picked is not from this ship, we commence the search to find it anyway
            if (Radar_Controller != null && !AreOnSameGrid(Me, Radar_Controller)) {
                List<IMyProgrammableBlock> temp = new List<IMyProgrammableBlock>();
                Radar_Controller = null;
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyProgrammableBlock prog in temp) {
                    if (AreOnSameGrid(prog, Me) && prog.CustomName.Equals(GetFullScriptName(RADAR_CONTROLLER_SCRIPT_NAME))) {
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
                if (AreOnSameGrid(Me, controller)) {
                    if (Ship_Controller == null || controller.IsMainCockpit) {
                        Ship_Controller = controller;
                        if (controller.IsMainCockpit) return true;
                    }
                }
            }

            return Ship_Controller != null;
        }

        public void Save() {
            Storage = AEGIS.UseGenericDataOnly + ";" + AEGIS.IsOnline;
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
                AEGIS.UseGenericDataOnly = bool.Parse(data[i++]);
                AEGIS.IsOnline = bool.Parse(data[i++]);
            }
            catch (Exception e) {
                e.ToString();
                int f = 0;
                if (++f >= i) AEGIS.UseGenericDataOnly = false;
                if (++f >= i) AEGIS.IsOnline = true;
            }
        }

        public void GetMeTheTurrets() {
            List<IMyProgrammableBlock>
                temp = new List<IMyProgrammableBlock>();
            turrets = new List<IMyProgrammableBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyProgrammableBlock block in temp) {
                if (AreOnSameGrid(block, Me) && block.CustomName.Contains(TURRET_BASE) && !block.Equals(Me))
                    turrets.Add(block);
            }

            List<IMyLargeTurretBase>
                tmp = new List<IMyLargeTurretBase>();
            genericTurrets = new List<IMyLargeTurretBase>();

            GridTerminalSystem.GetBlocksOfType(tmp);
            foreach (IMyLargeTurretBase block in tmp) {
                if (AreOnSameGrid(block, Me))
                    genericTurrets.Add(block);
            }
        }

        void SendToAEGIS(MyDetectedEntityInfo entity) {SendToAEGIS(new Entry(entity)); }
        void SendToAEGIS(Entry entry) { SendToAEGIS(entry.Position, entry.Velocity, entry.Id.ToString()); }
        void SendToAEGIS(Vector3D vec, Vector3D vec2, string tag) {SendCoords(vec.X, vec.Y, vec.Z, vec2.X, vec2.Y, vec2.Z, tag);}
        void SendCoords(double X1, double Y1, double Z1, double X2 , double Y2 , double Z2 , string tag) { IGC.SendBroadcastMessage(tag, "TARSET;" + X1 + ";" + Y1 + ";" + Z1 + ";" + X2 + ";" + Y2 + ";" + Z2); }



        void BumpMyMissile(int missNo) {
            foreach (Job job in schedule) {
                if (job.misNo == missNo && job.type == JobType.Launch) {
                    job.TTJ = 1;
                    SortJobs();
                }
            }
        }

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
        }

        void ProcessJobs() {
            if (schedule.Count > 0 && schedule[0].TTJ <= 0) {
                Job curr = schedule[0];
                schedule.RemoveAt(0);
                string name = "ANTI-";
                IMyDoor antiDoor = GridTerminalSystem.GetBlockWithName("Anti Door " + curr.misNo) as IMyDoor;
                switch (curr.type) {
                    case JobType.OpenDoor:
                            if (antiDoor != null) antiDoor.OpenDoor();
                        break;

                    case JobType.Launch:
                        IMyProgrammableBlock missile = GridTerminalSystem.GetBlockWithName(name + curr.misNo) as IMyProgrammableBlock;
                        if (missile == null) {
                            string message = "ABORTING LAUNCH: MISSILE DOES NOT EXIST: \"" + name + curr.misNo + "\"";
                            //ErrorOutput(message);
                            return;
                        }
                        else {
                            Entry target;
                            long id;

                            if (curr.code.Length > 0 && long.TryParse(curr.code, out id) && AEGIS.TryGetAM(id, out target)) {
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
            public int      misNo, // set if the job is allocated to a specific missile
                            TTJ; // "TicksToJob"


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
            SortJobs();
        }

        public bool PrepareForLaunch(string code, int launchSize = 1) {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            int counter = 0;
            foreach (IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("ANTIMISSILE-")) {
                    string toParse = pb.CustomName.Substring(12);
                    int missNo;
                    try { missNo = int.Parse(toParse); }
                    catch (Exception e) { missNo = 0; Output("PrepareForAntiLaunch: " + e.ToString(), true); }
                    if (missNo != 0) {
                        pb.CustomName = "ANTI-" + missNo;
                        AddAJob(new Job(JobType.OpenDoor, 10 + counter * 10, missNo));
                        AddAJob(new Job(JobType.Launch, /*200 +*/ counter * 10, missNo, code));
                        AddAJob(new Job(JobType.CloseDoor, 75 + counter * 10, missNo));
                    }
                    else continue;
                    if (launchSize != 1 && ++counter >= launchSize) return true;
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

        public void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (AreOnSameGrid(Me, screen) && screen.CustomName.Contains("[AEGIS]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) {
                //if (screen.GetText().Length > 0) 
                    screen.WriteText(message, append);
            }
        }

        string Stringify(Entry entity, bool forMe = true) {
            return
                (forMe ? (entity.Relation.ToString().Substring(0, 1)) : "") + entity.Id.ToString() + ";" +
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
        /*
                if (entry.Threat > 0d) {
                    targets.Add(entry);
                }
                else if (entry.Threat == 0) {
                }
                else {
                    Echo(entry.Comment);
                    Output(entry.Comment + "\n");
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Function(false);
                    return "";
                }*/

        public string ParseInput(string input) {
            AEGIS.ClearCGT();
            string output = "";
            string[] content = input.Split('\n');
            for (int i = 0; i < content.Length; i++) {
                try {AEGIS.Add(new Entry(content[i]));}
                catch(Exception e) {
                    string comment = e.ToString();
                    Echo("Error: "+comment);
                    Output("Error: " + comment + "\n");
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Function(false); 
                    return "";
                }
            }

            targets = AEGIS.GetSortedList();
            foreach (Entry ent in targets) output += Stringify(ent, false) + "\n";
            return output;
        }

        public void Function(bool yes) {
            AEGIS.IsOnline = yes;
            if (yes) {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else {
                AEGIS.UseGenericDataOnly = true;
                //Echo("Shutting down.");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                ShareDirectCommand("tar");
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {
                        case "reg":
                            if (AEGIS.UseGenericDataOnly) {
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
                                AEGIS.UseGenericDataOnly = !AEGIS.UseGenericDataOnly;
                            else {
                                switch (args[1]) {
                                    case "on":
                                    case "up":
                                    case "true":
                                        if (!AEGIS.IsOnline) Function(true);
                                        AEGIS.UseGenericDataOnly = false;
                                        break;

                                    case "off":
                                    case "down":
                                    case "false":
                                        if (!AEGIS.IsOnline) Function(true);
                                        AEGIS.UseGenericDataOnly = true;
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
            else {
                //Output(!AEGIS.UseGenericDataOnly + " " + turrets.Count + " " + AEGIS.GetTarCount());

                if (content.Length > 0 && !AEGIS.UseGenericDataOnly) {
                    ProcessData(content + GetGenericTargettingData());
                    ticksWOOrders = 0;
                    content = "";
                }
                else {
                    if (ticksWOOrders >= 10 || AEGIS.UseGenericDataOnly) {
                        ProcessData(GetGenericTargettingData());
                    }
                    else ticksWOOrders++;
                }

                if (timeNo++ >= 120) {
                    timeNo = 0;
                    GetMeTheTurrets();
                }

                ProcessJobs();
                Output("");
            }
        }

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
                    this.Threat = 0;
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

        class AEGIS {
            public static bool IsOnline = true;                         /// All Functionality is on
            public static bool UseGenericDataOnly = false;              /// Radar is Off = true && Radar is ON = true
            public static double AntiMissileLanuchThreshhold = 100d;

            private static Dictionary<long, Entry> AMTargets = new Dictionary<long, Entry>();   // Anti Missiles Targets
            private static Dictionary<long, Entry> CGTargets = new Dictionary<long, Entry>();    // Contact Guns Targets

            public static void ClearCGT() {CGTargets.Clear();}

            public static void ClearAMT() {AMTargets.Clear();}

            public static int GetTarCount() {
                return AMTargets.Count + CGTargets.Count;
            }

            private static void BroadcastInfo(Entry entry) {
                Program.MyInstance.SendToAEGIS(entry);
            }

            private static void LaunchMissile(Entry entry) {
                Program.MyInstance.PrepareForLaunch(entry.Id.ToString());
            }

            private static double CalculateThreat(Entry entry) {
                if(!entry.Relation.Equals(Relation.FRIEND) && (Ship_Controller!=null || Program.MyInstance.SetShipController())) {
                    Vector3D
                        EnPos = entry.Position,
                        EnVel = entry.Velocity,
                        MyPos = Ship_Controller.GetPosition(),
                        MyVel = Ship_Controller.GetShipVelocities().LinearVelocity;

                    double
                        EnSpd = EnVel.Length(),
                        MySpd = MyVel.Length();

                    // since we've written down lengths of the velocity vectors, we can safely normalize them now.
                    EnVel = Vector3D.Normalize(EnVel);
                    MyVel = Vector3D.Normalize(MyVel);

                    if (EnSpd <= 10) { return 1d; } // the object either does not move or moves at low speeds, so it is not an active danger to the ship (probably)

                    else {
                        Vector3D
                            DangerousHeading,
                            AbsDev1, // Absolute Deviation, a Vector between Enemy-Us and Enemy-Estimated position after SecondsToImpact seconds
                            AbsDev2, 
                            EstEnPos;

                        double
                            Distance,
                            SecondsToImpact;

                        // Variant One - Detecting blindly following objects (also true if the ship is stationary)
                        DangerousHeading = Vector3D.Subtract(EnPos, MyPos); Distance = DangerousHeading.Length(); DangerousHeading = Vector3D.Normalize(DangerousHeading);
                        SecondsToImpact = Distance / EnSpd;
                        EstEnPos = Vector3D.Add(EnPos, Vector3D.Multiply(EnVel, EnSpd * SecondsToImpact));
                        AbsDev1 = Vector3D.Subtract(EstEnPos, MyPos); // 500m or less should probably trigger a response

                        // Variant Two - Detecting objects that either predict or will find themselves on the path of the ship
                        Vector3D MyProjPos = applyTarSpd(EnPos, Vector3D.Multiply(EnVel, EnSpd), MyPos, Vector3D.Multiply(MyVel, MySpd));
                        Distance = Vector3D.Subtract(MyPos,MyProjPos).Length();
                        SecondsToImpact = Distance / MySpd;
                        EstEnPos = Vector3D.Add(EnPos, Vector3D.Multiply(EnVel, EnSpd * SecondsToImpact));
                        AbsDev2 = Vector3D.Subtract(EstEnPos, MyProjPos);

                        if (!entry.Relation.Equals(Relation.FRIEND) && (AbsDev1.Length() <= 500 || AbsDev2.Length() <= 500)) {
                            AddToCGT(entry);
                        }

                        double 
                            baseThreat = entry.Relation.Equals(Relation.HOSTILE) ? 100d : 10d, threat,
                            adjAbsDev  = AbsDev2.Length();

                        adjAbsDev = adjAbsDev > 25 ? adjAbsDev : 25;

                        threat = (baseThreat * EnSpd) / (adjAbsDev * SecondsToImpact);

                        return threat;
                    }


                }
                else {
                    return entry.Relation.Equals(Relation.HOSTILE) ? 1d : 0d;
                }
            }

            private static void AddToCGT(Entry entry) {
                if (CGTargets.ContainsKey(entry.Id)) {
                    BroadcastInfo(entry);
                }
                else {
                    CGTargets.Add(entry.Id, entry);
                    LaunchMissile(entry);
                }
            }

            public static void Add(Entry entry){
                if (entry.Threat < 0d) throw new Exception(entry.Comment);
                entry.Threat = CalculateThreat(entry);
                if (CGTargets.ContainsKey(entry.Id)) CGTargets.Remove(entry.Id);
                CGTargets.Add(entry.Id, entry);
            }

            public static bool TryGetAM(long id, out Entry entry) {
                if (AMTargets.TryGetValue(id, out entry))
                    return true;
                else
                    return false;
            }

            public static bool TryGetCG(long id, out Entry entry) {
                if (CGTargets.TryGetValue(id, out entry))
                    return true;
                else
                    return false;
            }

            public static List<Entry> GetSortedList() {
                List<Entry> output = new List<Entry>();

                foreach(Entry entry in CGTargets.Values)
                        if(entry.Threat>0) output.Add(entry);

                return output.OrderByDescending(o => o.Threat).ToList();
            }

            static Vector3D NOTHING = new Vector3D(44, 44, 44);
            static Vector3D applyTarSpd(Vector3D position, Vector3D speed, Vector3D myPosition, Vector3D myVel) {
                double
                    mySpeed = myVel.Length(),
                    enSpeed = speed.Length(),
                    multiplier;

                position = Vector3D.Add(position, Vector3D.Multiply(speed, 1 / 60));

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
            static Vector3D GetProjectedPos(Vector3D enPos, Vector3D enSpeed, Vector3D myPos, Vector3D mySpeed) {
                /// do not enter if enSpeed is a "0" vector, or if our speed is 0
                Vector3D
                    A = myPos,
                    B = enPos;

                double
                    t = mySpeed.Length() / enSpeed.Length(),        //t -> b = a*t  
                    projPath,//b
                    dist = Vector3D.Distance(A, B),         //c
                    cos = InterCosine(enSpeed, Vector3D.Subtract(enPos, myPos)),

                    delta = 4 * dist * dist * ((1 / (t * t)) + (cos * cos) - 1);

                if (delta < 0) {
                    return NOTHING;
                }
                else
                if (delta == 0) {
                    if (t == 0) {
                        return NOTHING;
                    }
                    projPath = -1 * (2 * dist * cos) / (2 * (((t * t) - 1) / (t * t)));
                }
                else {
                    if (t == 0) {
                        return NOTHING;
                    }
                    else
                    if (t == 1) {
                        projPath = (dist) / (2 * cos);
                    }
                    else {
                        projPath = ((2 * dist * cos - Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                        if (projPath < 0) {
                            projPath = ((2 * dist * cos + Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                        }
                    }

                }
                mySpeed = Vector3D.Normalize(mySpeed);
                mySpeed = Vector3D.Multiply(mySpeed, projPath);

                return Vector3D.Add(myPos, mySpeed);
            }
            static double InterCosine(Vector3D first, Vector3D second) {
                double
                    scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                    productOfLengths = first.Length() * second.Length();

                return scalarProduct / productOfLengths;
            }
        }
    }
}
