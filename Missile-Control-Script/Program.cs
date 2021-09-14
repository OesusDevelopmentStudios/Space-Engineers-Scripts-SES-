using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {
        //////////////////// MISSILE CONTROL SCRIPT ///////////////////////
        /// Constants

        const string    SCRIPT_VERSION = "v7.1.0";
        const int       MIN_SUCC_CAMERAS = 9;
        readonly bool   DEFAULT_DAMPENERS_SETTING = false,
                        THIS_MISSILE_IS_AN_ANTIMISSILE = false;

        MISSILE_STATE CurrentState;

        IMyBroadcastListener
                missileListener;
        readonly string misCMDTag = "MISSILE_COMMAND-CHN";
        string missileTag = "MISSILE-CHN";

        Vector3D  UPP_CMD = new Vector3D( 0, -1,  0),
                  DWN_CMD = new Vector3D( 0,  1,  0),
                  LFT_CMD = new Vector3D(-1,  0,  0),
                  RIG_CMD = new Vector3D( 1,  0,  0),
                  CLK_CMD = new Vector3D( 0,  0,  1),
                  ALK_CMD = new Vector3D( 0,  0, -1),

                  NOTHING = new Vector3D(44, 44, 44),
                  target,
                  cameras_target;

        const int FW_VAL = 2,
                  UP_VAL = 6,
                  LF_VAL = 4,
                  RT_VAL = 3,
                  BW_VAL = 1,
                  DW_VAL = 5;

        /// END OF CONSTANTS

        int timeIndex = 0, ticksSinceLastOrder = 0;
        readonly int myNumber = 0;

        double strtSPD = -1d,
                strtELV = -1d,
                currentMissileElevation = -1d,
                lastDist = 999999,

                maxSpeed = 256d,
                addSPDNeed = 100d,
                maxSPDDev = 30d;
        readonly double
                ACT_DIST,
                maxDeviation;

        bool    useMNV = false,
                missileIsInGravityWell = false,
                controllerExistsAndWorking = false,
                missileHasPendingOrders = false,
                missileChangedTarget = false,
                skipThisTick = false,
                mbOrbital = false;


        List<IMyShipController> ControlList = new List<IMyShipController>();
        readonly List<IMyShipMergeBlock> MergerList = new List<IMyShipMergeBlock>();
        readonly List<IMyShipConnector> ConnecList = new List<IMyShipConnector>();
        readonly List<IMyBatteryBlock> BattryList = new List<IMyBatteryBlock>();
        readonly List<IMyGasTank> HTankList = new List<IMyGasTank>();
        List<IMyCameraBlock> MissileCameras = new List<IMyCameraBlock>();

        class NavPrompt {
            public int dirInt;
            public double vLength;

            public NavPrompt(int dir, Vector3D input) {
                this.dirInt = dir;
                this.vLength = input.Length();
            }
        }

        enum MISSILE_STATE {
            INITIALIZING,
            PREPARING_FOR_LAUNCH,
            EXITING_LAUNCHPORT,
            GRAV_ALGN,
            DAMPENING,
            APPROACHING_TARGET,
            MANUAL
        }

        void CutAnchor(int ticksToLaunch) {
            if (ticksToLaunch == 120) { foreach (IMyShipConnector con in ConnecList) { if (con.CustomName.Equals("Missile/Refuel Connector")) con.Enabled = false; } }
            else if (ticksToLaunch == 80) { foreach (IMyGasTank tank in HTankList) { tank.Stockpile = false; } }
            else if (ticksToLaunch == 40) { foreach (IMyBatteryBlock batt in BattryList) { batt.ChargeMode = ChargeMode.Auto; } }
            else if (ticksToLaunch <= 0) {
                foreach (IMyShipMergeBlock mer in MergerList) { if (mer.CustomName.Equals("Missile/DMerge Block")) mer.Enabled = false; }
            }
        }

        bool IsOnThisGrid(IMyCubeBlock G) {
            if (G.CubeGrid.Equals(Me.CubeGrid)) return true;
            else return false;
        }

        bool IsVerySimilar(double d1, double d2) {
            if (d1 == d2) return true;
            double first = d1 > d2 ? d1 : d2,
                   second = d1 < d2 ? d1 : d2;

            if (first - second < (first / 10)) return true;
            else return false;
        }

        void ChangeState(MISSILE_STATE state) {
            //Output("Changing mode from " + CurrentState + " to " + state.ToString() + ".");
            timeIndex = 0;
            List<IMyThrust> group;
            switch (state) {
                case MISSILE_STATE.INITIALIZING:
                    ResetThrust();
                    MoveAllGyros(0, 0, 0);
                    OverrideGyros(false);

                    foreach (IMyShipMergeBlock mer in MergerList) { if (mer.CustomName.Equals("Missile/DMerge Block")) mer.Enabled = true; }
                    foreach (IMyShipConnector con in ConnecList) { if (con.CustomName.Equals("Missile/Refuel Connector")) con.Enabled = true; }

                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case MISSILE_STATE.PREPARING_FOR_LAUNCH:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.EXITING_LAUNCHPORT:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    //Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case MISSILE_STATE.GRAV_ALGN:
                    useMNV = false;
                    if (controllerExistsAndWorking) SHIP_CONTROLLER.DampenersOverride = false;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.DAMPENING:
                    ResetThrust();
                    if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
                    if (controllerExistsAndWorking) SHIP_CONTROLLER.DampenersOverride = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    if (missileListener == null) {
                        missileListener = IGC.RegisterBroadcastListener(missileTag);
                        missileListener.SetMessageCallback();
                    }
                    break;

                case MISSILE_STATE.APPROACHING_TARGET:
                    if (controllerExistsAndWorking) SHIP_CONTROLLER.DampenersOverride = false;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    if (THIS_MISSILE_IS_AN_ANTIMISSILE) {
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 1f);
                    }
                    else useMNV = false;

                    if (missileListener == null) {
                        missileListener = IGC.RegisterBroadcastListener(missileTag);
                        missileListener.SetMessageCallback();
                    }
                    break;

                case MISSILE_STATE.MANUAL:
                    Runtime.UpdateFrequency = UpdateFrequency.Once;
                    ResetThrust(true);
                    OverrideGyros(false);
                    disToTarget = 50000d;
                    break;

                default:
                    Output("Function 'ChangeState': Undefined input value.");
                    return;
            }
            CurrentState = state;
        }

        float Multiplier() { return ((Runtime.UpdateFrequency == UpdateFrequency.Update1) && !THIS_MISSILE_IS_AN_ANTIMISSILE)? 3f:1f; }

        void ChangeState(string state) {
            Output("Changing mode from " + CurrentState + " to " + state + ".");
            switch (state.ToUpper()) {
                case "INIT": ChangeState(MISSILE_STATE.INITIALIZING); break;

                case "DAMP": ChangeState(MISSILE_STATE.DAMPENING); break;

                default: Output("Function 'ChangeState': Undefined input value."); break;
            }
        }

        IMyShipController SHIP_CONTROLLER;
        readonly Dictionary<int, List<IMyThrust>> THRUSTERS = new Dictionary<int, List<IMyThrust>>();

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            target = NOTHING;
            SayMyName(SCRIPT_VERSION);
            if(THIS_MISSILE_IS_AN_ANTIMISSILE) {
                Me.CubeGrid.CustomName = "Antimissile " + SCRIPT_VERSION;
                ACT_DIST = 100d;
                maxDeviation = 0.65d;
                misCMDTag = "AEGIS";
                addSPDNeed = 25d;
                maxSPDDev = 50d;
            }
            else {
                Me.CubeGrid.CustomName = "Antiship Missile " + SCRIPT_VERSION;
                ACT_DIST = 300d;
                maxDeviation = 0.02d;
                misCMDTag = "MISSILE_COMMAND-CHN";
                addSPDNeed = 100d;
                maxSPDDev = 30d;
            }

            List<IMyShipController> controls = new List<IMyShipController>();
            ControlList = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controls);
            foreach (IMyShipController cont in controls) { if (IsOnThisGrid(cont) && cont.IsWorking) ControlList.Add(cont); }

            ChangeState(MISSILE_STATE.INITIALIZING);
        }

        void SayMyName(string ScriptName, float textSize = 10f) {
            IMyTextSurface surface = Me.GetSurface(1);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        Vector3D CutVector(Vector3D vector) { return CutVector(vector, 3); }

        Vector3D CutVector(Vector3D vector, int decNo) {
            double X = Math.Round(vector.X, decNo),
                Y = Math.Round(vector.Y, decNo),
                Z = Math.Round(vector.Z, decNo);

            return new Vector3D(X, Y, Z);
        }

        void AntennaText(object message) {
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (IsOnThisGrid(ant)) { ant.Radius = CurrentState > MISSILE_STATE.EXITING_LAUNCHPORT ? 50000f : 1000f; ant.CustomName = text; }
        }

        // CONV

        string DirintToName(int dirint) {
            switch (dirint) {
                case 1:
                    return "FORWARD ";
                case 2:
                    return "BACKWARD";
                case 3:
                    return "LEFT    ";
                case 4:
                    return "RIGHT   ";
                case 5:
                    return "UP      ";
                case 6:
                    return "DOWN    ";
                default:
                    return "ERROR   ";
            }
        }

        Vector3D DirToCmd(int lndDir, int culprit) {
            if (lndDir <= 2) {
                if (culprit <= 4) {
                    if (lndDir % 2 == culprit % 2) return RIG_CMD;
                    else return LFT_CMD; /// LFT
                }
                else {
                    if (lndDir % 2 == culprit % 2) return DWN_CMD; /// DWN
                    else return UPP_CMD; /// UPP
                }
            }
            else if (lndDir <= 4) {
                if (culprit <= 4) {
                    if (lndDir % 2 == culprit % 2) return LFT_CMD; /// LFT
                    else return RIG_CMD; /// RIG
                }
                else {
                    if (lndDir % 2 == culprit % 2) return ALK_CMD; /// ALK
                    else return CLK_CMD; /// CLK
                }
            }
            else {
                if (culprit <= 2) {
                    if (lndDir % 2 == culprit % 2) return UPP_CMD; /// UPP
                    else return DWN_CMD; /// DWN
                }
                else {
                    if (lndDir % 2 == culprit % 2) return CLK_CMD; /// CLK
                    else return ALK_CMD; /// ALK
                }
            }
        }

        void DirToMnv(int lndDir, int culprit, float ovrPrc) {
            ResetThrust();
            List<IMyThrust> manThr;
            /**/
            if (lndDir == 1 && (culprit == 3 || culprit == 4)) {
                if (culprit == 3) { if (THRUSTERS.TryGetValue(4, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; } }
                else { if (THRUSTERS.TryGetValue(3, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; } }
            }
            else {
                if (THRUSTERS.TryGetValue(culprit, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; }
            }
            /**/

        }

        Vector3D DirintToVec(int dirint) {
            switch (dirint) {
                case 1: return SHIP_CONTROLLER.WorldMatrix.Forward;
                case 2: return SHIP_CONTROLLER.WorldMatrix.Backward;
                case 3: return SHIP_CONTROLLER.WorldMatrix.Left;
                case 4: return SHIP_CONTROLLER.WorldMatrix.Right;
                case 5: return SHIP_CONTROLLER.WorldMatrix.Up;
                case 6: return SHIP_CONTROLLER.WorldMatrix.Down;
            }
            return NOTHING;
        }

        // END OF CONV
        // GET

        bool ControllingBlockFoundAndApplied() {
            List<IMyShipController> temp = new List<IMyShipController>();
            foreach (IMyShipController cont in ControlList) { if (IsOnThisGrid(cont) && cont.IsWorking) temp.Add(cont); }
            ControlList = new List<IMyShipController>(temp);

            SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in ControlList) {
                if (controler.IsMainCockpit) {
                    SHIP_CONTROLLER = controler;
                    return true;
                }
            }

            foreach (IMyShipController controler in ControlList) {
                if (SHIP_CONTROLLER == null && controler.IsWorking && IsOnThisGrid(controler)) {
                    SHIP_CONTROLLER = controler;
                    controler.IsMainCockpit = true;
                }
                controler.DampenersOverride = DEFAULT_DAMPENERS_SETTING;
            }

            if (SHIP_CONTROLLER == null) {
                Output("Could not find any ship controller.");
                return false;
            }
            return true;
        }

        List<IMyGyro> GetGyros() {
            List<IMyGyro> list = new List<IMyGyro>();
            List<IMyGyro> temp = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyGyro gyro in temp) if (IsOnThisGrid(gyro)) list.Add(gyro);

            return list;
        }

        Vector3D FindTargetUsingCameras(int camIndx = 0) {
            IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("Missile/Camera" + (camIndx + 1)) as IMyCameraBlock;
            timeIndex = 0;
            if (camIndx > 8) {
                Output("\nOOC");
                return NOTHING;
            }
            if (camera == null) {
                return FindTargetUsingCameras(++camIndx);
            }
            Vector3D
                rayTG = cameras_target,
                addition;
            switch (camIndx) {
                case 0: break;
                case 1:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 50d);
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 2:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 50d);
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 3:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 50d);
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 4:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 50d);
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 5:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 35d));
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 6:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 35d));
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 7:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 35d));
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                case 8:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 35d));
                    rayTG = Vector3D.Add(rayTG, addition);
                    break;
                default: rayTG = NOTHING; break;
            }
            double distance = Vector3D.Distance(camera.GetPosition(), rayTG);
            if (!camera.CanScan(distance))
                camera = GetRdyCam(distance);

            if (camera != null) {
                MyDetectedEntityInfo target = camera.Raycast(rayTG);
                if (!target.IsEmpty() &&
                   ((target.Type == MyDetectedEntityType.LargeGrid || target.Type == MyDetectedEntityType.SmallGrid)
                   && target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)) {
                    Output("\nFOUND YA");
                    cameras_target = target.Position;
                    return ApplyTarSpd(target.Position, target.Velocity);
                }
                return FindTargetUsingCameras(++camIndx);
            }
            else {
                Output("\nNO RDY CAM");
                return NOTHING;
            }
        }
        /**/
        List<IMyCameraBlock> GetCameras() {
            List<IMyCameraBlock> list = new List<IMyCameraBlock>();
            List<IMyCameraBlock> temp = new List<IMyCameraBlock>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyCameraBlock cam in temp) {
                if (IsOnThisGrid(cam)) {
                    cam.EnableRaycast = true;
                    list.Add(cam);
                }
            }

            return list;
        }

        IMyCameraBlock GetRdyCam(double distance = 3000d) {
            for (int i = 9; i > 0; i--) {
                IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("Missile/Camera" + (i)) as IMyCameraBlock;
                if (camera == null || !camera.CanScan(distance)) continue;
                else { return camera; }
            }
            return null;
        }

        // END OF GET
        // FIND

        void FindThrusters() { FindThrusters(false); }

        void FindThrusters(bool output) {
            List<IMyThrust> temp;
            THRUSTERS.Clear();
            List<IMyThrust> list = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(list);
            foreach (IMyThrust t in list) {
                if (!IsOnThisGrid(t)) continue;
                int dirint = TranslateDirection(t);
                t.CustomName = DirintToName(dirint);
                if (THRUSTERS.TryGetValue(dirint, out temp)) {
                    temp.Add(t);
                    THRUSTERS.Remove(dirint);
                    THRUSTERS.Add(dirint, temp);
                }
                else {
                    temp = new List<IMyThrust> { t };
                    THRUSTERS.Add(dirint, temp);
                }
                //Echo(t.CustomName + " " + t.MaxThrust + "\n");
            }
            bool ok = true;
            if (output) {
                for (int i = 1; i < 7; i++) if (!THRUSTERS.ContainsKey(i)) { ok = false; Output("WARNING: NO " + DirintToName(i) + " THRUSTERS."); }
                if (ok) Output("All thrusters found.");
            }
        }

        // END OF FIND

        int TranslateOrientation(MyBlockOrientation o) {
            int translatedFW = TranslateDirection(o.Forward);
            int translatedUP = TranslateDirection(o.Up);
            if (translatedFW == 44 || translatedUP == 44) { Output("*ANGERY SIREN NOISES*"); return 444; }
            else
                return translatedFW * 10 + translatedUP;
        }

        int TranslateDirection(Base6Directions.Direction d) {
            switch (d) {
                case Base6Directions.Direction.Forward: return 1;
                case Base6Directions.Direction.Backward: return 2;
                case Base6Directions.Direction.Left: return 3;
                case Base6Directions.Direction.Right: return 4;
                case Base6Directions.Direction.Up: return 5;
                case Base6Directions.Direction.Down: return 6;
                default: Output("*ANGERY SIREN NOISES*"); return 44;
            }
        }

        int TranslateDirection(IMyCubeBlock block) {
            int TSL = SHIP_CONTROLLER == null ? 15 : TranslateOrientation(SHIP_CONTROLLER.Orientation);
            int TFW = (TSL / 10);
            int TUP = TSL - TFW * 10;
            if (block is IMyThrust) {
                int blockDir = TranslateDirection(block.Orientation.Forward);
                if (blockDir == TFW) return FW_VAL;
                if (blockDir == TUP) return UP_VAL;
                if (TFW % 2 == 0) {
                    if (blockDir == TFW - 1) return BW_VAL;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1) return DW_VAL;
                        else {
                            if (blockDir % 2 == 0) return RT_VAL;
                            else return LF_VAL;
                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return DW_VAL;
                        else {
                            if (blockDir % 2 == 0) return RT_VAL;
                            else return LF_VAL;
                        }
                    }
                }
                else {
                    if (blockDir == TFW + 1) return BW_VAL;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1) return DW_VAL;
                        else {
                            if (blockDir % 2 == 0) return LF_VAL;
                            else return RT_VAL;
                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return DW_VAL;
                        else {
                            if (blockDir % 2 == 0) return LF_VAL;
                            else return RT_VAL;
                        }
                    }
                }

            }
            else
            if (block is IMyGyro) {

                int blockDir = TranslateDirection(block.Orientation.Forward),
                    blockSub = TranslateDirection(block.Orientation.Up),
                    firstDigit;

                if (blockSub == TFW) firstDigit = 2;
                else if (blockSub == TUP) firstDigit = 6;
                else if (TFW % 2 == 0) {
                    if (blockSub == TFW - 1) firstDigit = 1;
                    else if (TUP % 2 == 0) {
                        if (blockSub == TUP - 1) firstDigit = 5;
                        else {
                            if (blockSub % 2 == 0) firstDigit = 3;
                            else firstDigit = 4;
                        }
                    }
                    else {
                        if (blockSub == TUP + 1) firstDigit = 5;
                        else {
                            if (blockSub % 2 == 0) firstDigit = 3;
                            else firstDigit = 4;
                        }
                    }
                }
                else {
                    if (blockSub == TFW + 1) firstDigit = 1;
                    else if (TUP % 2 == 0) {
                        if (blockSub == TUP - 1) firstDigit = 5;
                        else {
                            if (blockSub % 2 == 0) firstDigit = 4;
                            else firstDigit = 3;
                        }
                    }
                    else {
                        if (blockSub == TUP + 1) firstDigit = 5;
                        else {
                            if (blockSub % 2 == 0) firstDigit = 4;
                            else firstDigit = 3;
                        }
                    }
                }

                if (blockDir == TFW) return firstDigit * 10 + 2;
                else if (blockDir == TUP) return firstDigit * 10 + 6;
                else if (TFW % 2 == 0) {
                    if (blockDir == TFW - 1) return firstDigit * 10 + 1;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1)
                            return firstDigit * 10 + 5;
                        else {
                            if (blockDir % 2 == 0) return firstDigit * 10 + 3;
                            else return firstDigit * 10 + 4;
                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return firstDigit * 10 + 5;
                        else {
                            if (blockDir % 2 == 0) return firstDigit * 10 + 3;
                            else return firstDigit * 10 + 4;
                        }
                    }
                }
                else {
                    if (blockDir == TFW + 1) return firstDigit * 10 + 1;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1) return firstDigit * 10 + 5;
                        else {
                            if (blockDir % 2 == 0) return firstDigit * 10 + 4;
                            else return firstDigit * 10 + 3;

                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return firstDigit * 10 + 5;
                        else {
                            if (blockDir % 2 == 0) return firstDigit * 10 + 4;
                            else return firstDigit * 10 + 3;
                        }
                    }
                }
            }
            else return 0;
        }

        void MoveGyroInAWay(IMyGyro target, float Yaw, float Pitch, float Roll) {
            target.GyroOverride = true;
            Yaw *= Multiplier();
            Pitch *= Multiplier();
            Roll *= Multiplier();
            switch (TranslateDirection(target)) {
                case 13:
                    target.Yaw = Roll; target.Pitch = Yaw; target.Roll = Pitch;
                    break;
                case 14:
                    target.Yaw = Roll; target.Pitch = -Yaw; target.Roll = -Pitch;
                    break;
                case 15:
                    target.Yaw = Roll; target.Pitch = -Pitch; target.Roll = Yaw;
                    break;
                case 16:
                    target.Yaw = Roll; target.Pitch = Pitch; target.Roll = -Yaw;
                    break;
                case 23:
                    target.Yaw = -Roll; target.Pitch = -Yaw; target.Roll = Pitch;
                    break;
                case 24:
                    target.Yaw = -Roll; target.Pitch = Yaw; target.Roll = -Pitch;
                    break;
                case 25:
                    target.Yaw = -Roll; target.Pitch = Pitch; target.Roll = Yaw;
                    break;
                case 26:
                    target.Yaw = -Roll; target.Pitch = -Pitch; target.Roll = -Yaw;
                    break;
                case 31:
                    target.Yaw = Pitch; target.Pitch = -Yaw; target.Roll = -Roll;
                    break;
                case 32:
                    target.Yaw = -Pitch; target.Pitch = Yaw; target.Roll = Roll;
                    break;
                case 35:
                    target.Yaw = -Pitch; target.Pitch = -Roll; target.Roll = Yaw;
                    break;
                case 36:
                    target.Yaw = -Pitch; target.Pitch = Roll; target.Roll = -Yaw;
                    break;
                case 41:
                    target.Yaw = Pitch; target.Pitch = Yaw; target.Roll = -Roll;
                    break;
                case 42:
                    target.Yaw = Pitch; target.Pitch = Yaw; target.Roll = Roll;
                    break;
                case 45:
                    target.Yaw = Pitch; target.Pitch = Roll; target.Roll = Yaw;
                    break;
                case 46:
                    target.Yaw = Pitch; target.Pitch = -Roll; target.Roll = -Yaw;
                    break;
                case 51:
                    target.Yaw = -Yaw; target.Pitch = Pitch; target.Roll = -Roll;
                    break;
                case 52:
                    target.Yaw = -Yaw; target.Pitch = -Pitch; target.Roll = Roll;
                    break;
                case 53:
                    target.Yaw = -Yaw; target.Pitch = -Roll; target.Roll = Pitch;
                    break;
                case 54:
                    target.Yaw = -Yaw; target.Pitch = -Roll; target.Roll = -Pitch;
                    break;
                case 61:
                    target.Yaw = Yaw; target.Pitch = -Pitch; target.Roll = -Roll;
                    break;
                case 62:
                    target.Yaw = Yaw; target.Pitch = Pitch; target.Roll = Roll;
                    break;
                case 63:
                    target.Yaw = Yaw; target.Pitch = -Roll; target.Roll = Pitch;
                    break;
                case 64:
                    target.Yaw = Yaw; target.Pitch = -Roll; target.Roll = -Pitch;
                    break;
                default:
                    Output("ERROR: " + target.CustomName + " GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
                    target.ShowOnHUD = true;
                    break;
            }
        }

        void MoveAllGyros(float Yaw, float Pitch, float Roll) {
            List<IMyGyro> gyros = GetGyros();
            foreach (IMyGyro gyro in gyros) {
                MoveGyroInAWay(gyro, Yaw, Pitch, Roll);
            }
        }

        void MoveAGroupOfThrusters(List<IMyThrust> Group, float OverridePercent) {
            foreach (IMyThrust Thruster in Group) {
                Thruster.ThrustOverridePercentage = OverridePercent;
            }
        }

        void ResetThrust() {
            ResetThrust(false);
        }

        void ResetThrust(bool all) {
            List<IMyThrust> list;
            int i = all ? 0 : 2;
            for (; i < 7; i++)
                if (THRUSTERS.TryGetValue(i, out list))
                    foreach (IMyThrust tru in list) {
                        tru.ThrustOverride = 0f;
                    }
        }

        void OverrideGyros(bool doThat) {
            foreach (IMyGyro gyro in GetGyros()) {
                gyro.GyroOverride = doThat;
            }
        }

        void InitShip() {
            MissileCameras = GetCameras();
            IMyShipConnector refCon = null;
            List<IMyShipConnector> cons = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(cons);
            foreach (IMyShipConnector con in cons) {
                if (con.IsWorking && IsOnThisGrid(con) && con.CustomName.Equals("Missile/Refuel Connector")) {
                    refCon = con;
                    break;
                }
            }

            if (MissileCameras.Count < MIN_SUCC_CAMERAS || refCon == null || refCon.Status != MyShipConnectorStatus.Connected) return;
            else {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                //if (!Me.CustomName.StartsWith("ANTIMISSILE-"))
                Me.CustomName = String.Format("{0}MISSILE-{1}",THIS_MISSILE_IS_AN_ANTIMISSILE?"ANTI":"",refCon.OtherConnector.CustomData);
                //getCustName(); 
            }

            FindThrusters();
            //GetGyros();

            List<IMyShipMergeBlock> list1 = new List<IMyShipMergeBlock>();
            List<IMyShipConnector> list2 = new List<IMyShipConnector>();
            List<IMyBatteryBlock> list3 = new List<IMyBatteryBlock>();
            List<IMyGasTank> list4 = new List<IMyGasTank>();

            GridTerminalSystem.GetBlocksOfType(list1);
            GridTerminalSystem.GetBlocksOfType(list2);
            GridTerminalSystem.GetBlocksOfType(list3);
            GridTerminalSystem.GetBlocksOfType(list4);

            foreach (IMyShipMergeBlock mer in list1) if (IsOnThisGrid(mer)) MergerList.Add(mer);
            foreach (IMyShipConnector con in list2) if (IsOnThisGrid(con)) ConnecList.Add(con);
            foreach (IMyBatteryBlock bat in list3) if (IsOnThisGrid(bat)) BattryList.Add(bat);
            foreach (IMyGasTank hdt in list4) if (IsOnThisGrid(hdt)) { HTankList.Add(hdt); hdt.Stockpile = true; }
        }

        double GetSpeed() {
            if (!ControllingBlockFoundAndApplied())
                return 101D;
            else
                return SHIP_CONTROLLER.GetShipSpeed();
        }

        void SelfDestruct() {
            List<IMyWarhead> warheads = new List<IMyWarhead>();
            GridTerminalSystem.GetBlocksOfType(warheads);
            foreach (IMyWarhead head in warheads) { head.IsArmed = true; }
            foreach (IMyWarhead head in warheads) { head.Detonate(); }
        }

        Vector3D CheckIfGrav() {
            Vector3D planet;
            if (strtELV == -1) {
                if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out strtELV)) strtELV = -1;
            }
            double temp = currentMissileElevation;
            if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentMissileElevation)) currentMissileElevation = temp;

            if (SHIP_CONTROLLER.TryGetPlanetPosition(out planet)) {
                missileIsInGravityWell = true;
                useMNV = true;
                maxSpeed = 340d;
                addSPDNeed = 100d;
                maxSPDDev = 100d;
            }
            else {
                missileIsInGravityWell = false;
                useMNV = false;
                maxSpeed = 340d;
                addSPDNeed = 100d;
                maxSPDDev = 30d;
            }

            return missileIsInGravityWell ? planet : NOTHING;
        }

        Vector3D
                ship, sub, curr, algn,
                planet, command, initPos;

        double distance = 0,
                currSPD,
                disToTarget = 2000d;

        int culprit;
        float thrOv;


        List<NavPrompt> prompts = new List<NavPrompt>();
        List<NavPrompt> algPr;
        List<IMyThrust> group = new List<IMyThrust>();

        void SigToHQ(string message) { IGC.SendBroadcastMessage(misCMDTag, message); }

        /*
         distance <= (GetSpeed() / 2)
             */

        bool ArmPayload(double distance, double speed) {
            speed = speed < 200 ? 200 : speed;
            return distance <= (speed / 2);
        }

        bool StopFiringEngine() {
            if (!controllerExistsAndWorking) return true;

            Vector3D
                velocity = Vector3D.Normalize(SHIP_CONTROLLER.GetShipVelocities().LinearVelocity),
                forward = SHIP_CONTROLLER.WorldMatrix.Forward,
                diff = Vector3D.Subtract(velocity, forward);

            ///    New Part               && Classic Part
            return (diff.Length() < 0.5d) && (distance <= GetSpeed() + ACT_DIST || GetSpeed() > maxSpeed);
        }

        bool PayloadPrimed = false;
        void PerformStateSpecificWork() {
            ship = SHIP_CONTROLLER.GetPosition();
            sub = target == null ? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(target, ship)));
            curr = NOTHING;
            planet = CheckIfGrav();

            distance = 0;
            currSPD = controllerExistsAndWorking ? GetSpeed() : strtSPD;

            if (CurrentState > MISSILE_STATE.INITIALIZING) timeIndex++;

            prompts = new List<NavPrompt>();
            group = new List<IMyThrust>();

            if (CurrentState > MISSILE_STATE.PREPARING_FOR_LAUNCH) {
                curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);
                distance = Vector3D.Subtract(target, ship).Length();
                for (int i = 1; i < 7; i++)
                    prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                prompts = prompts.OrderBy(o => o.vLength).ToList();
            }

            switch (CurrentState) {
                case MISSILE_STATE.INITIALIZING:
                    InitShip();
                    break;

                case MISSILE_STATE.PREPARING_FOR_LAUNCH:
                    if (timeIndex > 200) { // /2 because of the throttling
                        initPos = Me.GetPosition();
                        ChangeState(MISSILE_STATE.EXITING_LAUNCHPORT);
                    }
                    CutAnchor(200 - timeIndex);
                    break;

                case MISSILE_STATE.EXITING_LAUNCHPORT:
                    if (strtSPD == -1d)
                        strtSPD = controllerExistsAndWorking ? SHIP_CONTROLLER.GetShipSpeed() : 0d;

                    //if (timeNR++ < 1) return;

                    thrOv = 1f - (myNumber - 1) * 0.05f;

                    if (currentMissileElevation <= strtELV && strtELV != -1) {
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, thrOv);
                    }

                    bool abortNormal = false;
                    if (!missileIsInGravityWell) {
                        if (currSPD >= strtSPD + 30d) {
                            for (culprit = 0; culprit < 5; culprit++) {
                                if (prompts[culprit].dirInt == 1) abortNormal = true;
                            }
                        }
                    }
                    else addSPDNeed = 9999;

                    disToTarget = Vector3D.Distance(initPos, target);
                    if (strtELV != -1 && currentMissileElevation != -1) {
                        double addELV = disToTarget >= 2000 ? disToTarget / 20 : 100d;
                        if (addELV > 500) addELV = 500;
                        if (strtELV + addELV <= currentMissileElevation) abortNormal = true;
                    }

                    if (currSPD >= strtSPD + addSPDNeed || abortNormal) {
                        ChangeState(MISSILE_STATE.DAMPENING);
                    }
                    else if (THRUSTERS.TryGetValue(1, out group)) {
                        MoveAGroupOfThrusters(group, thrOv);
                    }
                    break;

                case MISSILE_STATE.DAMPENING:
                    if (curr != null && curr.Length() <= 0.25d) {
                        if (curr.Length() <= maxDeviation || GetSpeed() <= strtSPD + maxSPDDev) {
                            if (!missileIsInGravityWell) ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                            else {
                                algn = CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship)));

                                algPr = new List<NavPrompt>();
                                for (int i = 1; i < 7; i++)
                                    algPr.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), algn)));
                                algPr = algPr.OrderBy(o => o.vLength).ToList();

                                if (algPr[0].dirInt == 2 || algPr[1].dirInt == 2) {
                                    mbOrbital = true;
                                    ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                                }
                                else {
                                    mbOrbital = false;
                                    ChangeState(MISSILE_STATE.GRAV_ALGN);
                                }
                            }
                            return;
                        }
                    }
                    else Runtime.UpdateFrequency = UpdateFrequency.Update1;

                    for (culprit = 0; culprit < 3; culprit++) if (prompts[culprit].dirInt != 1 && prompts[culprit].dirInt != 2) break;
                    culprit = prompts[culprit].dirInt;

                    command = DirToCmd(2, culprit);
                    MoveAllGyros((float)(command.X * curr.Length()), (float)(command.Y * curr.Length()), (float)(command.Z * curr.Length()));
                    DirToMnv(2, culprit, 0.1F);

                    break;

                case MISSILE_STATE.GRAV_ALGN:
                    algn = CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship)));

                    algPr = new List<NavPrompt>();
                    for (int i = 1; i < 7; i++)
                        algPr.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), algn)));
                    if (IsVerySimilar(algPr[2].vLength, algPr[3].vLength) && algPr[5].vLength + 0.5 < algPr[4].vLength) {
                        ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                        MoveAllGyros(0, 0, 0);
                        OverrideGyros(false);
                        return;
                    }
                    algPr = algPr.OrderBy(o => o.vLength).ToList();

                    for (culprit = 0; culprit < 3; culprit++) {
                        if (algPr[culprit].dirInt != 5 && algPr[culprit].dirInt != 6) break;
                    }
                    culprit = algPr[culprit].dirInt;
                    if (culprit == 1 || culprit == 2) {
                        for (culprit = 0; culprit < 3; culprit++) {
                            if (algPr[culprit].dirInt != 5 && algPr[culprit].dirInt != 6 && algPr[culprit].dirInt != 1 && algPr[culprit].dirInt != 2) break;
                        }
                        culprit = algPr[culprit].dirInt;
                    }

                    command = DirToCmd(5, culprit);
                    MoveAllGyros((float)(command.X * 0.3), (float)(command.Y * 0.3), (float)(command.Z * 0.3));

                    break;

                case MISSILE_STATE.APPROACHING_TARGET:
                    if (curr.Length() <= maxDeviation) {
                        useMNV = true;
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 1f);
                    }
                    else 
                        useMNV = false;

                    if (distance < 50 && distance > lastDist) SelfDestruct();
                    if (StopFiringEngine()) {
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
                    }
                    if ((ArmPayload(distance, GetSpeed()) || !controllerExistsAndWorking) && !PayloadPrimed) {
                        PayloadPrimed = true;
                        SigToHQ("BOOM;" + missileTag);

                        List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
                        List<IMyWarhead> warheads = new List<IMyWarhead>();

                        GridTerminalSystem.GetBlocksOfType(merges);
                        GridTerminalSystem.GetBlocksOfType(warheads);

                        foreach (IMyWarhead war in warheads)        war.IsArmed = true;
                        foreach (IMyShipMergeBlock mer in merges)   mer.Enabled = false;
                    }

                    for (culprit = 0; culprit < 3; culprit++) {
                        if (prompts[culprit].dirInt != 1 && prompts[culprit].dirInt != 2) break;
                    }   culprit = prompts[culprit].dirInt;

                    Vector3D velocitySub = Vector3D.Subtract(CutVector(Vector3D.Normalize(SHIP_CONTROLLER.GetShipVelocities().LinearVelocity)), sub);

                    float mnvAmm = (distance >= 10000) ? (float)curr.Length() * 20f : (velocitySub.Length() < 0.1d? 0.4f :1f);

                    //AntennaText(String.Format("{0} {1:0.00}", mnvAmm, velocitySub.Length()));

                    if (missileIsInGravityWell && !mbOrbital) {
                        if (culprit == 5)
                            mnvAmm = 1f;
                        else
                        if (culprit == 6) 
                            mnvAmm = distance > 4000d ? 0f : 1f;
                    }
                    command = DirToCmd(2, culprit);

                    culprit = culprit == 4 ? 3 : (culprit==3? 4 : culprit);

                    if (useMNV) DirToMnv(2, culprit, mnvAmm);

                    MoveAllGyros((float)(command.X * curr.Length()), (float)(command.Y * curr.Length()), (float)(command.Z * curr.Length()));

                    if (distance >= 4000d && missileIsInGravityWell && currentMissileElevation <= 1000) if (THRUSTERS.TryGetValue(5, out group)) MoveAGroupOfThrusters(group, 1f);
                    lastDist = distance;

                    Echo(" " + distance + " ");
                    break;

                case MISSILE_STATE.MANUAL:
                    break;
            }
        }

        void Output(Object input) {
            string message = (input is string) ? (string)input : input.ToString();
            Echo(message);
        }

        double InterCosine(Vector3D first, Vector3D second) {
            double
                scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                productOfLengths = first.Length() * second.Length();

            return scalarProduct / productOfLengths;
        }

        bool GetProjectedPos(Vector3D enPos, Vector3D enSpeed, Vector3D myPos, double speed, out Vector3D projPos) {/// do not enter if enSpeed is a "0" vector, or if our speed is 0
            if (speed <= 0) speed = 1;
            projPos = NOTHING;
            /// A = enPos, B = myPos, C is the estimated meeting point

            if(speed < maxSpeed && speed < enSpeed.Length() &&
            Vector3D.Subtract(Vector3D.Normalize(Vector3D.Subtract(myPos, enPos)),Vector3D.Normalize(enSpeed))
                .Length() > 1.4142d) // i.e. if there is no chance in hell we will make it... please, accelerate :^)
                return false;

            double
                t = enSpeed.Length() / speed,           //t -> b = a*t  
                projPath,                               //b
                dist = Vector3D.Distance(myPos, enPos), //c
                cos = InterCosine(enSpeed, Vector3D.Subtract(myPos, enPos)),

                // pre 10-08-2021
                // delta = 4 * dist * dist * ((1 / (t * t)) + (cos * cos) - 1);

                // post 10-08-2021
                // delta = 4c^2(t^2cos^2 - t^2 + 1)
                delta = 4 * (dist * dist) * ((t * t * cos * cos) - (t * t) + 1);

            if (delta < 0) {
                return false;
            }
            else
            if (delta == 0) {
                // pre 10-08-2021
                // projPath = -1 * (2 * dist * cos) / (2 * (((t * t) - 1) / (t * t)));

                // post 10-08-2021
                projPath = ((t * dist * cos) / ((t * t) - 1));
            }
            else {
                // pre 10-08-2021
                // projPath = ((2 * dist * cos - Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                // if (projPath < 0) {
                //     projPath = ((2 * dist * cos + Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                // }

                // post 10-08-2021
                if ((projPath = (((2 * t * dist * cos) + Math.Sqrt(delta)) / (2 * ((t * t) - 1)))) < 0) {
                    projPath = (((2 * t * dist * cos) - Math.Sqrt(delta)) / (2 * ((t * t) - 1)));
                }
            }
            projPath *= t; /// projPath = a, so, to get b, we need to a*t = b

            enSpeed = Vector3D.Normalize(enSpeed);
            enSpeed = Vector3D.Multiply(enSpeed, projPath);

            projPos = Vector3D.Add(enPos, enSpeed);
            return true;
        }

        Vector3D ApplyTarSpd(Vector3D position, Vector3D speed) {
            double
                mySpeed = SHIP_CONTROLLER.GetShipVelocities().LinearVelocity.Length(),
                enSpeed = speed.Length(),
                multiplier;

            if (enSpeed > 0) {
                Vector3D output;
                if (GetProjectedPos(position, speed, SHIP_CONTROLLER.CubeGrid.GetPosition(), mySpeed, out output)) { return output; }
            }

            multiplier = (mySpeed != 0 && enSpeed != 0) ? (enSpeed / mySpeed) : 0;

            Vector3D
                addition = Vector3D.Multiply(speed, multiplier);

            return Vector3D.Add(position, addition);
        }

        void EvaluateCommandInput(string argument) {
            String[] eval = argument.ToUpper().Split(' ');

            if (eval[0].Equals("GYRO")) {
                if (eval.Length > 3) {
                    MoveAllGyros(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                }
                else {
                    MoveAllGyros(0, 0, 0);
                    OverrideGyros(false);
                }
            }
            else if (eval[0].Equals("LNCH")) {
                if (eval.Length > 3) {
                    target = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                    ChangeState(MISSILE_STATE.EXITING_LAUNCHPORT);
                }
            }
            else if (eval[0].Equals("DUMB")) {
                if (eval.Length > 3) {
                    target = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                    ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                }
            }
            else if (eval[0].Equals("PREP")) {
                if (eval.Length > 3) {
                    target = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                    if (eval.Length > 4) missileTag = eval[4];
                    ChangeState(MISSILE_STATE.PREPARING_FOR_LAUNCH);
                }
            }
            else if (eval[0].Equals("MANUAL")) {
                ChangeState(MISSILE_STATE.MANUAL);
            }
            else if (eval[0].Equals("LAUNCHABORT")) {
                ChangeState(MISSILE_STATE.INITIALIZING);
            }
            else
                ChangeState(argument.ToUpper());
        }

        string GetMessageTag(MyIGCMessage message){
            string data = (string)message.Data;
            string[] bits = data.Split(';');

            return (bits.Length>0)? bits[0].ToUpper():"";
        }

        void EvaluateAllPendingMessages(){
            List<MyIGCMessage> messages = new List<MyIGCMessage>();
            while(missileListener!=null && missileListener.HasPendingMessage)
                messages.Add(missileListener.AcceptMessage());

            int msgCount = messages.Count, lastTarsetIndex = -1;
            string tag;
            for(int i=0; i<msgCount; i++){
                MyIGCMessage message = messages[i];
                tag = GetMessageTag(message);
                if (tag.Equals("ABORT")){
                    EvaluateMessage(message);
                    return;
                }
                else
                if (tag.Equals("TARSET")) 
                    lastTarsetIndex = i;
            }

            if(lastTarsetIndex!=-1) EvaluateMessage(messages[lastTarsetIndex]);
        }

        void EvaluateMessage(MyIGCMessage message) {
            string data = (string)message.Data;
            string[] bits = data.Split(';');
            if (bits[0].ToUpper().Equals("TARSET")) {
                Vector3D
                    oldTar = target,
                    olCaTa = cameras_target;
                missileHasPendingOrders = true;
                if (bits.Length > 3) {
                    if (bits.Length > 6) {
                        Vector3D pos, vel;
                        try {
                            pos = new Vector3D(double.Parse(bits[1]), double.Parse(bits[2]), double.Parse(bits[3]));
                            vel = new Vector3D(double.Parse(bits[4]), double.Parse(bits[5]), double.Parse(bits[6]));
                            target = ApplyTarSpd(pos, vel);
                            cameras_target = pos;
                        }
                        catch (Exception e) {
                            Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
                            Me.GetSurface(0).WriteText(e.ToString());
                            target = oldTar;
                            cameras_target = olCaTa;
                            missileHasPendingOrders = false;
                        }
                    }
                    else {
                        try {
                            target = new Vector3D(double.Parse(bits[1]), double.Parse(bits[2]), double.Parse(bits[3]));
                            cameras_target = target;
                        }
                        catch (Exception e) {
                            Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
                            Me.GetSurface(0).WriteText(e.ToString());
                            target = oldTar;
                            cameras_target = olCaTa;
                            missileHasPendingOrders = false;
                        }
                    }
                }
                return;
            }
            else
            if (data.Equals("ABORT") || (bits.Length > 0 && bits[0].ToUpper().Equals("ABORT"))) {
                MoveAllGyros(0, 0, 0);
                OverrideGyros(false);
                List<IMyThrust> group = new List<IMyThrust>();
                GridTerminalSystem.GetBlocksOfType(group);
                MoveAGroupOfThrusters(group, 0f);
                if (controllerExistsAndWorking) SHIP_CONTROLLER.DampenersOverride = false;
                SelfDestruct();
                ChangeState(MISSILE_STATE.INITIALIZING);
            }
        }

        void OutputStatusOnTheAntenna() {
            string status = "";
            status += Me.CustomName + " " + string.Format("{0:0.##}", currentMissileElevation) + "m " + CurrentState.ToString() + " " + timeIndex + " " + (missileIsInGravityWell ? "- In Gravity" : "- In SPACE");
            if (missileHasPendingOrders) status = "ORDGOT" + status;
            else
            if (missileChangedTarget) status = "TARCHNG" + status;
            /*/else {
                if (MissileCameras.Count > 0) {
                    MissileCameras = MissileCameras.OrderBy(o => o.AvailableScanRange).ToList();
                    status += " " + string.Format("{0:0.}", MissileCameras[0].AvailableScanRange);
                    status += " " + string.Format("{0:0.}", MissileCameras[MissileCameras.Count - 1].AvailableScanRange);
                }
            }/**/
            status += " " + missileTag;
            SetAntennasText(status);
        }

        void SetAntennasText(object message) {
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (IsOnThisGrid(ant)) { ant.Radius = CurrentState > MISSILE_STATE.EXITING_LAUNCHPORT ? 50000f : 1000f; ant.CustomName = text; }
        }

        public void Main(string argument, UpdateType updateSource) {
            if (SHIP_CONTROLLER == null || !SHIP_CONTROLLER.IsWorking)
                controllerExistsAndWorking = ControllingBlockFoundAndApplied();
            if ((updateSource & UpdateType.IGC) > 0) {
                EvaluateAllPendingMessages();
            }
            else {
                switch (argument.ToUpper()) {
                    case "":
                        if (skipThisTick) {
                            skipThisTick = false;
                            OutputStatusOnTheAntenna();
                            return;
                        }
                        else {
                            skipThisTick = true;
                            if (missileHasPendingOrders) {
                                ticksSinceLastOrder = 0;
                                missileHasPendingOrders = false;
                            }
                            else {
                                if (CurrentState >= MISSILE_STATE.DAMPENING) {
                                    if (ticksSinceLastOrder++ > 10) {
                                        Vector3D tango = FindTargetUsingCameras();
                                        if (!tango.Equals(NOTHING)) {
                                            target = tango;
                                            missileChangedTarget = true;
                                        }
                                        else
                                            missileChangedTarget = false;
                                        ticksSinceLastOrder = 0;
                                    }
                                }
                            }
                            PerformStateSpecificWork();
                        }
                        break;

                    default:
                        EvaluateCommandInput(argument);
                        break;
                }
            }
        }
    }
}
