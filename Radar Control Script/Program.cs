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

        string  RadarCode   = "[RADAR]";
        int     MAX_WAIT    = 900;
        float   currExt, currRPM;

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

        public class Entry {
            public long id;
            public int timeNum;
            public Vector3D location;
            public MyDetectedEntityType type;
            public MyRelationsBetweenPlayerAndBlock relation;

            public void Increment() {
                this.timeNum++;
            }

            public Entry(MyDetectedEntityInfo entity) {
                this.timeNum    = 0;
                this.id         = entity.EntityId;
                this.location   = entity.Position;
                this.type       = entity.Type;
                this.relation   = entity.Relationship;
            }
        }

        public class Register {
            static List<Entry> content = new List<Entry>();
            static IMyProgrammableBlock Me;
            static int    MAX_WAIT;

            public static void SetMe(IMyProgrammableBlock Me) { Register.Me = Me;  }
            public static void SetMax(int MAX) { Register.MAX_WAIT = MAX; }

            public static void Add(MyDetectedEntityInfo entity) {
                int current = 0, target = -1;
                foreach (Entry ent in content) {
                    if (ent.id == entity.EntityId) {target = current; break;}
                    current++;
                }
                Entry temp = new Entry(entity);
                if (target == -1) {content.Add(temp);}
                else {content[target] = temp;}
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

            static string Convert(Vector3D location) {
                double temporal = Vector3D.Subtract(Me.CubeGrid.GetPosition(), location).Length();
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
        }

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
                    foreach (MyDetectedEntityInfo entity in Detected) Register.Add(entity);
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

            float detThcc = (2.5f * (float)(Math.PI) * detExt * tarRPM / 7200f);

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

        void Output(object input) {
            bool AllRight = true;
            string message = input is string ? (string)input : input.ToString();
            foreach(IMyTextPanel screen in Screens) {
                if (screen != null) {
                    screen.ContentType = ContentType.TEXT_AND_IMAGE;
                    screen.WriteText(message);
                }
                else AllRight = false;
            }
            //Echo(message);
            if (!AllRight) GetScreens();
        }

        public void Main(string argument, UpdateType updateSource) {
            if((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0) {
                Detect();
                Output(PrintOut() + "\n" + Register.PrintOut());
                Register.IncrementAll();
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
                                        DetectAsteroids = true;
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
