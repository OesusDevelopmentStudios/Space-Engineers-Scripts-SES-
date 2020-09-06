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

        //////////////////// MISSILE CONTROL SCRIPT ///////////////////////
        /// Constants

        const string SCRIPT_VERSION = "v4.2.17";
        const bool DEFAULT_DAMPENERS_SETTING = false;
        const float ACT_DIST = 100f;
        const double maxDeviation = 0.02d;
        const int MIN_SUCC_CAMERAS = 9;

        MISSILE_STATE CurrentState;

        IMyBroadcastListener
                missileListener;
        string missileTag = "MISSILE-CHN", misCMDTag = "MISSILE_COMMAND-CHN";

        Vector3D UPP_CMD = new Vector3D(0, -1, 0),
                 DWN_CMD = new Vector3D(0, 1, 0),
                 LFT_CMD = new Vector3D(-1, 0, 0),
                 RIG_CMD = new Vector3D(1, 0, 0),
                 CLK_CMD = new Vector3D(0, 0, 1),
                 ALK_CMD = new Vector3D(0, 0, -1),
                 ZRO_CMD = new Vector3D(0,0,0),

                 NOTHING = new Vector3D(44, 44, 44),
                 TARGET,
                 CAMTAR;

        const int FW_VAL = 2,
                  UP_VAL = 6,
                  LF_VAL = 3,
                  RT_VAL = 4,
                  BW_VAL = 1,
                  DW_VAL = 5;

        /// END OF CONSTANTS

        int     timeNR = 0,
                myNumber = 0;

        double strtSPD = -1d,
                strtELV = -1d,
                currELV = -1d,
                lastDist = 999999,

                addSPDNeed = 100d,
                maxSPDDev = 30d;

        const double
                maxSpeed = 100;

        bool    
            //useMNV = false,
                gravMode = false,
                contrFine = false,
                aborting = false,
                ordersGot = false,
                chngTarg = false,
                throttle = false,
                mbOrbital = false;


        List<IMyShipController> ControlList = new List<IMyShipController>();
        List<IMyShipMergeBlock> MergerList = new List<IMyShipMergeBlock>();
        List<IMyShipConnector> ConnecList = new List<IMyShipConnector>();
        List<IMyBatteryBlock> BattryList = new List<IMyBatteryBlock>();
        List<IMyGasTank> HTankList = new List<IMyGasTank>();
        List<IMyCameraBlock> CameraList = new List<IMyCameraBlock>();

        class NavPrompt {
            public int dirInt;
            public double vLength;

            public NavPrompt(int dir, Vector3D input) {
                this.dirInt = dir;
                this.vLength = input.Length();
            }
        }

        enum MISSILE_STATE {
            INIT,
            PREP_LNCH,
            EXIT_LAUNCHPOINT,
            GRAV_ALGN,
            DAMPENING,
            DUMB_APP_TARGET,
            MANUAL
        }

        void CutAnchor(int ticksToLaunch) {
            if (ticksToLaunch == 3) { foreach (IMyShipConnector con in ConnecList) { if (con.CustomName.Equals("AntiMissile/Refuel Connector")) con.Enabled = false; } }
            else if (ticksToLaunch == 2) { foreach (IMyGasTank tank in HTankList) { tank.Stockpile = false; } }
            else if (ticksToLaunch == 1) { foreach (IMyBatteryBlock batt in BattryList) { batt.ChargeMode = ChargeMode.Auto; } }
            else if (ticksToLaunch <= 0) {
                foreach (IMyShipMergeBlock mer in MergerList) { if (mer.CustomName.Equals("AntiMissile/DMerge Block")) mer.Enabled = false; }
            }
        }

        bool isOnThisGrid(IMyCubeBlock G) {
            if (G.CubeGrid.Equals(Me.CubeGrid)) return true;
            else return false;
        }

        double Difference(double d1, double d2){
            double first = d1 > d2 ? d1 : d2,
                   second = d1 < d2 ? d1 : d2;

            return first - second;
        }

        bool isAlmostSame(double d1, double d2) {
            if (d1 == d2) return true;
            double first = d1 > d2 ? d1 : d2,
                   second = d1 < d2 ? d1 : d2;

            if (first - second < (first / 10)) return true;
            else return false;
        }

        void ChangeState(MISSILE_STATE state) {
            //Output("Changing mode from " + CurrentState + " to " + state.ToString() + ".");
            timeNR = 0;
            List<IMyThrust> group = new List<IMyThrust>();
            if (state > MISSILE_STATE.INIT) Me.CustomName = missileTag;
            switch (state) {
                case MISSILE_STATE.INIT:
                    ResetThrust();
                    MoveAllGyros(0, 0, 0);
                    OverrideGyros(false);

                    foreach (IMyShipMergeBlock mer in MergerList) { if (mer.CustomName.Equals("AntiMissile/DMerge Block")) mer.Enabled = true; }
                    foreach (IMyShipConnector con in ConnecList) { if (con.CustomName.Equals("AntiMissile/Refuel Connector")) con.Enabled = true; }

                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case MISSILE_STATE.PREP_LNCH:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.EXIT_LAUNCHPOINT:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    //Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case MISSILE_STATE.GRAV_ALGN:
                    //useMNV = false;
                    if (contrFine) SHIP_CONTROLLER.DampenersOverride = false;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.DAMPENING:
                    ResetThrust();
                    if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupThrusters(group, 0f);
                    if (contrFine) SHIP_CONTROLLER.DampenersOverride = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    if (missileListener == null) {
                        missileListener = IGC.RegisterBroadcastListener(missileTag);
                        missileListener.SetMessageCallback();
                    }
                    break;

                case MISSILE_STATE.DUMB_APP_TARGET:
                    if (contrFine) SHIP_CONTROLLER.DampenersOverride = false;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    //useMNV = false;
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

        float multiplier() {
            if (Runtime.UpdateFrequency == UpdateFrequency.Update1)
                return 3f;
            else
                return 1f;
        }

        void ChangeState(string state) {
            Output("Changing mode from " + CurrentState + " to " + state + ".");
            switch (state.ToUpper()) {
                case "INIT": ChangeState(MISSILE_STATE.INIT); break;

                case "DAMP": ChangeState(MISSILE_STATE.DAMPENING); break;

                default: Output("Function 'ChangeState': Undefined input value."); break;
            }
        }

        IMyShipController SHIP_CONTROLLER;
        Dictionary<int, List<IMyThrust>> THRUSTERS = new Dictionary<int, List<IMyThrust>>();

        public Program() {
            Runtime.UpdateFrequency
                            = UpdateFrequency.Update10;
            TARGET = NOTHING;
            CAMTAR = NOTHING;
            SayMyName(SCRIPT_VERSION);
            Me.CubeGrid.CustomName = "AntiMissile " + SCRIPT_VERSION;

            List<IMyShipController> controls = new List<IMyShipController>();
            ControlList = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controls);
            foreach (IMyShipController cont in controls) { if (isOnThisGrid(cont) && cont.IsWorking) ControlList.Add(cont); }

            ChangeState(MISSILE_STATE.INIT);
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

        void antenaText(object message) {
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (isOnThisGrid(ant)) { ant.Radius = CurrentState > MISSILE_STATE.EXIT_LAUNCHPOINT ? 50000f : 1000f; ant.CustomName = text; }
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

        string getCustName() {
            int index = 1;
            while (true) {
                IMyProgrammableBlock block = GridTerminalSystem.GetBlockWithName("ANTIMISSILE-" + index) as IMyProgrammableBlock;
                if (block != null) index++;
                else break;
            }
            myNumber = index;
            return "ANTIMISSILE-" + index;
        }

        Vector3D CulpritToMove(int culprit, float deviation){
            if (deviation < 0.002f) return ZRO_CMD;

            deviation = deviation > 1 ? deviation*deviation*deviation : (float)Math.Sqrt((double)deviation);
            /**/
            if (culprit <= 4) {
                if (culprit % 2 == 0) {
                    return  Vector3D.Multiply(RIG_CMD,(double)deviation);
                }
                else{
                    return Vector3D.Multiply(LFT_CMD,(double)deviation);
                }
            }
            else {
                if (culprit % 2 == 0) {
                    return Vector3D.Multiply(DWN_CMD,(double)deviation);
                }
                else {
                    return Vector3D.Multiply(UPP_CMD,(double)deviation);
                }
            }
            /**/
        }


        /*/
            ship = SHIP_CONTROLLER.GetPosition();
            sub = TARGET == null ? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(TARGET, ship)));
            curr = NOTHING;
            planet = checkIfGrav();

            distance = 0;
            currSPD = contrFine ? GetSpeed() : strtSPD;

            if (CurrentState > MISSILE_STATE.INIT) timeNR++;

            prompts = new List<NavPrompt>();
            group = new List<IMyThrust>();

            if (CurrentState > MISSILE_STATE.PREP_LNCH) {
                curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);
                distance = Vector3D.Subtract(TARGET, ship).Length();
                for (int i = 3; i < 7; i++)
                    prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                prompts = prompts.OrderBy(o => o.vLength).ToList();

                culprit1 = prompts[0];
                culprit2 = prompts[1];
            }
        /**/

        void CorrectionManeuvers() {

            Vector3D
                culpritTrap = Me.GetPosition()+SHIP_CONTROLLER.GetShipVelocities().LinearVelocity;

            sub = TARGET == null ? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(culpritTrap, ship)));

            prompts = new List<NavPrompt>();
            curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);

            for (int i = 3; i < 7; i++)
                prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
            prompts = prompts.OrderBy(o => o.vLength).ToList();

            culprit1 = prompts[0];
            culprit2 = prompts[1];

            float
                OR1 = (float)Difference(culprit1.vLength, 1.4142d),
                OR2 = (float)Difference(culprit2.vLength, 1.4142d);

            OR1 = OR1 < 0.002f ? 0 : 1;
            OR2 = OR2 < 0.002f ? 0 : 1;

            //OR1 = OR1 > 1 ? 1 : ((OR1 < 0.005) ? 0 : OR1);
            //OR2 = OR2 > 1 ? 1 : ((OR2 < 0.005) ? 0 : OR2);

            int
                TC1 = culprit1.dirInt, 
                TC2 = culprit2.dirInt;

            if (TC1 == 5) TC1 = 6;
            else
            if (TC1 == 6) TC1 = 5;

            if (TC2 == 5) TC2 = 6;
            else
            if (TC2 == 6) TC2 = 5;

            ResetThrust();

            List<IMyThrust> thrusters = new List<IMyThrust>();

            if(THRUSTERS.TryGetValue(TC1, out thrusters)) {
                MoveAGroupThrusters(thrusters, OR1);
            }
            if(THRUSTERS.TryGetValue(TC2, out thrusters)) {
                MoveAGroupThrusters(thrusters, OR2);
            }
            /**/
        }

        Vector3D DoubleCTM(int C1, double VL1, int C2, double VL2) {return Vector3D.Add(CulpritToMove(C1, (float)Difference(VL1,1.4142d)), CulpritToMove(C2, (float)Difference(VL2, 1.4142d)));}

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

        /**
        void DirToMnv(int lndDir, int culprit) { DirToMnv(lndDir, culprit, 1f); }

        void DirToMnv(int lndDir, int culprit, float ovrPrc) {
            ResetThrust();
            List<IMyThrust> manThr = new List<IMyThrust>();
            if (lndDir == 1 && (culprit == 3 || culprit == 4)) {
                if (culprit == 3) { if (THRUSTERS.TryGetValue(4, out manThr)) { MoveAGroupThrusters(manThr, ovrPrc); return; } }
                else { if (THRUSTERS.TryGetValue(3, out manThr)) { MoveAGroupThrusters(manThr, ovrPrc); return; } }
            }
            else {
                if (THRUSTERS.TryGetValue(culprit, out manThr)) { MoveAGroupThrusters(manThr, ovrPrc); return; }
            }
        }
        /**/

        Vector3D DirintToVec(int dirint) {
            switch (dirint) {
                case 1:
                    return SHIP_CONTROLLER.WorldMatrix.Forward;
                case 2:
                    return SHIP_CONTROLLER.WorldMatrix.Backward;
                case 3:
                    return SHIP_CONTROLLER.WorldMatrix.Left;
                case 4:
                    return SHIP_CONTROLLER.WorldMatrix.Right;
                case 5:
                    return SHIP_CONTROLLER.WorldMatrix.Up;
                case 6:
                    return SHIP_CONTROLLER.WorldMatrix.Down;
            }
            return NOTHING;
        }

        // END OF CONV
        // GET

        bool GetControllingBlock() {
            List<IMyShipController> temp = new List<IMyShipController>();
            foreach (IMyShipController cont in ControlList) { if (isOnThisGrid(cont) && cont.IsWorking) temp.Add(cont); }
            ControlList = new List<IMyShipController>(temp);

            SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in ControlList) {
                if (controler.IsMainCockpit) {
                    SHIP_CONTROLLER = controler;
                    return true;
                }
            }

            foreach (IMyShipController controler in ControlList) {
                if (SHIP_CONTROLLER == null && controler.IsWorking && isOnThisGrid(controler)) {
                    SHIP_CONTROLLER = controler;
                    controler.IsMainCockpit = true;
                }
                controler.DampenersOverride = DEFAULT_DAMPENERS_SETTING;
            }

            if (SHIP_CONTROLLER == null) {
                Output("Could not find any ship controller.");
                return false;
            }
            else
                return true;
        }

        List<IMyGyro> GetGyros() {
            List<IMyGyro> list = new List<IMyGyro>();
            List<IMyGyro> temp = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyGyro gyro in temp) if (isOnThisGrid(gyro)) list.Add(gyro);

            return list;
        }

        Vector3D GetTarget(int camIndx = 0) {
            IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("AntiMissile/Camera" + (camIndx + 1)) as IMyCameraBlock;
            timeNR = 0;
            if (camIndx > 8 || !contrFine) {
                Output("\nOOC");
                return NOTHING;
            }
            if (camera == null) {
                return GetTarget(++camIndx);
            }
            Vector3D
                rayTG,
                addition;
            switch (camIndx) {
                case 0:
                    rayTG = CAMTAR;
                    break;
                case 1:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 50d);
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 2:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 50d);
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 3:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 50d);
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 4:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 50d);
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 5:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 35d));
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 6:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Right, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 35d));
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 7:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Down, 35d));
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                case 8:
                    addition = Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Left, 35d);
                    addition = Vector3D.Add(addition, Vector3D.Multiply(SHIP_CONTROLLER.WorldMatrix.Up, 35d));
                    rayTG = Vector3D.Add(CAMTAR, addition);
                    break;
                default:
                    rayTG = NOTHING;
                    break;
            }
            double distance = Vector3D.Distance(camera.GetPosition(), rayTG);
            if (!camera.CanScan(distance))
                camera = GetRdyCam(distance);

            if (camera != null) {
                MyDetectedEntityInfo target = camera.Raycast(rayTG);
                long entId;

                if (!target.IsEmpty() &&
                   //((target.Type == MyDetectedEntityType.LargeGrid || target.Type == MyDetectedEntityType.SmallGrid)
                   //&& target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                   long.TryParse(missileTag, out entId) && entId.Equals(target.EntityId)
                   ) {
                    Output("\nFOUND YA");
                    CAMTAR = target.Position;
                    return applyTarSpd(target.Position, target.Velocity);
                }
                else {
                    return GetTarget(++camIndx);
                }
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
                if (isOnThisGrid(cam)) {
                    cam.EnableRaycast = true;
                    list.Add(cam);
                }
            }

            return list;
        }

        Vector3D AutoTarget(double distance = 3000d) {
            IMyCameraBlock cam = GetRdyCam();
            if (cam == null) return NOTHING;

            MyDetectedEntityInfo target = cam.Raycast(distance);
            if (target.IsEmpty() || target.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies ||
                (target.Type != MyDetectedEntityType.LargeGrid && target.Type != MyDetectedEntityType.SmallGrid)) return NOTHING;
            else return applyTarSpd(target.Position, target.Velocity);
        }

        IMyCameraBlock GetRdyCam(double distance = 3000d) {
            for (int i = 9; i > 0; i--) {
                IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("AntiMissile/Camera" + (i)) as IMyCameraBlock;
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
                if (!isOnThisGrid(t)) continue;
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
                if(output) Echo(t.CustomName + " " + t.MaxThrust + "\n");
            }
            bool ok = true;
            if (output) {
                for (int i = 1; i < 7; i++) if (!THRUSTERS.TryGetValue(i, out temp)) { ok = false; Output("WARNING: NO " + DirintToName(i) + " THRUSTERS."); }
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

                int blockDir = TranslateDirection(block.Orientation.Forward);
                int blockSub = TranslateDirection(block.Orientation.Up);
                int firstDigit = 0;

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
            Yaw *= multiplier();
            Pitch *= multiplier();
            Roll *= multiplier();
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

        void MoveAGroupThrusters(List<IMyThrust> Group, float OverridePercent) {
            foreach (IMyThrust Thruster in Group) {
                Thruster.ThrustOverridePercentage = OverridePercent;
            }
        }

        void EnableAGroupThrusters(List<IMyThrust> Group, bool Enable) {
            foreach (IMyThrust Thruster in Group) {
                Thruster.Enabled = Enable;
            }
        }

        void ResetThrust() {
            ResetThrust(false);
        }

        void ResetThrust(bool all) {
            List<IMyThrust> list = new List<IMyThrust>();
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
            CameraList = GetCameras();
            IMyShipConnector refCon = null;
            List<IMyShipConnector> cons = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(cons);
            foreach(IMyShipConnector con in cons) {
                if (con.IsWorking && isOnThisGrid(con) && con.CustomName.Equals("AntiMissile/Refuel Connector")) {
                    refCon = con;
                    break;
                }
            }

            if (CameraList.Count < MIN_SUCC_CAMERAS || refCon == null || refCon.Status != MyShipConnectorStatus.Connected) return;
            else {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                //if (!Me.CustomName.StartsWith("ANTIMISSILE-"))
                    Me.CustomName = "ANTIMISSILE-"+refCon.OtherConnector.CustomData;
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

            foreach (IMyShipMergeBlock mer in list1) if (isOnThisGrid(mer)) MergerList.Add(mer);
            foreach (IMyShipConnector con in list2) if (isOnThisGrid(con)) ConnecList.Add(con);
            foreach (IMyBatteryBlock bat in list3) if (isOnThisGrid(bat)) BattryList.Add(bat);
            foreach (IMyGasTank hdt in list4) if (isOnThisGrid(hdt)) { HTankList.Add(hdt); hdt.Stockpile = true; }
        }

        double GetSpeed() {
            if (!GetControllingBlock())
                return 101D;
            else
                return SHIP_CONTROLLER.GetShipSpeed();
        }

        void selfDestruct() {
            List<IMyWarhead> warheads = new List<IMyWarhead>();
            GridTerminalSystem.GetBlocksOfType(warheads);
            foreach (IMyWarhead head in warheads) { head.IsArmed = true; }
            foreach (IMyWarhead head in warheads) { head.Detonate(); }
        }

        Vector3D checkIfGrav() {
            Vector3D planet;
            if (strtELV == -1) {
                if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out strtELV)) strtELV = -1;
            }
            double temp = currELV;
            if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out currELV)) currELV = temp;

            if (SHIP_CONTROLLER.TryGetPlanetPosition(out planet)) {
                gravMode = true;
                //useMNV = true;
                addSPDNeed = 100d;
                maxSPDDev = 100d;
            }
            else {
                gravMode = false;
                //useMNV = false;
                addSPDNeed = 100d;
                maxSPDDev = 30d;
            }

            return gravMode ? planet : NOTHING;
        }

        Vector3D
                ship, sub, curr, algn, alVec,
                planet, command, initPos;

        NavPrompt culprit1, culprit2;

        double distance = 0,
                currSPD,
                disToTarget = 2000d;

        int culprit;
        float thrOv;


        List<NavPrompt> prompts = new List<NavPrompt>();
        List<NavPrompt> algPr;
        List<IMyThrust> group = new List<IMyThrust>();

        void sigToHQ(string message) { IGC.SendBroadcastMessage(misCMDTag, message); }

        /*
         distance <= (GetSpeed() / 2)
             */

        bool armPayload(double distance, double speed) {
            speed = speed < 200 ? 200 : speed;
            return distance <= (speed / 2);
        }

        Vector3D normalize(Vector3D input) {
            return new Vector3D(input.X / input.Length(), input.Y / input.Length(), input.Z / input.Length());
        }

        bool stopFiringEngine() {
            if (!contrFine) return true;

            Vector3D
                velocity = normalize(SHIP_CONTROLLER.GetShipVelocities().LinearVelocity),
                forward = SHIP_CONTROLLER.WorldMatrix.Forward,
                diff = Vector3D.Subtract(velocity, forward);

            ///    New Part               && Classic Part
            return (diff.Length() < 0.5d) && (distance <= GetSpeed() + ACT_DIST || GetSpeed() > maxSpeed);
        }

        void getSensorData() {
            IMySensorBlock sensor = GridTerminalSystem.GetBlockWithName("AntiMissile/Sensor") as IMySensorBlock;
            if (sensor == null) return;
            string output = "";
            List<MyDetectedEntityInfo> list = new List<MyDetectedEntityInfo>();
            sensor.DetectedEntities(list);
            foreach (MyDetectedEntityInfo data in list) {
                output += data.Type + " " + data.Relationship + "\n";
            }
            Echo("List:\n" + output);
        }

        void ReactToState() {
            ship = SHIP_CONTROLLER.GetPosition();
            sub = TARGET == null ? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(TARGET, ship)));
            curr = NOTHING;
            planet = checkIfGrav();

            distance = 0;
            currSPD = contrFine ? GetSpeed() : strtSPD;

            if (CurrentState > MISSILE_STATE.INIT) timeNR++;

            prompts = new List<NavPrompt>();
            group = new List<IMyThrust>();

            if (CurrentState > MISSILE_STATE.PREP_LNCH) {
                curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);
                distance = Vector3D.Subtract(TARGET, ship).Length();
                if (Double.IsNaN(distance)) selfDestruct();
                for (int i = 3; i < 7; i++)
                    prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                prompts = prompts.OrderBy(o => o.vLength).ToList();

                culprit1 = prompts[0];
                culprit2 = prompts[1];
            }

            switch (CurrentState) {
                case MISSILE_STATE.INIT:
                    InitShip();
                    break;

                case MISSILE_STATE.PREP_LNCH:
                    if (timeNR > 4) { // /2 because of the throttling
                        initPos = Me.GetPosition();
                        ChangeState(MISSILE_STATE.EXIT_LAUNCHPOINT);
                    }
                    CutAnchor(4 - timeNR);
                    break;

                case MISSILE_STATE.EXIT_LAUNCHPOINT:
                    if (strtSPD == -1d)
                        strtSPD = contrFine ? SHIP_CONTROLLER.GetShipSpeed() : 0d;

                    //if (timeNR++ < 1) return;

                    thrOv = 1f;

                    if (currELV <= strtELV && strtELV != -1) {
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupThrusters(group, 0f);
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupThrusters(group, thrOv);
                    }

                    bool abortNormal = false;
                    if (!gravMode) {
                        if (currSPD >= strtSPD + 30d) {
                            if (new NavPrompt(1, Vector3D.Subtract(CutVector(DirintToVec(1)), sub)).vLength<=1.4143) abortNormal = true;
                        }
                    }
                    else addSPDNeed = 9999;

                    disToTarget = Vector3D.Distance(initPos, TARGET);
                    if (strtELV != -1 && currELV != -1) {
                        double addELV = disToTarget >= 2000 ? disToTarget / 20 : 100d;
                        if (addELV > 500) addELV = 500;
                        if (strtELV + addELV <= currELV) abortNormal = true;
                    }

                    if (currSPD >= strtSPD + addSPDNeed || abortNormal) {
                        ChangeState(MISSILE_STATE.DAMPENING);
                    }
                    else if (THRUSTERS.TryGetValue(1, out group)) {
                        MoveAGroupThrusters(group, thrOv);
                    }
                    break;

                case MISSILE_STATE.DAMPENING:
                    if (curr != null && curr.Length() <= 0.25d) {
                        if (curr.Length() <= maxDeviation || GetSpeed() <= strtSPD + maxSPDDev) {
                            if (!gravMode) ChangeState(MISSILE_STATE.DUMB_APP_TARGET);
                            else {
                                algn = CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship)));
                                alVec = Vector3D.Subtract(CutVector(DirintToVec(6)), algn);

                                algPr = new List<NavPrompt>();
                                for (int i = 1; i < 7; i++)
                                    algPr.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), algn)));
                                algPr = algPr.OrderBy(o => o.vLength).ToList();

                                if (algPr[0].dirInt == 2 || algPr[1].dirInt == 2) {
                                    mbOrbital = true;
                                    ChangeState(MISSILE_STATE.DUMB_APP_TARGET);
                                }
                                else {
                                    mbOrbital = false;
                                    ChangeState(MISSILE_STATE.GRAV_ALGN);
                                }
                            }
                            return;
                        }
                    }
                    else {
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    }


                    /*/
                    for (culprit = 0; culprit < 3; culprit++) {
                        if (prompts[culprit].dirInt != 1 && prompts[culprit].dirInt != 2) break;
                    }
                    culprit = prompts[culprit].dirInt;

                    command = DirToCmd(2, culprit);
                    /**/

                    //MoveAllGyros((float)(command.X * curr.Length()), (float)(command.Y * curr.Length()), (float)(command.Z * curr.Length()));

                    command = DoubleCTM(culprit1.dirInt, culprit1.vLength, culprit2.dirInt, culprit2.vLength);

                    MoveAllGyros((float)(command.X), (float)(command.Y), (float)(command.Z));
                    CorrectionManeuvers();

                    break;

                case MISSILE_STATE.GRAV_ALGN:
                    algn = CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship)));
                    alVec = Vector3D.Subtract(CutVector(DirintToVec(6)), algn);


                    algPr = new List<NavPrompt>();
                    for (int i = 1; i < 7; i++)
                        algPr.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), algn)));
                    if (isAlmostSame(algPr[2].vLength, algPr[3].vLength) && algPr[5].vLength + 0.5 < algPr[4].vLength) {
                        ChangeState(MISSILE_STATE.DUMB_APP_TARGET);
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
                    MoveAllGyros((float)(command.X * 0.6), (float)(command.Y * 0.6), (float)(command.Z * 0.6));

                    break;

                case MISSILE_STATE.DUMB_APP_TARGET:

                    if (curr.Length() <= maxDeviation) {
                        //useMNV = true;
                        SHIP_CONTROLLER.DampenersOverride = true;
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupThrusters(group, 1f);
                    }
                    else { Runtime.UpdateFrequency = UpdateFrequency.Update10; }
                    if (distance < 50 && distance > lastDist) selfDestruct();
                    if (stopFiringEngine()) {
                        if (THRUSTERS.TryGetValue(1, out group)) MoveAGroupThrusters(group, 0f);
                    }
                    if (armPayload(distance, GetSpeed()) || !contrFine) {
                        List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
                        List<IMyWarhead> warheads = new List<IMyWarhead>();

                        GridTerminalSystem.GetBlocksOfType(merges);
                        GridTerminalSystem.GetBlocksOfType(warheads);

                        foreach (IMyWarhead war in warheads) war.IsArmed = true;
                        foreach (IMyShipMergeBlock mer in merges) mer.Enabled = false;

                        /* Making Timer Blocks Redundant since 2019
                        IMyTimerBlock timBl = GridTerminalSystem.GetBlockWithName("AntiMissile/Timer Detach") as IMyTimerBlock;
                        if (timBl != null) {
                            timBl.Trigger();
                        }
                        /**/
                    }


                    command = DoubleCTM(culprit1.dirInt, culprit1.vLength, culprit2.dirInt, culprit2.vLength);
                    /*/
                    for (culprit = 0; culprit < 3; culprit++) {
                        if (prompts[culprit].dirInt != 1 && prompts[culprit].dirInt != 2) break;
                    }
                    culprit = prompts[culprit].dirInt;

                    command = DirToCmd(2, culprit);
                    /**/
                    float mnvAmm = (distance >= 10000) ? (float)curr.Length() * 20f : 1f;

                    if (gravMode && !mbOrbital) {
                        if (culprit1.dirInt == 5)
                            mnvAmm = 1f;
                        else
                        if (culprit1.dirInt == 6) {
                            if (distance > 4000d)
                                mnvAmm = 0f;
                            else
                                mnvAmm = 1f;
                        }
                    }

                    //if (useMNV) DirToMnv(2, culprit1.dirInt, mnvAmm);
                    CorrectionManeuvers();
                    //MoveAllGyros((float)(command.X * curr.Length()), (float)(command.Y * curr.Length()), (float)(command.Z * curr.Length()));
                    MoveAllGyros((float)(command.X), (float)(command.Y), (float)(command.Z));

                    if (distance >= 4000d && gravMode && currELV <= 1000) if (THRUSTERS.TryGetValue(5, out group)) MoveAGroupThrusters(group, 1f);
                    lastDist = distance;

                    Echo("distance: " + distance + " ");
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

        Vector3D GetProjectedPos(Vector3D enPos, Vector3D enSpeed, Vector3D myPos, double mySpeed = maxSpeed) {
            /// do not enter if enSpeed is a "0" vector, or if our speed is 0

            Vector3D
                A = enPos,
                B = myPos;

            double
                t = enSpeed.Length() / mySpeed,         //t -> b = a*t
                projPath,                               //b
                dist = Vector3D.Distance(A, B),  //c
                cos = InterCosine(enSpeed, Vector3D.Subtract(myPos, enPos)),

                delta = 4 * dist * dist * ((1 / t) + cos * cos - 1);


            if (delta < 0) {
                return NOTHING;
            }
            else
            if (delta == 0) {
                if (t == 0) {
                    return NOTHING;
                }
                projPath = -1 * (2 * dist * cos) / (2 - (2 / t * t));
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
                    double 
                        proj1 = (t * t * dist * (cos + Math.Sqrt((1 / t) + cos - 1))) / ((t + 1) * (t - 1)), 
                        proj2 = (t * t * dist * (cos - Math.Sqrt((1 / t) + cos - 1))) / ((t + 1) * (t - 1));
                    /*/
                    projPath = (t * t * dist * (cos + Math.Sqrt((1 / t) + cos - 1))) / ((t + 1) * (t - 1));
                    if (projPath < 0) {
                        projPath = (t * t * dist * (cos - Math.Sqrt((1 / t) + cos - 1))) / ((t + 1) * (t - 1));
                    }
                    /**/
                    projPath = proj1 > proj2? proj2:proj1;
                    if (projPath < 0) {
                        projPath = proj1 < proj2 ? proj2 : proj1;
                        if (projPath < 0) return NOTHING;
                    }
                }

            }

            enSpeed = Vector3D.Normalize(enSpeed);
            enSpeed = Vector3D.Multiply(enSpeed, projPath);

            return Vector3D.Add(enPos, enSpeed);
        }

        Vector3D applyTarSpd(Vector3D position, Vector3D speed) {
            double
                mySpeed = SHIP_CONTROLLER.GetShipVelocities().LinearVelocity.Length(),
                enSpeed = speed.Length();

            if (enSpeed > 0) {
                Vector3D output = GetProjectedPos(position, speed, SHIP_CONTROLLER.CubeGrid.GetPosition()/**/, SHIP_CONTROLLER.GetShipSpeed()/**/);
                if (!output.Equals(NOTHING)) {
                    return output;
                }
            }
            Echo("TO MA RAKA");
            return position;
        }

        public void Main(string argument, UpdateType updateSource) {
            if (SHIP_CONTROLLER == null || !SHIP_CONTROLLER.IsWorking)
                contrFine = GetControllingBlock();

            String[] eval = argument.ToUpper().Split(' ');

            if ((updateSource & UpdateType.IGC) > 0) {
                if (missileListener != null && missileListener.HasPendingMessage) {
                    MyIGCMessage message = missileListener.AcceptMessage();
                    string data = (string)message.Data;
                    string[] bits = data.Split(';');

                    if (bits[0].ToUpper().Equals("TARSET")) {
                        Vector3D 
                            oldTar = TARGET,
                            olCaTa = CAMTAR;


                        ordersGot = true;
                        if (bits.Length > 3) {
                            if (bits.Length > 6) {
                                Vector3D pos, vel;
                                try {
                                    pos = new Vector3D(double.Parse(bits[1]), double.Parse(bits[2]), double.Parse(bits[3]));
                                    vel = new Vector3D(double.Parse(bits[4]), double.Parse(bits[5]), double.Parse(bits[6]));
                                    CAMTAR = pos;
                                    TARGET = applyTarSpd(pos, vel);
                                }
                                catch (Exception e) {
                                    Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
                                    Me.GetSurface(0).WriteText(e.ToString());
                                    TARGET = oldTar;
                                    CAMTAR = olCaTa;
                                    ordersGot = false;
                                }
                            }
                            else {
                                try {
                                    TARGET = new Vector3D(double.Parse(bits[1]), double.Parse(bits[2]), double.Parse(bits[3]));
                                    CAMTAR = TARGET;
                                }
                                catch (Exception e) {
                                    Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
                                    Me.GetSurface(0).WriteText(e.ToString());
                                    TARGET = oldTar;
                                    CAMTAR = olCaTa;
                                    ordersGot = false;
                                }
                            }
                        }
                        return;
                    }
                    else
                    if (data.Equals("ABORT") || (bits.Length > 0 && bits[0].ToUpper().Equals("ABORT"))) {
                        aborting = true;
                        MoveAllGyros(0, 0, 0);
                        OverrideGyros(false);
                        List<IMyThrust> group = new List<IMyThrust>();
                        GridTerminalSystem.GetBlocksOfType(group);
                        MoveAGroupThrusters(group, 0f);
                        if (contrFine) SHIP_CONTROLLER.DampenersOverride = false;
                        selfDestruct();
                        ChangeState(MISSILE_STATE.INIT);
                    }
                }
            }
            else {
                switch (argument.ToUpper()) {
                    case "":
                        if (throttle) {
                            throttle = false;
                            //if(CurrentState > MISSILE_STATE.PREP_LNCH)IGC.SendBroadcastMessage(misCMDTag,"TARGET: ("+string.Format("{0:0.#}",TARGET.X)+"," + string.Format("{0:0.#}", TARGET.Y) + "," + string.Format("{0:0.#}", TARGET.Z) + ")");
                            return;
                        }
                        else {
                            throttle = true;
                            if (ordersGot) ordersGot = false;
                            else {
                                if (timeNR % 10 == 0 && CurrentState >= MISSILE_STATE.DAMPENING) {
                                    Vector3D tango = GetTarget();
                                    if (!tango.Equals(NOTHING)) {
                                        TARGET = tango;
                                        chngTarg = true;
                                    }
                                    else
                                        chngTarg = false;
                                }
                            }
                            ReactToState();
                        }
                        break;

                    //PROGRAM_STATEa
                    default:
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
                                TARGET = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                                ChangeState(MISSILE_STATE.EXIT_LAUNCHPOINT);
                            }
                        }
                        else if (eval[0].Equals("DUMB")) {
                            if (eval.Length > 3) {
                                TARGET = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                                ChangeState(MISSILE_STATE.DUMB_APP_TARGET);
                            }
                        }
                        else if (eval[0].Equals("PREP")) {
                            if (eval.Length > 3) {
                                TARGET = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                                ChangeState(MISSILE_STATE.PREP_LNCH);
                                /**/
                                if(eval.Length>4 && eval[4].Length>0) {
                                    missileTag = eval[4];
                                }
                                /**/
                            }
                        }
                        else if (eval[0].Equals("AUTO")) {
                            Vector3D potTango = AutoTarget(3000d);
                            if (!potTango.Equals(NOTHING)) {
                                TARGET = potTango;
                                ChangeState(MISSILE_STATE.PREP_LNCH);
                                timeNR = -20;
                            }
                        }
                        else if (eval[0].Equals("MANUAL")) {
                            ChangeState(MISSILE_STATE.MANUAL);
                        }
                        else if (eval[0].Equals("SENS")) {
                            getSensorData();
                        }
                        else if (eval[0].Equals("LAUNCHABORT")) {
                            ChangeState(MISSILE_STATE.INIT);
                        }
                        else if (eval[0].Equals("THRTEST")) {
                            FindThrusters(true);
                            List<IMyThrust> thrusters = new List<IMyThrust>();
                            if (THRUSTERS.TryGetValue(6, out thrusters)) {
                                Echo("done!");
                                IMyThrust thruster = thrusters[0];
                                thruster.Enabled = false;
                                thruster.ThrustOverridePercentage = 1f;
                            }
                        }
                        else
                            ChangeState(argument.ToUpper());
                        break;
                }
                string status = "";
                status += Me.CustomName + " " + string.Format("{0:0.##}", currELV) + "m " + CurrentState.ToString() + " " + timeNR + " " + (gravMode ? "- In Gravity" : "- In SPACE");
                if (ordersGot) status = "ORDGOT" + status;
                else
                if (chngTarg) status = "TARCHNG" + status;
                else {
                    //double distance;*/
                    if (CameraList.Count > 0) {
                        CameraList = CameraList.OrderBy(o => o.AvailableScanRange).ToList();
                        status += " " + string.Format("{0:0.}", CameraList[0].AvailableScanRange);
                        status += " " + string.Format("{0:0.}", CameraList[CameraList.Count - 1].AvailableScanRange);
                    }
                }
                status += " " + Runtime.UpdateFrequency;
                if (aborting) antenaText("ABORTING " + status);
                else antenaText(status);
            }
        }
    }
}
