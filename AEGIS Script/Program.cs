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

        List<IMyProgrammableBlock>  turrets;
        List<IMyLargeTurretBase>    genericTurrets;
        List<IMyTextPanel>          screens;
        List<Entry>                 targets;

        const string
            TURRET_BASE = "AEG-",
            MY_PREFIX   = "AEGIS";

        string content  = "";

        bool UseGenericDataOnly;

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

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        bool SetRadarControl() {
            /// this should be okay if there are no ships with Radar Control Script docked to the main ship
            Radar_Controller = GridTerminalSystem.GetBlockWithName(GetFullScriptName(RADAR_CONTROLLER_SCRIPT_NAME)) as IMyProgrammableBlock;

            /// if the programmable block we picked is not from this ship, we commence the search to find it anyway
            if (Radar_Controller != null && !AreOnSameGrid(Me, Radar_Controller)) {
                List<IMyProgrammableBlock> temp = new List<IMyProgrammableBlock>();
                Radar_Controller = null;
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach(IMyProgrammableBlock prog in temp) {
                    if (AreOnSameGrid(prog, Me) && prog.CustomName.Equals(GetFullScriptName(RADAR_CONTROLLER_SCRIPT_NAME))) {
                        Radar_Controller = prog; return true;
                    }
                }
            }
            /// and if we fail... welp, we can just inform the rest of the script that we can't do nothing
            return Radar_Controller != null;
        }

        public void Save() {
            Storage = UseGenericDataOnly+"";
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SetRadarControl();
            SayMyName(MY_PREFIX);
            GetMeTheTurrets();
            try {
                UseGenericDataOnly = bool.Parse(Storage);
            }
            catch(Exception e) {
                e.ToString();
                UseGenericDataOnly = false;
            }
        }

        public void GetMeTheTurrets() {
            List<IMyProgrammableBlock> 
                temp    = new List<IMyProgrammableBlock>();
                turrets = new List<IMyProgrammableBlock>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach(IMyProgrammableBlock block in temp) {
                if (AreOnSameGrid(block, Me) && block.CustomName.Contains(TURRET_BASE))
                    turrets.Add(block);
            }

            List<IMyLargeTurretBase>
                tmp             = new List<IMyLargeTurretBase>();
                genericTurrets  = new List<IMyLargeTurretBase>();

            GridTerminalSystem.GetBlocksOfType(tmp);
            foreach (IMyLargeTurretBase block in tmp) {
                if (AreOnSameGrid(block, Me))
                    genericTurrets.Add(block);
            }
        }

        public void ShareInfo(string data) {
            data = ParseInput(data);
            foreach(IMyProgrammableBlock block in turrets) {
                block.CustomData = data;
                block.TryRun("reg");
            }
        }

        public void Output(object input, bool append = false) {
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (AreOnSameGrid(Me, screen) && screen.CustomName.Contains("[TRRT]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) screen.WriteText(message, append);
        }

        string Stringify(Entry entity) {
            return 
                entity.Id.ToString() + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Z * 10)) + ";" +

                string.Format("{0:0.}", Math.Round((double)entity.Velocity.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Z * 10)) + "\n";
        }


        private string OfficialRelationToLetter(MyRelationsBetweenPlayerAndBlock relation) {
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
                entity.IsEmpty()?
                "":
                OfficialRelationToLetter(entity.Relationship) + entity.EntityId.ToString() + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round(entity.Position.Z * 10)) + ";" +

                string.Format("{0:0.}", Math.Round((double)entity.Velocity.X * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Y * 10)) + ";" +
                string.Format("{0:0.}", Math.Round((double)entity.Velocity.Z * 10)) + "\n";
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
            targets = new List<Entry>();
            string output = "";
            string[] content = input.Split('\n');
            Entry entry;
            for(int i=0; i < content.Length; i++) {
                entry = new Entry(content[i]);
                if (entry.Threat > 0d) {
                    targets.Add(entry);
                }
                else {
                    Me.CustomData = entry.Comment;
                    Function(false);
                    return "";
                }
            }
            targets = targets.OrderByDescending(o => o.Threat).ToList();
            foreach (Entry ent in targets) output += Stringify(ent)+"\n";
            return output;
        }

        public void Function(bool yes) {
            if (yes) {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                UseGenericDataOnly = false;
            }
            else {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                UseGenericDataOnly = true;
                ShareInfo("tar");
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {
                        case "reg":
                            if (UseGenericDataOnly) {
                                content = "";
                                return;
                            }
                            if (Radar_Controller == null && !SetRadarControl()) break;
                            
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
                                UseGenericDataOnly = !UseGenericDataOnly;
                            else {
                                switch (args[1]) {
                                    case "on":
                                    case "up":
                                    case "true":
                                        UseGenericDataOnly = false;
                                        break;

                                    case "off":
                                    case "down":
                                    case "false":
                                        UseGenericDataOnly = true;
                                        break;
                                }
                            }
                            break;
                    }
                }
            }
            else {
                Output("");
                if (content.Length > 0 && !UseGenericDataOnly) {
                    ShareInfo(content + GetGenericTargettingData());
                    ticksWOOrders = 0;
                    content = "";
                }
                else {
                    if (ticksWOOrders >= 10 || UseGenericDataOnly) {
                        ShareInfo(GetGenericTargettingData());
                    }
                    else ticksWOOrders++;
                }

                if (timeNo++ >= 120) {
                    timeNo = 0;
                    GetMeTheTurrets();
                }
            }
        }

        enum Relation {
            FRIEND,
            HOSTILE,
            NEUTRAL
        }

        class Entry {
            public long         Id;
            public Relation     Relation;
            public Vector3D     Position;
            public Vector3D     Velocity;
            public double       Threat;
            public string       Comment;

            public Entry(MyDetectedEntityInfo entity) {
                this.Id         = entity.EntityId;
                this.Relation   = OfficialRelationToRelation(entity.Relationship);
                this.Position   = entity.Position;
                this.Velocity   = entity.Velocity;
                this.Threat     = CalculateThreat();
                this.Comment    = "Generated with Official entity";
            }

            public Entry(string input) {
                string[] 
                    content;
                string 
                    temp    = input.Substring(1);
                    input   = input.Substring(0, 1);

                content = temp.Split(';');

                double  px, py, pz, vx, vy, vz;
                int     i = 0;

                try {
                    Relation = LetterToRelation(input);
                    px = double.Parse(content[i++])/10;
                    py = double.Parse(content[i++])/10;
                    pz = double.Parse(content[i++])/10;
                    vx = double.Parse(content[i++])/10;
                    vy = double.Parse(content[i++])/10;
                    vz = double.Parse(content[i++])/10;
                    Position = new Vector3D(px, py, pz);
                    Velocity = new Vector3D(vx, vy, vz);
                    Threat = CalculateThreat();
                }
                catch(Exception e) {
                    this.Relation   = Relation.NEUTRAL;
                    this.Position   = new Vector3D();
                    this.Velocity   = new Vector3D();
                    this.Comment    = e.ToString();
                    this.Threat     = -1;
                }
            }

            private double CalculateThreat() {

                return Relation.Equals(Relation.HOSTILE)? 1d:0d;
            }

            private Relation OfficialRelationToRelation(MyRelationsBetweenPlayerAndBlock relation){
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

                    default: throw new Exception("Letter is not one of the base ones: 'F','N','H': '"+letter+"'.");
                }
            }
        }

    }
}
