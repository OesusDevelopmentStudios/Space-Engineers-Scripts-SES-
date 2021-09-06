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
        const bool      DEFAULT_DAMPENERS_SETTING = true;
        const float     GRAV_ACC_CONST = 9.81f;
        const double    maxDeviation = 0.005d;
        const bool      allowOnCockpit = false;
        const int       CARGO_MULTIPLIER = 10;

        Vector3D    UPP_CMD = new Vector3D( 0,-1, 0),
                    DWN_CMD = new Vector3D( 0, 1, 0),
                    LFT_CMD = new Vector3D(-1, 0, 0),
                    RIG_CMD = new Vector3D( 1, 0, 0),
                    CLK_CMD = new Vector3D( 0, 0, 1),
                    ALK_CMD = new Vector3D( 0, 0,-1);

        const int FW_VAL = 2,
                  UP_VAL = 6,
                  LF_VAL = 3,
                  RT_VAL = 4,
                  BW_VAL = 1,
                  DW_VAL = 5;

        class CosmicBodyDatabase {
            static readonly CosmicBody[] content = {
                new CosmicBody(new Vector3D(0d, 0d, 0d), 1f, true, "Earth"),
                new CosmicBody(new Vector3D(16384d, 136384d, -113616d), 0.25f, false, "Moon"),
                new CosmicBody(new Vector3D(1031072d, 131072d, 1631072d), 0.9f, true, "Mars"),
                new CosmicBody(new Vector3D(916384d, 16384d, 1616384d), 0.25f, true, "Europa"),
                new CosmicBody(new Vector3D(131072d, 131072d, 5731072d), 1.1f, true, "Alien Planet"),
                new CosmicBody(new Vector3D(36384d, 226384d, 5796384d), 0.25f, true, "Titan"),
                new CosmicBody(new Vector3D(-284463d, -2434463d, 365536), 1f, true, "Triton"),
            };

            public static bool TryGet(string name, out CosmicBody body) {
                foreach(CosmicBody b in content) {
                    if(b.name.ToLower().Equals(name.ToLower())) { body = b; return true; }
                }
                body = null; return false;
            }

            public static bool TryGet(Vector3D coords, out CosmicBody body) {
                foreach (CosmicBody b in content) {
                    if (b.coords.Equals(coords)) { body = b; return true; }
                }
                body = null; return false;
            }

            public static CosmicBody GetClosestBody(Vector3D coords) {
                double distance = -1d, contender;
                CosmicBody output = null;
                foreach(CosmicBody b in content) {
                    if (distance > (contender = Vector3D.Distance(coords, b.coords)) || distance == -1D) {
                        distance = contender; output = b;
                    }
                }
                return output;
            }
        }

        CosmicBody CurrentTarget;

        Vector3D NOTHING_CRDS = new Vector3D(-0.5d, -0.5d, -0.5d);

        class CosmicBody {
            public string       name;
            public Vector3D     coords;
            public float        gravity;
            public bool         hasAtmo;
            public double       gravBound;

            public CosmicBody(Vector3D coords,float gravity,bool hasAtmo, string name) {
                this.coords     = coords;
                this.gravity    = gravity;
                this.hasAtmo    = hasAtmo;
                this.name       = name;
            }

            public CosmicBody(Vector3D coords, float gravity, bool hasAtmo, string name, double gravBound): this(coords,gravity,hasAtmo,name) {
                this.gravBound  = gravBound;
            }
        }
        
        class NavPrompt {
            public int dirInt;
            public double vLength;

            public NavPrompt(int dir, Vector3D input) {
                this.dirInt = dir;
                this.vLength = input.Length();
            }
        }

        IMyShipController SHIP_CONTROLLER;
        IMyTextPanel CONTROL_SCREEN;
        readonly string ShipName = "";
        int stdby_decr,
            landingDir;
        float TWR = 1f;

        PROGRAM_STATE CurrentState,
        NextState = PROGRAM_STATE.INIT;

        readonly Dictionary<int, List<IMyThrust>> THRUSTERS = new Dictionary<int, List<IMyThrust>>();

        Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            string name = Me.CubeGrid.CustomName;
            string[] split = name.Split(' ');
            if (!(split.Length > 1 && split[1].ToUpper().Equals("GRID"))) { ShipName = name; }
            SayMyName("SHIP CONTROL");
        }

        public void Save() {
            Storage = CurrentState.ToString();
        }

        string GetFullScriptName(string ScriptName) { return "[" + ScriptName + "] Script"; }
        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = GetFullScriptName(ScriptName);
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        public void STDBYS(int time) { STDBYT(time * 60, CurrentState); }

        public void STDBYS(int time, PROGRAM_STATE state) { STDBYT(time * 60, state); }

        public void STDBYT(int time) { STDBYT(time, CurrentState); }

        public void STDBYT(int time, PROGRAM_STATE state) {
            CurrentState = PROGRAM_STATE.STDBY;
            NextState = state;
            if (time < 30) {
                stdby_decr = time;
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            if (time < 300) {
                stdby_decr = time / 10;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
            }
            else {
                stdby_decr = time / 100;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }
        }

        public void ClearShipThrusters() {
            THRUSTERS.Clear();
        }

        public enum PROGRAM_STATE {
            INIT,
            LND_INFO,
            LND_ALGN,
            LND_AUTO,
            LND_DAMP,
            STDBY
        }

        public float NtoKg(float input) { return input / GRAV_ACC_CONST; }

        public float KgtoN(float input) { return input * GRAV_ACC_CONST; }

        public float Evaluate(IMyThrust thrust) {
            if (CurrentTarget==null) {
                return thrust.MaxEffectiveThrust;
            }
            else {
                if (CurrentTarget.hasAtmo) {
                    if      (thrust.BlockDefinition.SubtypeName.Contains("Hydrogen")) return thrust.MaxThrust;
                    else if (thrust.BlockDefinition.SubtypeName.Contains("Atmospheric")) return thrust.MaxThrust;
                    else return thrust.MaxThrust/5f;
                }
                else {
                    if (thrust.BlockDefinition.SubtypeName.Contains("Atmospheric")) return 0f;
                    else return thrust.MaxThrust;
                }
            }
        }

        public float GetThrust(List<IMyThrust> list) {
            float inp = 0f;
            foreach (IMyThrust t in list) {
                if (t.IsWorking)
                    inp += Evaluate(t);
            }
            return inp;
        }

        public bool GetScreen() {
            CONTROL_SCREEN = GridTerminalSystem.GetBlockWithName(ShipName + "/SCS") as IMyTextPanel;
            if (CONTROL_SCREEN == null)
                return false;
            else {
                CONTROL_SCREEN.ContentType = ContentType.TEXT_AND_IMAGE;
                return true;
            }
        }

        public void InitShip() {
            FindThrusters();
            GetGyros(true);
        }

        public int TranslateOrientation(MyBlockOrientation o) {
            int translatedFW = TranslateDirection(o.Forward);
            int translatedUP = TranslateDirection(o.Up);
            if (translatedFW == 44 || translatedUP == 44) { Output("*ANGERY SIREN NOISES*"); return 444; }
            else
                return translatedFW * 10 + translatedUP;
        }

        public int TranslateDirection(VRageMath.Base6Directions.Direction d) {
            switch (d) {
                case Base6Directions.Direction.Forward: return 1;
                case Base6Directions.Direction.Backward:return 2;
                case Base6Directions.Direction.Left:    return 3;
                case Base6Directions.Direction.Right:   return 4;
                case Base6Directions.Direction.Up:      return 5;
                case Base6Directions.Direction.Down:    return 6;
                default:Output("*ANGERY SIREN NOISES*");return 44;
            }
        }

        public int TranslateDirection(IMyCubeBlock block) {
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
                    blockSub = TranslateDirection(block.Orientation.Up), firstDigit;

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

        public string DirintToName(int dirint) {
            switch (dirint) {
                case 1: return "FORWARD ";
                case 2: return "BACKWARD";
                case 3: return "LEFT    ";
                case 4: return "RIGHT   ";
                case 5: return "UP      ";
                case 6: return "DOWN    ";
                default:return "ERROR   ";
            }
        }

        public Vector3D DirToCmd(int lndDir, int culprit) {
            if(lndDir<=2) {
                if (culprit <=4) {
                    if (lndDir % 2 == culprit % 2) return RIG_CMD;
                    else return LFT_CMD; /// LFT
                }
                else {
                    if (lndDir % 2 == culprit % 2)  return DWN_CMD; /// DWN
                    else                            return UPP_CMD; /// UPP
                }
            }
            else if(lndDir<=4) {
                if (culprit <= 4) {
                    if (lndDir % 2 == culprit % 2)  return LFT_CMD; /// LFT
                    else                            return RIG_CMD; /// RIG
                }
                else {
                    if (lndDir % 2 == culprit % 2)  return ALK_CMD; /// ALK
                    else                            return CLK_CMD; /// CLK
                }
            }
            else {
                if (culprit <= 2) {
                    if (lndDir % 2 == culprit % 2)  return UPP_CMD; /// UPP
                    else                            return DWN_CMD; /// DWN
                }
                else {
                    if (lndDir % 2 == culprit % 2)  return CLK_CMD; /// CLK
                    else                            return ALK_CMD; /// ALK
                }
            }
        }

        public Vector3D DirintToVec(int dirint) {
            switch (dirint) {
                case 1: return SHIP_CONTROLLER.WorldMatrix.Forward;
                case 2: return SHIP_CONTROLLER.WorldMatrix.Backward;
                case 3: return SHIP_CONTROLLER.WorldMatrix.Left;
                case 4: return SHIP_CONTROLLER.WorldMatrix.Right;
                case 5: return SHIP_CONTROLLER.WorldMatrix.Up;
                case 6: return SHIP_CONTROLLER.WorldMatrix.Down;
            } return NOTHING_CRDS;
        }

        public Vector3D DirintToLndVec(int dirint) {
            switch (dirint) {
                case 1: return SHIP_CONTROLLER.WorldMatrix.Backward;
                case 2: return SHIP_CONTROLLER.WorldMatrix.Forward;
                case 3: return SHIP_CONTROLLER.WorldMatrix.Right;
                case 4: return SHIP_CONTROLLER.WorldMatrix.Left;
                case 5: return SHIP_CONTROLLER.WorldMatrix.Down;
                case 6: return SHIP_CONTROLLER.WorldMatrix.Up;
            } return NOTHING_CRDS;
        }

        public void LndGyroMove(float fir, float sec) {
            switch (landingDir) {
                case 1:
                case 2:
                    MoveAllGyros(fir,sec,0);
                    break;

                case 3:
                case 4:
                    MoveAllGyros(fir,0,sec);
                    break;

                case 5:
                case 6:
                    MoveAllGyros(0,fir,sec);
                    break;
            }
        }

        public void OverrideGyros(bool doThat) {
            foreach(IMyGyro gyro in GetGyros()) {
                gyro.GyroOverride = doThat;
            }
        }

        public void OverrideThrusters(bool doThat) {
            if (doThat) return;
            for (int i = 0; i < 7; i++) {
                List<IMyThrust> group;
                if (THRUSTERS.TryGetValue(i, out group)) {
                    foreach(IMyThrust thr in group) {
                        thr.ThrustOverride = 0;
                    }
                }
            }
        }

        public bool IsOnThisGrid(IMyCubeBlock block) {
            return (block != null && block.CubeGrid.Equals(Me.CubeGrid));
        }

        public void MoveAGroupThrusters(List<IMyThrust> Group, float OverridePercent) {
            foreach (IMyThrust Thruster in Group) {
                Thruster.ThrustOverridePercentage = OverridePercent;
            }
        }

        public void EnableAGroupThrusters(List<IMyThrust> Group, bool Enable) {
            foreach (IMyThrust Thruster in Group) {
                Thruster.Enabled = Enable;
            }
        }

        public void MoveAllGyros(float Yaw, float Pitch, float Roll) {
            List<IMyGyro> gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType(gyros);
            foreach (IMyGyro gyro in gyros) {
                MoveGyroInAWay(gyro, Yaw, Pitch, Roll);
            }
        }

        public void MoveGyroInAWay(IMyGyro target, float Yaw, float Pitch, float Roll) {
            target.GyroOverride = true; float[] command;
            switch (TranslateDirection(target)) {
                case 13: command = { Roll , Yaw   , Pitch}; break;
                case 14: command = { Roll ,-Yaw   ,-Pitch}; break;
                case 15: command = { Roll ,-Pitch , Yaw  }; break;
                case 16: command = { Roll , Pitch ,-Yaw  }; break;
                case 23: command = {-Roll ,-Yaw   , Pitch}; break;
                case 24: command = {-Roll , Yaw   ,-Pitch}; break;
                case 25: command = {-Roll , Pitch , Yaw  }; break;
                case 26: command = {-Roll ,-Pitch ,-Yaw  }; break;
                case 31: command = { Pitch,-Yaw   ,-Roll }; break;
                case 32: command = {-Pitch, Yaw   , Roll }; break;
                case 35: command = {-Pitch,-Roll  , Yaw  }; break;
                case 36: command = {-Pitch, Roll  ,-Yaw  }; break;
                case 41: command = { Pitch, Yaw   ,-Roll }; break;
                case 42: command = { Pitch, Yaw   , Roll }; break;
                case 45: command = { Pitch, Roll  , Yaw  }; break;
                case 46: command = { Pitch,-Roll  ,-Yaw  }; break;
                case 51: command = {-Yaw  , Pitch ,-Roll }; break;
                case 52: command = {-Yaw  ,-Pitch , Roll }; break;
                case 53: command = {-Yaw  ,-Roll  , Pitch}; break;
                case 54: command = {-Yaw  ,-Roll  ,-Pitch}; break;
                case 61: command = { Yaw  ,-Pitch ,-Roll }; break;
                case 62: command = { Yaw  , Pitch , Roll }; break;
                case 63: command = { Yaw  ,-Roll  , Pitch}; break;
                case 64: command = { Yaw  ,-Roll  ,-Pitch}; break;
                default:
                    Output("ERROR: " + target.CustomName + " GYROSCOPE IS IN AN IMPOSSIBLE SETTING."); target.ShowOnHUD = true;
                    return;
            }
            target.Yaw  = command[0]; target.Pitch= command[1]; target.Roll = command[2];
        }

        public bool GetControllingBlock() {
            List<IMyShipController> controls = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controls);

            SHIP_CONTROLLER = null;
            foreach (IMyShipController controler in controls) {
                if (controler.IsMainCockpit && controler.IsWorking) {
                    SHIP_CONTROLLER = controler;
                    return true;
                }
            }

            foreach (IMyShipController controler in controls) {
                if (SHIP_CONTROLLER == null && controler.IsWorking && Me.CubeGrid.Equals(controler.CubeGrid)) {
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

        public double GetSpeed() { return GetControllingBlock()? SHIP_CONTROLLER.GetShipSpeed():-1D; }

        public float GetMass() { return GetMass("T"); }

        public float GetMass(string input) {
            if (!GetControllingBlock()) {
                Output("Could not find any ship controller.");
                return 0f;
            }
            else {
                switch (input) {
                    case "B": return    SHIP_CONTROLLER.CalculateShipMass().BaseMass;
                    case "T":
                    default: 
                        float   basis   = SHIP_CONTROLLER.CalculateShipMass().BaseMass, 
                                excess  = SHIP_CONTROLLER.CalculateShipMass().TotalMass;

                        excess  -= basis; excess /= CARGO_MULTIPLIER;
                        basis   += excess;

                        return basis;
                }
            }
        }

        public void FindThrusters() { FindThrusters(true); }

        public void FindThrusters(bool output) {
            List<IMyThrust> temp;
            ClearShipThrusters();
            List<IMyThrust> list = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(list);
            foreach (IMyThrust t in list) {
                if (!IsOnThisGrid(t)) continue;
                int dirint = TranslateDirection(t);
                t.CustomName = DirintToName(dirint);
                if (THRUSTERS.TryGetValue(dirint, out temp)) {
                    temp.Add(t);
                    THRUSTERS.Remove(dirint);
                    THRUSTERS.Add(dirint, temp);
                }
                else 
                    THRUSTERS.Add(dirint, new List<IMyThrust> { t });
            }
            bool ok = true;
            if (output) {
                for (int i = 1; i < 7; i++) if (!THRUSTERS.TryGetValue(i, out temp)) { ok = false; Output("WARNING: NO " + DirintToName(i) + " THRUSTERS."); }
                if (ok) Output("All thrusters found.");
            }
        }

        public List<IMyGyro> GetGyros(bool wantOutput) {
            List<IMyGyro> list = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType((temp = new List<IMyGyro>()));
            foreach (IMyGyro gyro in temp) if (IsOnThisGrid(gyro)) list.Add(gyro);
            if(wantOutput) Output("Found " + list.Count() + " gyros.\nEach gyro has to move about "+GetMass()/list.Count()+"kg of weight.");
            return list;
            //// 343 000 kg W CHUJ ZA DUŻO
            ///  200 000 kg no jeszcze jakoś ujdzie
            ///   85 000 kg względnie okej
            //foreach (IMyGyro g in list) {Output(g.CustomName + " positioned as " + TranslateDirection(g));}
            //return output;
        }

        public List<IMyGyro> GetGyros() {
            return GetGyros(false);
        }

        public void ChangeState(PROGRAM_STATE state) {
            Output("Changing mode from " + CurrentState + " to " + state.ToString() + ".");
            switch (state) {
                case PROGRAM_STATE.INIT:
                    Runtime.UpdateFrequency = UpdateFrequency.Once;
                    break;

                case PROGRAM_STATE.LND_INFO:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    OverrideThrusters(false);
                    OverrideGyros(false);
                    break;

                case PROGRAM_STATE.LND_ALGN:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case PROGRAM_STATE.LND_AUTO:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    if (SHIP_CONTROLLER != null) { SHIP_CONTROLLER.DampenersOverride = false; }
                    break;

                case PROGRAM_STATE.LND_DAMP:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case PROGRAM_STATE.STDBY:
                    STDBYS(3);
                    break;

                default:
                    Output("Function 'ChangeState': Undefined input value.");
                    return;
            }
            CurrentState = state;
        }

        public void ChangeState(string state) {
            Output("Changing mode from " + CurrentState + " to " + state.ToString() + ".");
            switch (state.ToUpper()) {
                case "INIT":        ChangeState(PROGRAM_STATE.INIT);        break;
                case "LND_INFO":    ChangeState(PROGRAM_STATE.LND_INFO);    break;
                case "LND_ALGN":    ChangeState(PROGRAM_STATE.LND_ALGN);    break;
                case "LND_AUTO":    ChangeState(PROGRAM_STATE.LND_AUTO);    break;

                default: Output("Function 'ChangeState': Undefined input value."); break;
            }
        }

        public void CLS() {
            if (CONTROL_SCREEN is IMyCockpit && allowOnCockpit) {
                IMyCockpit cock = (IMyCockpit)CONTROL_SCREEN;
                IMyTextSurface pan = cock.GetSurface(0);
                if (pan != null) {
                    pan.ContentType = ContentType.TEXT_AND_IMAGE;
                    pan.WriteText("", false);
                }
                else {
                    if (GetScreen()) {
                        CONTROL_SCREEN.WriteText("", false);
                    }
                }
            }
            else if (GetScreen()) {
                CONTROL_SCREEN.WriteText("", false);
            }
        }

        public void CustomizePanel() {
            CONTROL_SCREEN.FontSize = 0.8f;
            CONTROL_SCREEN.Font = "Monospace";
        }

        public void Output(Object input) {
            string message = (input is string) ? (string)input : input.ToString();
            message += "\n";
            if (SHIP_CONTROLLER is IMyCockpit && allowOnCockpit) {
                IMyCockpit cock = (IMyCockpit)SHIP_CONTROLLER;
                IMyTextSurface pan = cock.GetSurface(0);
                if (pan != null) {
                    pan.ContentType = ContentType.TEXT_AND_IMAGE;
                    pan.WriteText(message, true);
                }
                else {
                    if (GetScreen()) {
                        CustomizePanel();
                        CONTROL_SCREEN.WriteText(message, true);
                    }
                }
            }
            else if (GetScreen()) {
                CustomizePanel();
                CONTROL_SCREEN.WriteText(message, true);
            }
        }

        public Vector3D CutVector(Vector3D vector) { return CutVector(vector, 3); }

        public Vector3D CutVector(Vector3D vector, int decNo) {
            double 
                X = Math.Round(vector.X, decNo),
                Y = Math.Round(vector.Y, decNo),
                Z = Math.Round(vector.Z, decNo);

            return new Vector3D(X, Y, Z);
        }

        public void ReactToState() {
            Vector3D planet, ship, sub;
            string putout = "";
            switch (CurrentState) {
                case PROGRAM_STATE.INIT:
                    InitShip();
                    STDBYS(4, PROGRAM_STATE.LND_INFO);
                    break;

                case PROGRAM_STATE.LND_INFO:
                    if (GetControllingBlock()) {
                        FindThrusters(false);

                        CosmicBody body;
                        if (SHIP_CONTROLLER != null) {
                            putout = "Program chose this ship controler: " + SHIP_CONTROLLER.CustomName + "\n";
                            //SHIP_CONTROLLER.ShowOnHUD = true;
                            if (SHIP_CONTROLLER.TryGetPlanetPosition(out planet)) {
                                planet.X += planet.X >= 0 ? -0.5d : 0.5d;
                                planet.Y += planet.Y >= 0 ? -0.5d : 0.5d;
                                planet.Z += planet.Z >= 0 ? -0.5d : 0.5d;
                            }
                            ship = SHIP_CONTROLLER.GetPosition();

                            if (planet != null) {
                                double elev; SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out elev);
                                if (CosmicBodyDatabase.TryGet(planet, out body)) {
                                    putout += String.Format(
                                        "Currently in {0}'s sphere of influence.\n" +
                                        "{0}'s gravity: {1} G.\n{2}{3}",
                                        body.name, body.gravity, body.hasAtmo? "Atmosphere present.":"There's no atmosphere.",
                                        elev!=0? String.Format("\nRelative elevation: {0:0.00} m"):""
                                    );
                                    CurrentTarget = body;
                                }
                                else{
                                    putout = "In Unknown Planet's SOI\n";
                                    if (elev != 0d) putout += Math.Round(elev, 2) + " meters from the ground.\n";
                                    CurrentTarget = null;
                                }
                            }
                            else {
                                body = CosmicBodyDatabase.GetClosestBody(ship);
                                double distance = Vector3D.Distance(ship, body.coords);
                                putout += String.Format(
                                    "Closest Astral Body: {0}.\n" +
                                    "{0}'s gravity: {1} G.\n{2}",
                                    body.name, body.gravity, body.hasAtmo? "Atmosphere present.":"There's no atmosphere."
                                );
                            }
                        }

                        float   shipMassN = KgtoN(GetMass()),
                                shipMassub = KgtoN(GetMass("B")),
                                currMax = -1f;
                        int     currMaxI = 0, 
                                nonT = 0;

                        for (int i = 1; i < 7; i++) {
                            List<IMyThrust> temp = new List<IMyThrust>();
                            if (THRUSTERS.TryGetValue(i, out temp)) {
                                if (GetThrust(temp) > currMax) {
                                    currMaxI = i;
                                    currMax = GetThrust(temp);
                                }
                                putout
                                    += DirintToName(i) + " supportable gravity: " + (GetThrust(temp) / (shipMassN)).ToString("f3") 
                                    + "G  (Theoretical " + (GetThrust(temp) / (shipMassub)).ToString("f3") + " G)\n";
                            }
                            else nonT++;
                        }
                        if (nonT == 6) {
                            CurrentState = PROGRAM_STATE.INIT;
                            return;
                        }

                        putout += String.Format(
                            "\nThrust to weight ratio: {0} ({1})\n\n", 
                            (TWR = currMax/shipMassN).ToString("f3"), DirintToName(currMaxI).Split(' ')[0]
                        );

                        landingDir = currMaxI;

                        Output(putout);
                    }
                    break;

                case PROGRAM_STATE.LND_ALGN:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    ship = SHIP_CONTROLLER.GetPosition();
                    SHIP_CONTROLLER.TryGetPlanetPosition(out planet);
                    sub = planet == null ? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship)));
                    Vector3D curr = Vector3D.Subtract(CutVector(DirintToLndVec(landingDir)), sub);
                    List<NavPrompt> prompts = new List<NavPrompt>();

                    for (int i = 1; i < 7; i++) prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                    prompts = prompts.OrderBy(o => o.vLength).ToList();

                    putout = "Navigating ship so that it's "+DirintToName(landingDir) +" points out of the planet.";
                    putout += "\nNavigational prompts:";

                    for (int i = 0; i < 6; i++) putout += "\n" + DirintToName(prompts[i].dirInt) + " legnth : " + prompts[i].vLength;


                    if (curr.Length() <= maxDeviation) {
                        MoveAllGyros(0, 0, 0);
                        OverrideGyros(false);
                        CurrentState = PROGRAM_STATE.LND_INFO;
                        return;
                    }
                    else OverrideGyros(true);

                    int culprit,
                        antidir = landingDir%2==0? landingDir-1:landingDir+1;
                    for(culprit = 0; culprit < 3; culprit++) {
                        if (prompts[culprit].dirInt != landingDir && prompts[culprit].dirInt != antidir) break;
                    }
                    culprit = prompts[culprit].dirInt;


                    Output(putout + "\n\nCurrent Deviation: " + curr.Length()+ "\n Culprit: "+ DirintToName(culprit));

                    Vector3D command = DirToCmd(landingDir, culprit);
                    MoveAllGyros((float) (command.X * curr.Length()), (float) (command.Y * curr.Length()), (float) (command.Z * curr.Length()));

                    break;

                case PROGRAM_STATE.LND_AUTO:
                    if (SHIP_CONTROLLER != null && SHIP_CONTROLLER.TryGetPlanetPosition(out planet)) {
                        double elev = 100000;
                        SHIP_CONTROLLER.TryGetPlanetElevation(MyPlanetElevation.Surface, out elev);
                        planet.X = planet.X >= 0 ? planet.X - 0.5d : planet.X + 0.5d;
                        planet.Y = planet.Y >= 0 ? planet.Y - 0.5d : planet.Y + 0.5d;
                        planet.Z = planet.Z >= 0 ? planet.Z - 0.5d : planet.Z + 0.5d;

                        if (CurrentTarget==null || !planet.Equals(CurrentTarget.coords)) {
                                CosmicBody body;
                                if (CosmicBodyDatabase.TryGet(planet, out body)) {
                                    putout += "Currently in " + body.name + "'s SOI\n";
                                    putout += body.name + "'s gravity: " + body.gravity + "G. " + (body.hasAtmo ? "It has atmosphere." : "It has no atmosphere.");
                                    if (elev != 0d) putout += "\n" + Math.Round(elev, 2) + " meters from the ground.\n";
                                    CurrentTarget = body;
                                }
                            if (CurrentTarget == null || TWR <= CurrentTarget.gravity) {
                                ChangeState(PROGRAM_STATE.LND_INFO);
                                return;
                            }
                        }

                        double currGrav = (SHIP_CONTROLLER.GetNaturalGravity().Length()/GRAV_ACC_CONST);

                        // CALCULATIONS TIIIME! //
                        //float cautiousPortion = 1f;
                        
                        double 
                               cautiousAccel = ((TWR - CurrentTarget.gravity) * GRAV_ACC_CONST),
                               boldAccel     = ((TWR - currGrav) * GRAV_ACC_CONST),
                               /*/
                               thrustAccel   = (((TWR*TWR*TWR*boldAccel) + (cautiousPortion*cautiousAccel)) / ((TWR*TWR*TWR) + cautiousPortion));
                               /**/
                               /**/
                               thrustAccel = boldAccel;
                               /**/
                               /*/
                               thrustAccel = cautiousAccel;
                               /**/

                        double currSPD     = SHIP_CONTROLLER.GetShipSpeed();            // in m/s

                        if (thrustAccel == 0) thrustAccel = 0.01;
                        
                        double PONR_Elev    = ((2 * (currSPD * currSPD)) / (3 * thrustAccel)),
                               PONR_CAU     = ((2 * (currSPD * currSPD)) / (3 * cautiousAccel)),
                               PONR_BLD     = ((2 * (currSPD * currSPD)) / (3 * boldAccel));

                        double addition = (TWR - CurrentTarget.gravity);

                        addition *= addition; addition *= 10;

                        PONR_Elev   += addition;
                        PONR_CAU    += addition;
                        PONR_BLD    += addition;

                        Output("Cautious: " + (PONR_CAU-PONR_Elev));
                        Output("Elevation at which you should start worrying: "+ PONR_Elev);
                        Output("Bold: " + (PONR_BLD-PONR_Elev));
                        if (currSPD>0) Output("(Very Simple) Elapsed time to that point: "+ ((elev-PONR_Elev)/currSPD));

                        if (PONR_Elev >= elev) {
                            OverrideThrusters(false);
                            //ChangeState(PROGRAM_STATE.LND_DAMP);
                            List<IMyThrust> temp = new List<IMyThrust>();
                            if (THRUSTERS.TryGetValue(landingDir, out temp)) MoveAGroupThrusters(temp, 1f);
                            return;
                        }
                        else {
                            if (currSPD < 99) {
                                if (currGrav < 0.10) {
                                    int antiLND = landingDir % 2 == 0 ? landingDir - 1 : landingDir + 1;
                                    List<IMyThrust> temp = new List<IMyThrust>();
                                    if (THRUSTERS.TryGetValue(antiLND, out temp)) MoveAGroupThrusters(temp, 1f);
                                    return;
                                }
                                else {
                                    if (currSPD < 5) {
                                        OverrideThrusters(false);
                                        SHIP_CONTROLLER.DampenersOverride = true;
                                        ChangeState(PROGRAM_STATE.LND_INFO);
                                        return;
                                    }
                                }
                            }
                            OverrideThrusters(false);
                        }
                    }
                    else {
                        ChangeState(PROGRAM_STATE.LND_INFO);
                        return;
                    }
                    break;


                case PROGRAM_STATE.STDBY:
                    stdby_decr--;
                    if (stdby_decr <= 0) {
                        stdby_decr = 0;
                        ChangeState(NextState);
                    }
                    break;
            }
        }

        bool autoThrustOn = false;
        public void SwitchAutoThrust() {
            List<IMyThrust> 
                FWD, 
                BWD;

            autoThrustOn = !autoThrustOn;

            if (autoThrustOn) {
                if (THRUSTERS.TryGetValue(1, out FWD)) MoveAGroupThrusters(FWD, 1f);
                if (THRUSTERS.TryGetValue(2, out BWD)) {
                    foreach (IMyThrust thrust in BWD) thrust.Enabled = false;
                }
            }
            else {
                if (THRUSTERS.TryGetValue(1, out FWD)) MoveAGroupThrusters(FWD, 0f);
                if (THRUSTERS.TryGetValue(2, out BWD)) {
                    foreach (IMyThrust thrust in BWD) thrust.Enabled = true;
                }
            }
        }

        /// MAIN FUNCTIONS

        public void Main(string argument, UpdateType updateSource) {
            //if (CurrentState == null) CurrentState = PROGRAM_STATE.INIT;
            if (SHIP_CONTROLLER == null || !SHIP_CONTROLLER.IsWorking)
                if (!GetControllingBlock())
                    ChangeState(PROGRAM_STATE.LND_INFO);

            String[] eval = argument.ToUpper().Split(' ');

            Echo(autoThrustOn? "Auto ON": "Auto OFF");

            switch (argument.ToUpper()) {
                case "":
                    if (CurrentState != PROGRAM_STATE.STDBY) {
                        CLS();
                        Output("\n\nCurrent state: " + CurrentState + "\n");
                    }
                    ReactToState();
                    break;

                //PROGRAM_STATE
                default:
                    if (eval[0].ToUpper().Equals("GYRO")) {
                        if (eval.Length > 3) {
                            MoveAllGyros(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3]));
                        }
                        else {
                            MoveAllGyros(0,0,0);
                        }
                    }
                    else
                    if (eval[0].ToUpper().Equals("ABORT")) {
                        ChangeState(PROGRAM_STATE.LND_INFO);
                    }
                    else
                    if (eval[0].ToUpper().Equals("SWITCH")) {
                        SwitchAutoThrust();
                    }
                    else
                    ChangeState(argument.ToUpper());
                    break;
            }
        }
    }
}
