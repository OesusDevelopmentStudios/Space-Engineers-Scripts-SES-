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

        //////////////////// SHIP TRACKING SCRIPT ///////////////////////

        IMyShipController control;
        IMyMotorStator XROT;
        IMyMotorStator YROT;    
        IMyCameraBlock camera;
        IMySoundBlock soundBlock;

        Vector3D curTarget, NOTHING = new Vector3D(0,0,0);
        static int DEFAULT_STATE_NR = 60 * 14;
        int     multi = 1, lnchNo = 0, maxMssle = 0, timeNr = 0, repeats = 0, stateNr = -1;
        string  missileTag = "MISSILE-CHN", misCMDTag = "MISSILE_COMMAND-CHN",
                screenName = "Missile Computer Screen";

        IMyBroadcastListener
            misCMDListener;

        Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            setUp();
        }

        public void setUp() {
            control     = GridTerminalSystem.GetBlockWithName("Targetting Ray/Remote"       ) as IMyShipController;
            XROT        = GridTerminalSystem.GetBlockWithName("Targetting Ray/Yaw Rotor"    ) as IMyMotorStator;
            YROT        = GridTerminalSystem.GetBlockWithName("Targetting Ray/Pitch Rotor"  ) as IMyMotorStator;
            camera      = GridTerminalSystem.GetBlockWithName("Targetting Ray/Camera"       ) as IMyCameraBlock;
            soundBlock  = GridTerminalSystem.GetBlockWithName("Targetting Ray/Sound Block"  ) as IMySoundBlock;
            if (camera != null && camera.EnableRaycast==false) camera.EnableRaycast = true;

            misCMDListener = IGC.RegisterBroadcastListener(misCMDTag);
            misCMDListener.SetMessageCallback();
        }

        public void play() {if (soundBlock != null) soundBlock.Play();}

        public Vector3D CastARay() {
            MyDetectedEntityInfo info = camera.Raycast(6000d, 0f, 0f);
            bool hit = false;
            if (!info.IsEmpty()) {
                if (
                    info.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies &&
                    info.Type == MyDetectedEntityType.LargeGrid
                    ) {
                    curTarget = info.HitPosition!=null? (Vector3D)info.HitPosition:info.Position;
                    hit = true;
                }
            }
            else curTarget = NOTHING;

            if (hit) {
                play();
                prepareToLaunch();
            }

            return curTarget;
        }

        public void prepareToLaunch() {
            List<IMyProgrammableBlock> progList = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(progList);
            maxMssle = 0;
            string rak = "";
            foreach(IMyProgrammableBlock pb in progList) {
                if (pb.CustomName.StartsWith("MISSILE-")) {
                    string  toParse = pb.CustomName.Substring(8);
                    int missNo;
                    try{missNo = int.Parse(toParse);} 
                    catch(Exception e)  {missNo = 0; rak += e.Message; }
                    maxMssle = maxMssle > missNo ? maxMssle : missNo;
                }
            }
            Output("maxMssle: "+maxMssle + " proglistsize:"+ progList.Count());
            lnchNo = 1;
        }

        public bool isOnThisGrid(IMyCubeGrid grid) {
            if (grid.Equals(Me.CubeGrid)) return true;
            return false;
        }

        public void antenaText(Object message) {
            /* nyope
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (isOnThisGrid(ant.CubeGrid)) { ant.Radius = 50000f; ant.CustomName = text; }
            /**/
        }

        public void Output(Object input) {
            string message = input is string ? (string)input : input.ToString();

            IMyTextSurface screen = GridTerminalSystem.GetBlockWithName(screenName) as IMyTextSurface;
            if (screen != null) {
                screen.ContentType = ContentType.TEXT_AND_IMAGE;
                screen.FontSize = 1f;
                screen.Font = "Monospace";
                screen.WriteText(message);
            }
            else Echo(message);
        }
        
        public void launch(int missileNo) {
            IMyAirtightHangarDoor siloDoor  = GridTerminalSystem.GetBlockWithName("Silo Door "+missileNo) as IMyAirtightHangarDoor;
            IMyProgrammableBlock missile    = GridTerminalSystem.GetBlockWithName("MISSILE-" + missileNo) as IMyProgrammableBlock;
            if (curTarget == null || missile == null || siloDoor == null) {
                Output("ABORTING LAUNCH :" + (curTarget == null) +" "+ (missile == null) +" "+ (siloDoor == null));
                return;
            }
            siloDoor.OpenDoor();
            missile.Enabled = false;
            missile.Enabled = true;
            missile.TryRun("prep " + curTarget.X + " " + curTarget.Y + " " + curTarget.Z);
        }

        public void SendCoords(Vector3D vec) { SendCoords(vec.X,vec.Y,vec.Z); }
        public void SendCoords(double X, double Y, double Z) {IGC.SendBroadcastMessage(missileTag, "TARSET;" + X + ";" + Y + ";" + Z);}
        public void Main(string argument, UpdateType updateSource) {
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
                                    curTarget = new Vector3D(float.Parse(bits[1]), float.Parse(bits[2]), float.Parse(bits[3]));
                                    repeats = bits.Length > 4? int.Parse(bits[4])  : 0;
                                    stateNr = bits.Length > 5? int.Parse(bits[5])*6: DEFAULT_STATE_NR;
                                    prepareToLaunch();
                                }
                                break;

                            case "ABORT":
                                IGC.SendBroadcastMessage(missileTag, "ABORT");
                                break;
                        }
                        antenaText(bits[0].ToUpper() + " - " + data);
                    }
                }
                return;
            }

            string[] evals = argument.ToUpper().Split(' ');
            string eval;

            if (evals.Length == 0) eval = "";
            else eval = evals[0];

            switch (eval) {
                case "":
                    if (control == null || XROT == null || YROT == null || camera == null) setUp();
                    float X = control == null ? 0f : control.RotationIndicator.Y;
                    float Y = control == null ? 0f : control.RotationIndicator.X;

                    if (XROT != null) XROT.TargetVelocityRPM = (float)(X * multi) / 10f;
                    if (YROT != null) YROT.TargetVelocityRPM = (float)(Y * multi) / 10f;

                    if (maxMssle > 0) {
                        if (timeNr++ > 50) {
                            if (lnchNo <= maxMssle) launch(lnchNo++);
                            else {
                                int index = 1;
                                IMyAirtightHangarDoor door;
                                do {
                                    door = GridTerminalSystem.GetBlockWithName("Silo Door " + index++) as IMyAirtightHangarDoor;
                                    if (door != null && (door.Status != DoorStatus.Opening || door.Status != DoorStatus.Open)) {
                                        door.OpenDoor();
                                    }
                                } while (door!=null);
                                maxMssle = 0;
                            }
                            timeNr = 0;
                        }
                    }

                    if (stateNr > 0) stateNr--;
                    else
                    if (stateNr == 0) {
                        Output("Bombing "+(repeats-1)+" more times.");
                        prepareToLaunch();
                        if (--repeats <= 1) stateNr = -1;
                        else stateNr = DEFAULT_STATE_NR;
                    }

                    break;

                case "+":
                    if (multi < 20) multi++;
                    break;

                case "-":
                    if (multi > 1) multi--;
                    break;

                case "CAST":
                    CastARay();
                    break;

                case "LAUNCH":
                    if (evals.Length>=2)
                    if (curTarget != null) {launch(int.Parse(evals[1]));}
                    break;

                case "ABORT":
                    IGC.SendBroadcastMessage(missileTag, "ABORT");
                    break;

                case "GPSTRIKE":
                    if (evals.Length > 3) {
                        curTarget = new Vector3D(float.Parse(evals[1]), float.Parse(evals[2]), float.Parse(evals[3]));
                        repeats = 0;
                        prepareToLaunch();
                    }
                    else {
                        repeats = 0;
                        prepareToLaunch();
                    }
                    break;

                case "GPSBMBNG":
                    if (evals.Length > 3) {
                        curTarget = new Vector3D(float.Parse(evals[1]), float.Parse(evals[2]), float.Parse(evals[3]));
                        repeats = evals.Length>4? int.Parse(evals[4]):2;
                        stateNr = DEFAULT_STATE_NR;
                        prepareToLaunch();
                    }
                    break;

                case "COUNT":
                    int count = 0;
                    while (true) antenaText(count++); /// robi 2271 iteracji tego
            }
        }
    }
}
