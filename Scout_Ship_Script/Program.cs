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

        //////////////////// SCOUT SHIP SCRIPT ///////////////////////

        IMyRemoteControl
                        control;
        IMyMotorStator  XROT;
        IMyMotorStator  YROT;
        IMyMotorStator  YROTA;
        IMyCameraBlock  camera;
        IMySoundBlock   soundBlock;

        static string shipId = "", misCMDTag = "MISSILE_COMMAND-CHN";


        Vector3D        curTarget,
                        NOTHING = new Vector3D(0, 0, 0);

        public Program() {Runtime.UpdateFrequency = UpdateFrequency.Update1;}
        public void play() { if (soundBlock != null) soundBlock.Play(); }
        public bool isOnThisGrid(IMyCubeGrid G) {
            if (G == Me.CubeGrid) return true;
            else return false;
        }

        public void antenaText(Object message) {
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (isOnThisGrid(ant.CubeGrid)) { ant.Radius = 50000; ant.CustomName = text; }
        }

        public void setUp() {
            if (shipId.Length > 0) shipId = shipId + " ";
            control     = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Remote"       ) as IMyRemoteControl;
            XROT        = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Yaw Rotor"    ) as IMyMotorStator;
            YROT        = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Pitch Rotor"  ) as IMyMotorStator;
            YROTA       = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Anti Pitch Rotor"  ) as IMyMotorStator;
            camera      = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Camera"       ) as IMyCameraBlock;
            soundBlock  = GridTerminalSystem.GetBlockWithName(shipId + "Targetting Ray/Sound Block"  ) as IMySoundBlock;
            if (camera != null && camera.EnableRaycast == false) camera.EnableRaycast = true;
            if (control != null) {
                control.ControlThrusters = false;
            }
        }

        public Vector3D CastARay() {
            MyDetectedEntityInfo info = camera.Raycast(6000d, 0f, 0f);
            if (!info.IsEmpty()) {
                if (
                    info.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies /*&&
                    info.Type == MyDetectedEntityType.LargeGrid/**/
                    ) {
                    curTarget = info.HitPosition!=null? (Vector3D)info.HitPosition:info.Position;
                    play();
                    antenaText("Got new Target: " + string.Format("{0:0.##}", curTarget.X) + " " + string.Format("{0:0.##}", curTarget.Y) + " " + string.Format("{0:0.##}", curTarget.Z));
                }
            }
            else curTarget = NOTHING;

            return curTarget;
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & UpdateType.Update1) > 0) {
                if (control == null || XROT == null || YROT == null || YROTA == null || camera == null) setUp();

                    YROT.UpperLimitDeg = 30;
                    YROT.LowerLimitDeg = -210;

                    YROTA.UpperLimitDeg = 210;
                    YROTA.LowerLimitDeg = -30;

                if (control!=null&&control.IsUnderControl) {
                    XROT.UpperLimitDeg = float.MaxValue;
                    XROT.LowerLimitDeg = float.MinValue;

                    XROT.TargetVelocityRPM = 0;
                    YROT.TargetVelocityRPM = 0;
                    YROTA.TargetVelocityRPM = 0;

                    float X = control == null ? 0f : control.RotationIndicator.Y;
                    float Y = control == null ? 0f : control.RotationIndicator.X;

                    XROT.RotorLock = false;
                    YROT.RotorLock = false;
                    YROTA.RotorLock = false;

                    if (XROT != null) XROT.TargetVelocityRPM =  (X / 5f);
                    if (YROT != null) YROT.TargetVelocityRPM =  (Y / 5f);
                    if (YROTA != null) YROTA.TargetVelocityRPM = -(Y / 5f);
                }
                else {
                    XROT.UpperLimitDeg =  1;
                    XROT.LowerLimitDeg =  0;

                    if (XROT.Angle > -1 && XROT.Angle < 3) {
                        if (!XROT.RotorLock) {
                            XROT.TargetVelocityRPM = 0;
                            XROT.RotorLock = true;
                        }
                    }
                    else {
                        XROT.TargetVelocityRPM = 10;
                        XROT.RotorLock = false;
                    }

                    if (YROT.Angle > -2 && YROT.Angle < 2) {
                        if (!YROT.RotorLock) {
                            YROT.TargetVelocityRPM = 0;
                            YROTA.TargetVelocityRPM= 0;
                            YROT.RotorLock = true;
                            YROTA.RotorLock = true;
                        }
                    }
                    else {
                        YROT.TargetVelocityRPM = +10;
                        YROTA.TargetVelocityRPM =-10;
                        YROT.RotorLock = false;
                        YROTA.RotorLock = false;
                    }
                }
            }
            else {
                string[] evals = argument.ToLower().Split(' ');
                string eval;

                if (evals.Length == 0) eval = "";
                else eval = evals[0].ToLower();

                switch (eval) {
                    case "cast":
                        CastARay();
                        break;

                    case "send":
                        if (curTarget != null && !curTarget.Equals(NOTHING)) {
                            string message1 = "salvo;" + string.Format("{0:0.##}", curTarget.X) + ";" + string.Format("{0:0.##}", curTarget.Y) + ";" + string.Format("{0:0.##}", curTarget.Z);
                            IGC.SendBroadcastMessage(misCMDTag, message1);
                        }
                        break;

                    case "onme":
                        string message2 = "salvo;" + string.Format("{0:0.##}", Me.Position.X) + ";" + string.Format("{0:0.##}", Me.Position.Y) + ";" + string.Format("{0:0.##}", Me.Position.Z);
                        IGC.SendBroadcastMessage(misCMDTag, message2);
                        break;

                    case "abort":
                        IGC.SendBroadcastMessage(misCMDTag, "abort");
                        antenaText("Aborted");
                        break;

                    default: break;
                }
            }
        }
    }
}
