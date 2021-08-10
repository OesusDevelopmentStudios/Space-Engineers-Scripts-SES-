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

        const string SCRIPT_VERSION = "v6.0";
        const bool DEFAULT_DAMPENERS_SETTING = false;
        const float MIN_DISTANCE_TO_TARGET_FOR_ENGINE_DEACTIVATION = 300f;
        const double MAX_LENGTH_OF_DEVIATION_VECTOR = 0.02d;
        const int MINIMUM_SUFFICIENT_NUMBER_OF_CAMERAS = 9;

        MISSILE_STATE CurrentState;

        IMyBroadcastListener
            missileListener;
        readonly string 
            missileTag = "MISSILE-CHN", 
            missileCommandCenterTag = "MISSILE_COMMAND-CHN";

        class Command{
            public double X, Y, Z;

            public static Command
                PITCH_UP = new Command(0, -1, 0),
                PITCH_DOWN = new Command(0, 1, 0),
                YAW_LEFT = new Command(-1, 0, 0),
                YAW_RIGHT = new Command(1, 0, 0),
                ROLL_CLOCKWISE = new Command(0, 0, 1),
                ROLL_ANTICLOCKWISE = new Command(0, 0, -1);

            public Command(double X, double Y, double Z){
                this.X = X;
                this.Y = Y;
                this.Z = Z;
            }
        }

        Command currentCommand;

        Vector3D  
            NOTHING = new Vector3D(44, 44, 44),
            target,
            cameras_target,
            currentMissileLocation, vectorFromMissileToTarget, deviationVector, gravityAlignmentVector,
            centreOfGravityWell, initialMissilePosition;

        const int 
            FORWARD_VECTOR_VALUE    = 2,
            UPWARD_VECTOR_VALUE     = 6,
            LEFTWARD_VECTOR_VALUE   = 4,
            RIGHTWARD_VECTOR_VALUE  = 3,
            BACKWARD_VECTOR_VALUE   = 1,
            DOWNWARD_VECTOR_VALUE   = 5;

        /// END OF CONSTANTS

        int     timeIndex = 0, valueOfTheDeviationOffender;
        readonly int missileOrdinalNumber = 0;

        double  initialMissileSpeed = -1d,
                initialMissileElevation = -1d,
                currentMissileElevation = -1d,
                currentDistanceToTarget = 0,
                currentMissileSpeed,
                previousDistanceToTarget = 999999,

                maxAllowedMissileSpeed = 256d,
                additionalSpeedNeededForExiting = 100d,
                maxAllowedRelativeSpeedAfterDampening = 30d;

        bool    maneuveringThrustersAllowed = false,
                missileIsInGravityWell = false,
                controllerExistsAndWorking = false,
                missileHasPendingOrders = false,
                missileChangedTarget = false,
                skipThisTick = false,
                targetMightBeInOrbit = false,
                payloadIsPrimed = false;
                

        
        IMyShipController SHIP_CONTROLLER;

        List<IMyShipController>             MissileControllers  = new List<IMyShipController>();
        readonly List<IMyShipMergeBlock>    MissileMergers      = new List<IMyShipMergeBlock>();
        readonly List<IMyShipConnector>     MissileConnectors   = new List<IMyShipConnector>();
        readonly List<IMyBatteryBlock>      MissileBatteries    = new List<IMyBatteryBlock>();
        readonly List<IMyGasTank>           MissileHydroTanks   = new List<IMyGasTank>();
        List<IMyCameraBlock>                MissileCameras      = new List<IMyCameraBlock>();
        readonly Dictionary<int, List<IMyThrust>> MissileThrusters = new Dictionary<int, List<IMyThrust>>();

        float thrusterOverrideAmount;


        List<NavPrompt> NavigationalPrompts = new List<NavPrompt>();
        List<NavPrompt> AlignmentPrompts;
        List<IMyThrust> group = new List<IMyThrust>();

        class NavPrompt {
            public int directionInteger;
            public double vectorLength;

            public NavPrompt(int directionInteger, Vector3D inputVector) {
                this.directionInteger = directionInteger;
                this.vectorLength = inputVector.Length();
            }
        }

        enum MISSILE_STATE {
            INITIALIZING,
            PREPARING_FOR_LAUNCH,
            EXITING_LAUNCHPORT,
            GRAVITY_ALIGNMENT,
            DAMPENING,
            APPROACHING_TARGET,
            MANUAL
        }

        void PrepareForLaunch(int ticksToLaunch) {
            if (ticksToLaunch == 120) { foreach (IMyShipConnector con in MissileConnectors) { if (con.CustomName.Equals("Missile/Refuel Connector")) con.Enabled = false; } }
            else if (ticksToLaunch == 80) { foreach (IMyGasTank tank in MissileHydroTanks) { tank.Stockpile = false; } }
            else if (ticksToLaunch == 40) { foreach (IMyBatteryBlock batt in MissileBatteries) { batt.ChargeMode = ChargeMode.Auto; } }
            else if (ticksToLaunch <= 0) {
                foreach (IMyShipMergeBlock mer in MissileMergers) { if (mer.CustomName.Equals("Missile/DMerge Block")) mer.Enabled = false; }
            }
        }

        bool ThisBlockIsOnTheGrid(IMyCubeBlock G) {
            if (G.CubeGrid.Equals(Me.CubeGrid)) return true;
            else return false;
        }

        bool DoublesAreSimilar(double d1, double d2) {
            if (d1 == d2) return true;
            double first = d1 > d2 ? d1 : d2,
                   second = d1 < d2 ? d1 : d2;

            if (first - second < (first / 10)) return true;
            else return false;
        }

        void ChangeState(string state) {
            Output("Changing mode from " + CurrentState + " to " + state + ".");
            switch (state.ToUpper()) {
                case "INIT": ChangeState(MISSILE_STATE.INITIALIZING); break;

                case "DAMP": ChangeState(MISSILE_STATE.DAMPENING); break;

                default: Output("Function 'ChangeState': Undefined input value."); break;
            }
        }

        void ChangeState(MISSILE_STATE state) {
            timeIndex = 0;
            List<IMyThrust> group;
            switch (state) {
                case MISSILE_STATE.INITIALIZING:
                    TurnOffManeuveringThrusters();
                    MoveAllGyros(0, 0, 0);
                    OverrideGyros(false);

                    foreach (IMyShipMergeBlock mer in MissileMergers) { if (mer.CustomName.Equals("Missile/DMerge Block")) mer.Enabled = true; }
                    foreach (IMyShipConnector con in MissileConnectors) { if (con.CustomName.Equals("Missile/Refuel Connector")) con.Enabled = true; }

                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;

                case MISSILE_STATE.PREPARING_FOR_LAUNCH:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.EXITING_LAUNCHPORT:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case MISSILE_STATE.GRAVITY_ALIGNMENT:
                    maneuveringThrustersAllowed = false;
                    if (controllerExistsAndWorking) SHIP_CONTROLLER.DampenersOverride = false;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;

                case MISSILE_STATE.DAMPENING:
                    TurnOffManeuveringThrusters();
                    if (MissileThrusters.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
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
                    maneuveringThrustersAllowed = false;
                    if (missileListener == null) {
                        missileListener = IGC.RegisterBroadcastListener(missileTag);
                        missileListener.SetMessageCallback();
                    }
                    break;

                case MISSILE_STATE.MANUAL:
                    Runtime.UpdateFrequency = UpdateFrequency.Once;
                    TurnOffThrusters();
                    OverrideGyros(false);
                    break;

                default:
                    Output("Function 'ChangeState': Undefined input value.");
                    return;
            }
            CurrentState = state;
        }

        float GetMultiplierForUpdateFrequency() {
            if (Runtime.UpdateFrequency == UpdateFrequency.Update1)
                return 3f;
            else
                return 1f;
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            target = NOTHING;
            DisplayScriptNameOnTextSurfaces(SCRIPT_VERSION);
            Me.CubeGrid.CustomName = "Universal Missile " + SCRIPT_VERSION;

            List<IMyShipController> controls = new List<IMyShipController>();
            MissileControllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controls);
            foreach (IMyShipController cont in controls) { if (ThisBlockIsOnTheGrid(cont) && cont.IsWorking) MissileControllers.Add(cont); }

            ChangeState(MISSILE_STATE.INITIALIZING);
        }

        //TODO: Also fill the other surfaces with something
        void DisplayScriptNameOnTextSurfaces(string ScriptName, float textSize = 10f) {
            IMyTextSurface surface = Me.GetSurface(1);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);

            surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText("\n\nUniversal Missile");
        }

        Vector3D RoundVectorValues(Vector3D vector) { return RoundVectorValues(vector, 3); }

        Vector3D RoundVectorValues(Vector3D vector, int decimalPlace) {
            double 
                X = Math.Round(vector.X, decimalPlace),
                Y = Math.Round(vector.Y, decimalPlace),
                Z = Math.Round(vector.Z, decimalPlace);

            return new Vector3D(X, Y, Z);
        }

        void SetAntennasText(object message) {
            string text = message is string ? (string)message : message.ToString();
            List<IMyRadioAntenna> list = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyRadioAntenna ant in list) if (ThisBlockIsOnTheGrid(ant)) { ant.Radius = CurrentState>MISSILE_STATE.EXITING_LAUNCHPORT? 50000f:1000f; ant.CustomName = text; }
        }

        // CONV

        string VectorValueToName(int dirint) {
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

        Command VectorValueToCommand(int lndDir, int culprit) {
            if (lndDir <= 2) {
                if (culprit <= 4) {
                    if (lndDir % 2 == culprit % 2) return Command.YAW_RIGHT;
                    else return Command.YAW_LEFT; /// LFT
                }
                else {
                    if (lndDir % 2 == culprit % 2) return Command.PITCH_DOWN; /// DWN
                    else return Command.PITCH_UP; /// UPP
                }
            }
            else if (lndDir <= 4) {
                if (culprit <= 4) {
                    if (lndDir % 2 == culprit % 2) return Command.YAW_LEFT; /// LFT
                    else return Command.YAW_RIGHT; /// RIG
                }
                else {
                    if (lndDir % 2 == culprit % 2) return Command.ROLL_ANTICLOCKWISE; /// ALK
                    else return Command.ROLL_CLOCKWISE; /// CLK
                }
            }
            else {
                if (culprit <= 2) {
                    if (lndDir % 2 == culprit % 2) return Command.PITCH_UP; /// UPP
                    else return Command.PITCH_DOWN; /// DWN
                }
                else {
                    if (lndDir % 2 == culprit % 2) return Command.ROLL_CLOCKWISE; /// CLK
                    else return Command.ROLL_ANTICLOCKWISE; /// ALK
                }
            }
        }

        void VectorValueToManeuver(int lndDir, int culprit, float ovrPrc) {
            TurnOffManeuveringThrusters();
            List<IMyThrust> manThr;
            /**/
            if (lndDir == 1 && (culprit == 3 || culprit == 4)) {
                if (culprit == 3) { if (MissileThrusters.TryGetValue(4, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; } }
                else { if (MissileThrusters.TryGetValue(3, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; } }
            }
            else {
                if (MissileThrusters.TryGetValue(culprit, out manThr)) { MoveAGroupOfThrusters(manThr, ovrPrc); return; }
            }
            /**/

        }

        Vector3D VectorValueToVector(int dirint) {
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

        bool ControllingBlockFoundAndApplied() {
            List<IMyShipController> temp = new List<IMyShipController>();
            foreach (IMyShipController cont in MissileControllers) { if (ThisBlockIsOnTheGrid(cont) && cont.IsWorking) temp.Add(cont); }
            MissileControllers = new List<IMyShipController>(temp);

            SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in MissileControllers) {
                if (controler.IsMainCockpit) {
                    SHIP_CONTROLLER = controler;
                    return true;
                }
            }

            foreach (IMyShipController controler in MissileControllers) {
                if (SHIP_CONTROLLER == null && controler.IsWorking && ThisBlockIsOnTheGrid(controler)) {
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

            foreach (IMyGyro gyro in temp) if (ThisBlockIsOnTheGrid(gyro)) list.Add(gyro);

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
                case 0:
                    break;
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
                default:
                    rayTG = NOTHING;
                    break;
            }
            double distance = Vector3D.Distance(camera.GetPosition(), rayTG);
            if (!camera.CanScan(distance))
                camera = GetACameraThatCanScanAtADistance(distance);

            if (camera != null) {
                MyDetectedEntityInfo target = camera.Raycast(rayTG);
                if (!target.IsEmpty() &&
                   ((target.Type == MyDetectedEntityType.LargeGrid || target.Type == MyDetectedEntityType.SmallGrid)
                   && target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)) {
                    Output("\nFOUND YA");
                    cameras_target = target.Position;
                    return CalculateProjectedPositionOfTheTarget(target.Position,target.Velocity);
                }
                else {
                    return FindTargetUsingCameras(++camIndx);
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
                if (ThisBlockIsOnTheGrid(cam)) {
                    cam.EnableRaycast = true;
                    list.Add(cam);
                }
            }

            return list;
        }

        IMyCameraBlock GetACameraThatCanScanAtADistance(double distance = 3000d) {
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
            MissileThrusters.Clear();
            List<IMyThrust> list = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(list);
            foreach (IMyThrust t in list) {
                if (!ThisBlockIsOnTheGrid(t)) continue;
                int dirint = TranslateDirectionToIdentifiableValue(t);
                t.CustomName = VectorValueToName(dirint);
                if (MissileThrusters.TryGetValue(dirint, out temp)) {
                    temp.Add(t);
                    MissileThrusters.Remove(dirint);
                    MissileThrusters.Add(dirint, temp);
                }
                else {
                    temp = new List<IMyThrust> { t };
                    MissileThrusters.Add(dirint, temp);
                }
                //Echo(t.CustomName + " " + t.MaxThrust + "\n");
            }
            bool ok = true;
            if (output) {
                for (int i = 1; i < 7; i++) if (!MissileThrusters.ContainsKey(i)) { ok = false; Output("WARNING: NO " + VectorValueToName(i) + " MissileThrusters."); }
                if (ok) Output("All thrusters found.");
            }
        }

        // END OF FIND

        int TranslateOrientationToIdentifiableValue(MyBlockOrientation o) {
            int translatedFW = TranslateDirectionToIdentifiableValue(o.Forward);
            int translatedUP = TranslateDirectionToIdentifiableValue(o.Up);
            if (translatedFW == 44 || translatedUP == 44) { Output("*ANGERY SIREN NOISES*"); return 444; }
            else
                return translatedFW * 10 + translatedUP;
        }

        int TranslateDirectionToIdentifiableValue(Base6Directions.Direction d) {
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

        int TranslateDirectionToIdentifiableValue(IMyCubeBlock block) {
            int TSL = SHIP_CONTROLLER == null ? 15 : TranslateOrientationToIdentifiableValue(SHIP_CONTROLLER.Orientation);
            int TFW = (TSL / 10);
            int TUP = TSL - TFW * 10;
            if (block is IMyThrust) {
                int blockDir = TranslateDirectionToIdentifiableValue(block.Orientation.Forward);
                if (blockDir == TFW) return FORWARD_VECTOR_VALUE;
                if (blockDir == TUP) return UPWARD_VECTOR_VALUE;
                if (TFW % 2 == 0) {
                    if (blockDir == TFW - 1) return BACKWARD_VECTOR_VALUE;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1) return DOWNWARD_VECTOR_VALUE;
                        else {
                            if (blockDir % 2 == 0) return RIGHTWARD_VECTOR_VALUE;
                            else return LEFTWARD_VECTOR_VALUE;
                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return DOWNWARD_VECTOR_VALUE;
                        else {
                            if (blockDir % 2 == 0) return RIGHTWARD_VECTOR_VALUE;
                            else return LEFTWARD_VECTOR_VALUE;
                        }
                    }
                }
                else {
                    if (blockDir == TFW + 1) return BACKWARD_VECTOR_VALUE;
                    else if (TUP % 2 == 0) {
                        if (blockDir == TUP - 1) return DOWNWARD_VECTOR_VALUE;
                        else {
                            if (blockDir % 2 == 0) return LEFTWARD_VECTOR_VALUE;
                            else return RIGHTWARD_VECTOR_VALUE;
                        }
                    }
                    else {
                        if (blockDir == TUP + 1) return DOWNWARD_VECTOR_VALUE;
                        else {
                            if (blockDir % 2 == 0) return LEFTWARD_VECTOR_VALUE;
                            else return RIGHTWARD_VECTOR_VALUE;
                        }
                    }
                }

            }
            else
            if (block is IMyGyro) {

                int blockDir = TranslateDirectionToIdentifiableValue(block.Orientation.Forward),
                    blockSub = TranslateDirectionToIdentifiableValue(block.Orientation.Up),
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
            Yaw *= GetMultiplierForUpdateFrequency();
            Pitch *= GetMultiplierForUpdateFrequency();
            Roll *= GetMultiplierForUpdateFrequency();
            switch (TranslateDirectionToIdentifiableValue(target)) {
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

        void TurnOffThrusters() {
            List<IMyThrust> list;
            int i = 0;
            for (; i < 7; i++)
                if (MissileThrusters.TryGetValue(i, out list))
                    foreach (IMyThrust tru in list) {
                        tru.ThrustOverride = 0f;
                    }
        }

        void TurnOffManeuveringThrusters() {
            List<IMyThrust> list;
            int i = 2;
            for (; i < 7; i++)
                if (MissileThrusters.TryGetValue(i, out list))
                    foreach (IMyThrust tru in list) {
                        tru.ThrustOverride = 0f;
                    }
        }

        void OverrideGyros(bool doThat) {
            foreach (IMyGyro gyro in GetGyros()) {
                gyro.GyroOverride = doThat;
            }
        }

        void InitializeMissile() {
            MissileCameras = GetCameras();
            IMyShipConnector refCon = null;
            List<IMyShipConnector> cons = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(cons);
            foreach (IMyShipConnector con in cons) {
                if (con.IsWorking && ThisBlockIsOnTheGrid(con) && con.CustomName.Equals("Missile/Refuel Connector")) {
                    refCon = con;
                    break;
                }
            }

            if (MissileCameras.Count < MINIMUM_SUFFICIENT_NUMBER_OF_CAMERAS || refCon == null || refCon.Status != MyShipConnectorStatus.Connected) return;
            else {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Me.CustomName = "MISSILE-" + refCon.OtherConnector.CustomData;
            }

            FindThrusters();

            List<IMyShipMergeBlock> list1 = new List<IMyShipMergeBlock>();
            List<IMyShipConnector> list2 = new List<IMyShipConnector>();
            List<IMyBatteryBlock> list3 = new List<IMyBatteryBlock>();
            List<IMyGasTank> list4 = new List<IMyGasTank>();

            GridTerminalSystem.GetBlocksOfType(list1);
            GridTerminalSystem.GetBlocksOfType(list2);
            GridTerminalSystem.GetBlocksOfType(list3);
            GridTerminalSystem.GetBlocksOfType(list4);

            foreach (IMyShipMergeBlock mer in list1) if (ThisBlockIsOnTheGrid(mer)) MissileMergers.Add(mer);
            foreach (IMyShipConnector con in list2) if (ThisBlockIsOnTheGrid(con)) MissileConnectors.Add(con);
            foreach (IMyBatteryBlock bat in list3) if (ThisBlockIsOnTheGrid(bat)) MissileBatteries.Add(bat);
            foreach (IMyGasTank hdt in list4) if (ThisBlockIsOnTheGrid(hdt)) { MissileHydroTanks.Add(hdt); hdt.Stockpile = true; }
        }

        double GetCurrentMissileSpeed() {
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

        void CheckIfMissileIsInGravityWell() {
            if (initialMissileElevation == -1) {
                if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out initialMissileElevation)) initialMissileElevation = -1;
            }
            double temp = currentMissileElevation;
            if (!SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentMissileElevation)) currentMissileElevation = temp;

            if (SHIP_CONTROLLER.TryGetPlanetPosition(out centreOfGravityWell)) {
                missileIsInGravityWell = true;
                maneuveringThrustersAllowed = true;
                maxAllowedMissileSpeed = 340d;
                additionalSpeedNeededForExiting = 100d;
                maxAllowedRelativeSpeedAfterDampening = 100d;
            }
            else {
                missileIsInGravityWell = false;
                maneuveringThrustersAllowed = false;
                maxAllowedMissileSpeed = 340d;
                additionalSpeedNeededForExiting = 100d;
                maxAllowedRelativeSpeedAfterDampening = 30d;
            }
        }

        void SendMessageToMissileCommand(string message) { IGC.SendBroadcastMessage(missileCommandCenterTag, message); }

        bool MissileShouldArmPayload(double distance, double speed) {
            speed = speed < 200 ? 200 : speed;
            return distance <= (speed / 2);
        }

        bool MissileShouldTurnMainThrustOff() {
            if (!controllerExistsAndWorking) return true;

            Vector3D
                velocity = Vector3D.Normalize(SHIP_CONTROLLER.GetShipVelocities().LinearVelocity),
                forward = SHIP_CONTROLLER.WorldMatrix.Forward,
                diff = Vector3D.Subtract(velocity, forward);

            ///    New Part               && Classic Part
            return (diff.Length() < 0.5d) && (currentDistanceToTarget <= GetCurrentMissileSpeed() + MIN_DISTANCE_TO_TARGET_FOR_ENGINE_DEACTIVATION || GetCurrentMissileSpeed() > maxAllowedMissileSpeed);
        }

        void ComputeNavigationalData(){
            currentMissileLocation = SHIP_CONTROLLER.GetPosition();
            vectorFromMissileToTarget = target == null ? currentMissileLocation : RoundVectorValues(Vector3D.Normalize(Vector3D.Subtract(target, currentMissileLocation)));
            deviationVector = NOTHING;
            CheckIfMissileIsInGravityWell();

            currentDistanceToTarget = 0;
            currentMissileSpeed = controllerExistsAndWorking ? GetCurrentMissileSpeed() : initialMissileSpeed;
        }

        void ComputeTargetRelatedData(){
            NavigationalPrompts = new List<NavPrompt>();
            deviationVector = Vector3D.Subtract(RoundVectorValues(VectorValueToVector(1)), vectorFromMissileToTarget);
            currentDistanceToTarget = Vector3D.Subtract(target, currentMissileLocation).Length();
            for (int i = 1; i < 7; i++)
                NavigationalPrompts.Add(new NavPrompt(i, Vector3D.Subtract(RoundVectorValues(VectorValueToVector(i)), vectorFromMissileToTarget)));
            NavigationalPrompts = NavigationalPrompts.OrderBy(o => o.vectorLength).ToList();
        }

        void PerformStateSpecificWork() {
            if (CurrentState > MISSILE_STATE.INITIALIZING) {
                timeIndex++;
                ComputeNavigationalData();
            }

            if (CurrentState > MISSILE_STATE.PREPARING_FOR_LAUNCH) {
                ComputeTargetRelatedData();
                group = new List<IMyThrust>();
            }

            switch (CurrentState) {
                case MISSILE_STATE.INITIALIZING:
                    InitializeMissile();
                    break;

                case MISSILE_STATE.PREPARING_FOR_LAUNCH:
                    if (timeIndex > 200) {
                        initialMissilePosition = Me.GetPosition();
                        ChangeState(MISSILE_STATE.EXITING_LAUNCHPORT);
                    }
                    PrepareForLaunch(200 - timeIndex);
                    break;

                case MISSILE_STATE.EXITING_LAUNCHPORT:
                    if (initialMissileSpeed == -1d)
                        initialMissileSpeed = controllerExistsAndWorking ? SHIP_CONTROLLER.GetShipSpeed() : 0d;

                    thrusterOverrideAmount = 1f - (missileOrdinalNumber - 1) * 0.05f;

                    // part below seems to be needed, because missile does not want to exit the launchpoint otherwise
                    if (currentMissileElevation <= initialMissileElevation && initialMissileElevation != -1) {
                        if (MissileThrusters.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
                        if (MissileThrusters.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, thrusterOverrideAmount);
                    }

                    bool abortNormal = false;
                    if (!missileIsInGravityWell) {
                        if (currentMissileSpeed >= initialMissileSpeed + 30d) {
                            for (valueOfTheDeviationOffender = 0; valueOfTheDeviationOffender < 5; valueOfTheDeviationOffender++) {
                                if (NavigationalPrompts[valueOfTheDeviationOffender].directionInteger == 1) {
                                    abortNormal = true;
                                    break;
                                }
                            }
                        }
                    }
                    else additionalSpeedNeededForExiting = 9999;

                    double disToTarget = Vector3D.Distance(initialMissilePosition, target);
                    if (initialMissileElevation != -1 && currentMissileElevation != -1) {
                        double addELV = disToTarget >= 2000 ? disToTarget / 20 : 100d;
                        if (addELV > 500) addELV = 500;
                        if (initialMissileElevation + addELV <= currentMissileElevation) abortNormal = true;
                    }

                    if (currentMissileSpeed >= initialMissileSpeed + additionalSpeedNeededForExiting || abortNormal) {
                        ChangeState(MISSILE_STATE.DAMPENING);
                    }
                    else if (MissileThrusters.TryGetValue(1, out group)) {
                        MoveAGroupOfThrusters(group, thrusterOverrideAmount);
                    }
                    break;

                case MISSILE_STATE.DAMPENING:
                    if (deviationVector != null && deviationVector.Length() <= 0.25d) {
                        if (deviationVector.Length() <= MAX_LENGTH_OF_DEVIATION_VECTOR || GetCurrentMissileSpeed() <= initialMissileSpeed + maxAllowedRelativeSpeedAfterDampening) {
                            if (!missileIsInGravityWell) ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                            else {
                                gravityAlignmentVector = RoundVectorValues(Vector3D.Normalize(Vector3D.Subtract(centreOfGravityWell, currentMissileLocation)));

                                AlignmentPrompts = new List<NavPrompt>();
                                for (int i = 1; i < 7; i++)
                                    AlignmentPrompts.Add(new NavPrompt(i, Vector3D.Subtract(RoundVectorValues(VectorValueToVector(i)), gravityAlignmentVector)));
                                AlignmentPrompts = AlignmentPrompts.OrderBy(o => o.vectorLength).ToList();

                                if (AlignmentPrompts[0].directionInteger == 2 || AlignmentPrompts[1].directionInteger == 2) {
                                    targetMightBeInOrbit = true;
                                    ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                                }
                                else {
                                    targetMightBeInOrbit = false;
                                    ChangeState(MISSILE_STATE.GRAVITY_ALIGNMENT);
                                }
                            }
                            return;
                        }
                    }
                    else {
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    }

                    for (valueOfTheDeviationOffender = 0; valueOfTheDeviationOffender < 3; valueOfTheDeviationOffender++) {
                        if (NavigationalPrompts[valueOfTheDeviationOffender].directionInteger != 1 && NavigationalPrompts[valueOfTheDeviationOffender].directionInteger != 2) break;
                    }
                    valueOfTheDeviationOffender = NavigationalPrompts[valueOfTheDeviationOffender].directionInteger;

                    currentCommand = VectorValueToCommand(2, valueOfTheDeviationOffender);
                    MoveAllGyros((float)(currentCommand.X * deviationVector.Length()), (float)(currentCommand.Y * deviationVector.Length()), (float)(currentCommand.Z * deviationVector.Length()));
                    VectorValueToManeuver(2, valueOfTheDeviationOffender, 0.1F);

                    break;

                case MISSILE_STATE.GRAVITY_ALIGNMENT:
                    gravityAlignmentVector = RoundVectorValues(Vector3D.Normalize(Vector3D.Subtract(centreOfGravityWell, currentMissileLocation)));

                    AlignmentPrompts = new List<NavPrompt>();
                    for (int i = 1; i < 7; i++)
                        AlignmentPrompts.Add(new NavPrompt(i, Vector3D.Subtract(RoundVectorValues(VectorValueToVector(i)), gravityAlignmentVector)));
                    if (DoublesAreSimilar(AlignmentPrompts[2].vectorLength, AlignmentPrompts[3].vectorLength) && AlignmentPrompts[5].vectorLength + 0.5 < AlignmentPrompts[4].vectorLength) {
                        ChangeState(MISSILE_STATE.APPROACHING_TARGET);
                        MoveAllGyros(0, 0, 0);
                        OverrideGyros(false);
                        return;
                    }
                    AlignmentPrompts = AlignmentPrompts.OrderBy(o => o.vectorLength).ToList();

                    for (valueOfTheDeviationOffender = 0; valueOfTheDeviationOffender < 3; valueOfTheDeviationOffender++) {
                        if (AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 5 && AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 6) break;
                    }
                    valueOfTheDeviationOffender = AlignmentPrompts[valueOfTheDeviationOffender].directionInteger;
                    if (valueOfTheDeviationOffender == 1 || valueOfTheDeviationOffender == 2) {
                        for (valueOfTheDeviationOffender = 0; valueOfTheDeviationOffender < 3; valueOfTheDeviationOffender++) {
                            if (AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 5 && AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 6 && AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 1 && AlignmentPrompts[valueOfTheDeviationOffender].directionInteger != 2) break;
                        }
                        valueOfTheDeviationOffender = AlignmentPrompts[valueOfTheDeviationOffender].directionInteger;
                    }

                    currentCommand = VectorValueToCommand(5, valueOfTheDeviationOffender);
                    MoveAllGyros((float)(currentCommand.X * 0.3), (float)(currentCommand.Y * 0.3), (float)(currentCommand.Z * 0.3));

                    break;

                case MISSILE_STATE.APPROACHING_TARGET:
                    if (deviationVector.Length() <= MAX_LENGTH_OF_DEVIATION_VECTOR) {
                        maneuveringThrustersAllowed = true;
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        if (MissileThrusters.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 1f);
                    }
                    else {
                        maneuveringThrustersAllowed = false;
                    }
                    if (currentDistanceToTarget < 50 && currentDistanceToTarget > previousDistanceToTarget) SelfDestruct();
                    if (MissileShouldTurnMainThrustOff()) {
                        if (MissileThrusters.TryGetValue(1, out group)) MoveAGroupOfThrusters(group, 0f);
                    }
                    if ((MissileShouldArmPayload(currentDistanceToTarget, GetCurrentMissileSpeed()) || !controllerExistsAndWorking) && !payloadIsPrimed) {
                        payloadIsPrimed = true;
                        SendMessageToMissileCommand("BOOM;" + missileTag);

                        List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
                        List<IMyWarhead> warheads = new List<IMyWarhead>();

                        GridTerminalSystem.GetBlocksOfType(merges);
                        GridTerminalSystem.GetBlocksOfType(warheads);

                        foreach (IMyWarhead war in warheads) war.IsArmed = true;
                        foreach (IMyShipMergeBlock mer in merges) mer.Enabled = false;
                    }

                    for (valueOfTheDeviationOffender = 0; valueOfTheDeviationOffender < 3; valueOfTheDeviationOffender++) {
                        if (NavigationalPrompts[valueOfTheDeviationOffender].directionInteger != 1 && NavigationalPrompts[valueOfTheDeviationOffender].directionInteger != 2) break;
                    }
                    valueOfTheDeviationOffender = NavigationalPrompts[valueOfTheDeviationOffender].directionInteger;

                    currentCommand = VectorValueToCommand(2, valueOfTheDeviationOffender);
                    float mnvAmm = (currentDistanceToTarget >= 10000) ? (float)deviationVector.Length() * 20f : 1f;

                    if (missileIsInGravityWell && !targetMightBeInOrbit) {
                        if (valueOfTheDeviationOffender == 5)
                            mnvAmm = 1f;
                        else
                        if (valueOfTheDeviationOffender == 6) {
                            if (currentDistanceToTarget > 4000d)
                                mnvAmm = 0f;
                            else
                                mnvAmm = 1f;
                        }
                    }

                    if (valueOfTheDeviationOffender == 3) {
                        valueOfTheDeviationOffender = 4;
                    }
                    else
                    if (valueOfTheDeviationOffender == 4) {
                        valueOfTheDeviationOffender = 3;
                    }

                    if (maneuveringThrustersAllowed) VectorValueToManeuver(2, valueOfTheDeviationOffender, mnvAmm);
                    MoveAllGyros((float)(currentCommand.X * deviationVector.Length()), (float)(currentCommand.Y * deviationVector.Length()), (float)(currentCommand.Z * deviationVector.Length()));

                    if (currentDistanceToTarget >= 4000d && missileIsInGravityWell && currentMissileElevation <= 1000) if (MissileThrusters.TryGetValue(5, out group)) MoveAGroupOfThrusters(group, 1f);
                    previousDistanceToTarget = currentDistanceToTarget;

                    Echo(" " + currentDistanceToTarget + " ");
                    break;

                case MISSILE_STATE.MANUAL:
                    break;
            }
        }

        void Output(Object input) {
            string message = (input is string) ? (string)input : input.ToString();
            Echo(message);
        }

        double GetInnerCosineBetweenVectors(Vector3D first, Vector3D second) {
            double 
                scalarProduct   = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                productOfLengths   = first.Length() * second.Length();

            return scalarProduct / productOfLengths;
        }

        Vector3D CalculateProjectedPositionOfTheTarget(Vector3D enPos, Vector3D enSpeed, Vector3D myPos, double speed) {
            /// do not enter if enSpeed is a "0" vector, or if our speed is 0
            if (speed <= 0) speed = 1;

            /// A = enPos, B = myPos, C is the estimated meeting point

            double
                t = enSpeed.Length() / speed,           //t -> b = a*t  
                projPath,                               //b
                dist = Vector3D.Distance(enPos, myPos), //c
                cos = GetInnerCosineBetweenVectors(enSpeed, Vector3D.Subtract(myPos, enPos)),

                // pre 10-08-2021
                // delta = 4 * dist * dist * ((1 / (t * t)) + (cos * cos) - 1);

                // post 10-08-2021
                // delta = 4c^2(t^2cos^2 - t^2 + 1)
                delta = 4 * (dist * dist) * ((t * t * cos * cos) - (t * t) + 1);

            if (delta < 0) {
                return NOTHING;
            }
            else
            if (delta == 0) {
                // pre 10-08-2021
                // projPath = -1 * (2 * dist * cos) / (2 * (((t * t) - 1) / (t * t)));
                
                // post 10-08-2021
                projPath = ((t * dist * cos)/((t * t) - 1));
            }
            else {
                // pre 10-08-2021
                // projPath = ((2 * dist * cos - Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                // if (projPath < 0) {
                //     projPath = ((2 * dist * cos + Math.Sqrt(delta)) / (2 * (((t * t) - 1) / (t * t))));
                // }
                
                // post 10-08-2021
                if((projPath = (((2 * t * dist * cos) + Math.Sqrt(delta))/(2 * ((t * t) - 1)))) < 0) {
                    projPath = (((2 * t * dist * cos) - Math.Sqrt(delta))/(2 * ((t * t) - 1)));
                }
            }
            projPath *= t; /// projPath = a, so, to get b, we need to a*t = b

            enSpeed = Vector3D.Normalize(enSpeed);
            enSpeed = Vector3D.Multiply(enSpeed, projPath);

            return Vector3D.Add(enPos, enSpeed);
        }
        
        Vector3D CalculateProjectedPositionOfTheTarget(Vector3D position, Vector3D speed) {
            double
                mySpeed = SHIP_CONTROLLER.GetShipVelocities().LinearVelocity.Length(),
                enSpeed = speed.Length(),
                multiplier;
                
            if(enSpeed > 0) {
                Vector3D output = CalculateProjectedPositionOfTheTarget(position, speed, SHIP_CONTROLLER.CubeGrid.GetPosition(),mySpeed);
                if (!output.Equals(NOTHING)) {
                    return output;
                }
            }

            multiplier = (mySpeed!=0 && enSpeed!=0)? (enSpeed/mySpeed):0;

            Vector3D
                addition = Vector3D.Multiply(speed, multiplier);

            return Vector3D.Add(position, addition);
        }

        void EvaluateCommandInput(string argument){
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

        void EvaluateRadioMessageInput(){
            if (missileListener != null && missileListener.HasPendingMessage) {
                MyIGCMessage message = missileListener.AcceptMessage();
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
                                target = CalculateProjectedPositionOfTheTarget(pos,vel);
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
        }

        void OutputStatusOnTheAntenna(){
            string status = "";
            status += Me.CustomName + " " + string.Format("{0:0.##}", currentMissileElevation) + "m " + CurrentState.ToString() + " " + timeIndex + " " + (missileIsInGravityWell ? "- In Gravity" : "- In SPACE");
            if (missileHasPendingOrders) status = "ORDGOT" + status;
            else
            if (missileChangedTarget) status = "TARCHNG" + status;
            else {
                if (MissileCameras.Count > 0) {
                    MissileCameras = MissileCameras.OrderBy(o => o.AvailableScanRange).ToList();
                    status += " " + string.Format("{0:0.}", MissileCameras[0].AvailableScanRange);
                    status += " " + string.Format("{0:0.}", MissileCameras[MissileCameras.Count - 1].AvailableScanRange);
                }
            }
            status += " " + Runtime.UpdateFrequency;
            SetAntennasText(status);
        }


        public void Main(string argument, UpdateType updateSource) {
            if (SHIP_CONTROLLER == null || !SHIP_CONTROLLER.IsWorking)
                controllerExistsAndWorking = ControllingBlockFoundAndApplied();
            if ((updateSource & UpdateType.IGC) > 0) {
                EvaluateRadioMessageInput();
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
                            if (missileHasPendingOrders) missileHasPendingOrders = false;
                            else {
                                if (timeIndex % 10 == 0 && CurrentState >= MISSILE_STATE.DAMPENING) {
                                    Vector3D tango = FindTargetUsingCameras();
                                    if (!tango.Equals(NOTHING)) {
                                        target = tango;
                                        missileChangedTarget = true;
                                    }
                                    else
                                        missileChangedTarget = false;
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