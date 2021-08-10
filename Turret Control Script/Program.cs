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

        readonly string TURRET_BASE = "AEG-";
        string MY_PREFIX   = "UNS-AEG";

        bool    hasTurret   = false;
        int     turIndx     = 0;
        static Turret  turret;

        const string AEGIS_CONTROLLER_SCRIPT_NAME = "AEGIS";
        IMyProgrammableBlock AEGIS_Controller;

        static Program MyInstance;


        public class Turret {
            public enum State {
                STOW,
                IDLE,
                RELOAD,
                TRACK,
                MANUAL
            }

            public IMyCameraBlock
                Camera;

            public IMyRemoteControl
                CTRL;

            IMyMotorStator
                XROT,
                YROT,
                YROTA;

            public IMyShipConnector
                RLDCon,
                BSDCon;

            readonly List<IMySmallGatlingGun>
                weaponry;

            Vector3D
                targetCoords;

            public double
                maxAngle;

            public bool
                hasTarget;

            public bool
                isFiring;

            State
                currState;

            public Turret(IMyRemoteControl CTRL = null, IMyMotorStator XROT = null, IMyMotorStator YROT = null, IMyMotorStator YROTA = null, IMyShipConnector RLDCon = null, IMyShipConnector BSDCon = null, List<IMySmallGatlingGun> weaponry = null) {
                this.CTRL = CTRL;
                this.XROT = XROT;
                this.YROT = YROT;
                this.YROTA = YROTA;
                this.RLDCon = RLDCon;
                this.BSDCon = BSDCon;
                ChangeState(State.STOW);
                SetAngle(BSDCon);
                this.targetCoords = new Vector3D(0, 0, 0);
                this.hasTarget = false;
                if (weaponry != null)
                    this.weaponry = weaponry;
                else
                    this.weaponry = new List<IMySmallGatlingGun>();
            }

            public void PerformSelfDiagnostic() {
                IMyGridTerminalSystem GridTerminalSystem = MyInstance.GridTerminalSystem;

                if (CTRL    != null && GridTerminalSystem.GetBlockWithId(this.CTRL.EntityId)   == null) { CTRL  = null; }
                if (XROT    != null && GridTerminalSystem.GetBlockWithId(this.XROT.EntityId)   == null) { XROT  = null; }
                if (YROT    != null && GridTerminalSystem.GetBlockWithId(this.YROT.EntityId)   == null) { YROT  = null; }
                if (YROTA   != null && GridTerminalSystem.GetBlockWithId(this.YROTA.EntityId)  == null) { YROTA = null; }
                if (RLDCon  != null && GridTerminalSystem.GetBlockWithId(this.RLDCon.EntityId) == null) { RLDCon= null; }
                if (BSDCon  != null && GridTerminalSystem.GetBlockWithId(this.BSDCon.EntityId) == null) { BSDCon= null; }
                if (Camera  != null && GridTerminalSystem.GetBlockWithId(this.Camera.EntityId) == null) { Camera= null; }
            }

            public void Lockdown() {
                if (XROT    != null) XROT   .TargetVelocityRPM = 0;
                if (YROT    != null) YROT   .TargetVelocityRPM = 0;
                if (YROTA   != null) YROTA  .TargetVelocityRPM = 0;
            }

            public void SetAngle(IMyShipConnector control) {
                if (control == null) {
                    maxAngle = 85;
                    return;
                }
                else {
                    double angle;
                    if (double.TryParse(control.CustomData, out angle))
                        maxAngle = 90 - angle;
                    else maxAngle = 85;
                }
            }

            public string WeaponryToSplitString() {
                string output = "";
                if (weaponry != null) {
                    foreach(IMySmallGatlingGun gun in weaponry) {
                        output += gun.GetId() + ",";
                    }
                    if(output.Length>2) output = output.Substring(0, output.Length - 2);
                }
                else
                    output = "N/A";

                return output;
            }

            public string ToSplitString() {
                string output = "";

                output += (Camera   != null ? Camera.GetId().ToString(): "N/A") + ";";

                output += (CTRL     != null ? CTRL.GetId()  .ToString(): "N/A") + ";";

                output += (XROT     != null ? XROT.GetId()  .ToString(): "N/A") + ";";
                output += (YROT     != null ? YROT.GetId()  .ToString(): "N/A") + ";";
                output += (YROTA    != null ? YROTA.GetId() .ToString(): "N/A") + ";";

                output += (RLDCon   != null ? RLDCon.GetId().ToString(): "N/A") + ";";
                output += (BSDCon   != null ? BSDCon.GetId().ToString(): "N/A") + ";";

                output += WeaponryToSplitString() + ";";

                return output;
            }

            public void AddWeapon(IMySmallGatlingGun gun)           { weaponry.Add(gun); }
            public void AddWeapon(List<IMySmallGatlingGun> guns)    { weaponry.AddList(guns); }

            public int GetWeaponrySize() { return this.weaponry.Count; }
            public bool HasCTRL()   { return this.CTRL  != null; }
            public bool HasXROT()   { return this.XROT  != null; }
            public bool HasYROT()   { return this.YROT  != null; }
            public bool HasYROTA()  { return this.YROTA != null; }
            public bool HasRLDCon() { return this.RLDCon!= null; }
            public bool HasBSCon()  { return this.BSDCon!= null; }
            public bool HasCamera() { return this.Camera!= null; }

            public void SetCTRL     (IMyRemoteControl   CTRL    ) { this.CTRL = CTRL; }
            public void SetXROT     (IMyMotorStator     XROT    ) { this.XROT = XROT; }
            public void SetYROT     (IMyMotorStator     YROT    ) { this.YROT = YROT; }
            public void SetYROTA    (IMyMotorStator     YROTA   ) { this.YROTA = YROTA; }
            public void SetRLCon    (IMyShipConnector   RLDCon  ) { this.RLDCon = RLDCon; }
            public void SetBSCon    (IMyShipConnector   BSDCon  ) { this.BSDCon = BSDCon; SetAngle(BSDCon); }
            public void SetCamera   (IMyCameraBlock     Camera  ) { this.Camera = Camera; Camera.EnableRaycast = true; }

            public void ChangeState(State state) {
                this.currState = state;
                switch (state) {
                    case State.IDLE:
                    case State.RELOAD:
                    case State.STOW:
                        if (this.weaponry != null && this.weaponry.Count > 0) this.Fire(false);
                        break;

                    case State.TRACK:
                    case State.MANUAL:

                        break;
                }
            }

            public void Fire(bool doThat) {
                isFiring = doThat;
                foreach (IMySmallGatlingGun gun in weaponry) {
                    if (doThat)
                        gun.ApplyAction("Shoot_On");
                    else
                        gun.ApplyAction("Shoot_Off");
                }
            }

            public bool CanTarget(Vector3D target) {
                Vector3D
                    up = BSDCon.WorldMatrix.Forward,
                    down = BSDCon.WorldMatrix.Backward;

                target = Vector3D.Normalize(Vector3D.Subtract(target, Vector3D.Add(BSDCon.GetPosition(), Vector3D.Multiply(down, 2))));
                
                if (InterCosine(up, target) >= Math.Cos(maxAngle * (Math.PI / 180.0)))
                    return true;
                return false;
            }

            public bool TrySetTarget(Vector3D target) {
                if (CanTarget(target)) {
                    SetTarget(target);
                    return true;
                }
                else {
                    ClearTarget();
                    return false;
                }
            }

            public void SetTarget(double X, double Y, double Z) { SetTarget(new Vector3D(X, Y, Z)); }

            public void SetTarget(Vector3D target) {
                this.targetCoords = target;
                this.hasTarget = true;

                ChangeState(State.TRACK);
            }

            public void ClearTarget() {
                this.hasTarget = false;
                this.ChangeState(Turret.State.STOW);
            }

            class NavPrompt {
                public int dirInt;
                public double vLength;

                public NavPrompt(int dir, Vector3D input) {
                    this.dirInt = dir;
                    this.vLength = input.Length();
                }
            }

            Vector3D CutVector(Vector3D vector) { return CutVector(vector, 3); }

            Vector3D CutVector(Vector3D vector, int decNo) {
                double
                    X = Math.Round(vector.X, decNo),
                    Y = Math.Round(vector.Y, decNo),
                    Z = Math.Round(vector.Z, decNo);

                return new Vector3D(X, Y, Z);
            }

            Vector3D DirintToVec(int dirint) {
                switch (dirint) {
                    case 1:
                        return CTRL.WorldMatrix.Forward;
                    case 2:
                        return CTRL.WorldMatrix.Backward;
                    case 3:
                        return CTRL.WorldMatrix.Left;
                    case 4:
                        return CTRL.WorldMatrix.Right;
                    case 5:
                        return CTRL.WorldMatrix.Up;
                    case 6:
                        return CTRL.WorldMatrix.Down;
                }
                return new Vector3D();
            }

            public double Difference(double A, double B) {
                double
                    max = A > B ? A : B,
                    min = A > B ? B : A;

                return max - min;
            }

            public bool AreQuiteSame(double A, double B) {
                return Difference(A, B) < 0.01d;
            }

            public void Move(Vector2 input) {
                Move(input.X, input.Y);
            }

            public void Move(float X, float Y) {
                if (XROT    != null)  XROT.RotorLock    = false;
                if (YROT    != null)  YROT.RotorLock    = false;
                if (YROTA   != null)  YROTA.RotorLock   = false;

                if (XROT    != null)  XROT.TargetVelocityRPM    = 5 * X;
                if (YROT    != null)  YROT.TargetVelocityRPM    = 5 * Y;
                if (YROTA   != null)  YROTA.TargetVelocityRPM   =-5 * Y;
            }

            public bool Evaluate(out string message) {
                message = "";
                bool negativeEvaluation = false, YR = true, YA = true;
                if (!HasCamera())   { message += "~CM"; }                               else { if (!Camera.IsFunctional)    { message += "*CM"; } }

                if (!HasBSCon())    { message += "~BS"; }                               else { if (!BSDCon.IsFunctional)    { message += "*BS"; } }

                if (!HasRLDCon())   { message += "~RL"; }                               else { if (!RLDCon.IsFunctional)    { message += "*RL"; } }

                if (!HasCTRL())     { message += "~CT"; negativeEvaluation = true; }    else { if (!CTRL.IsFunctional)      { message += "*CT"; negativeEvaluation = true; } }

                if (!HasXROT())     { message += "~XR"; negativeEvaluation = true; }    else { if (!XROT.IsFunctional)      { message += "*XR"; negativeEvaluation = true; } }

                if (!HasYROT())     { message += "~YR"; YR = false; }                   else { if (!YROT.IsFunctional)      { message += "*YR"; YR = false; } }
                    
                if (!HasYROTA())    { message += "~YA"; YA = false; }                   else { if (!YROTA.IsFunctional)     { message += "*YA"; YA = false; } }

                if (!YA && !YR)     negativeEvaluation = true;

                message += (message.Length>0) ? " ":"";

                return negativeEvaluation;
            }

            public void DoubleCTM(int C1, double VL1, int C2, double VL2) { Move(Vector2.Add(CulpritToMove(C1, (float)Difference(VL1, 1.4142d)), CulpritToMove(C2, (float)Difference(VL2, 1.4142d)))); }

            public Vector2 CulpritToMove(int culprit, float deviation) {
                if (deviation < 0.002f) return new Vector2(0, 0);

                if (culprit <= 4) {
                    deviation = deviation > 0.05 ? 6 : deviation;
                    if (culprit % 2 == 0) {
                        return new Vector2(deviation * 5f, 0);
                    }
                    else {
                        return new Vector2(-deviation * 5f, 0);
                    }
                }
                else {
                    deviation *= deviation > 0.05 ? 2 : 1;
                    if (culprit % 2 == 0) {
                        return new Vector2(0, deviation * 5);
                    }
                    else {
                        return new Vector2(0, -deviation * 5);
                    }
                }
            }

            public static double InterCosine(Vector3D first, Vector3D second) {
                double
                    scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                    productOfLengths = first.Length() * second.Length();

                return scalarProduct / productOfLengths;
            }

            public string DoYourJob() {
                string output = "";
                if (Evaluate(out output)) {
                    DisownTheTurret();
                    return output;
                }

                if (CTRL.IsUnderControl) {
                    if (this.currState != State.MANUAL) ChangeState(State.MANUAL);
                    if (RLDCon.Enabled == true) RLDCon.Enabled = false;

                    XROT.UpperLimitDeg = float.MaxValue;
                    XROT.LowerLimitDeg = float.MinValue;

                    XROT.TargetVelocityRPM = 0;
                    YROT.TargetVelocityRPM = 0;
                    YROTA.TargetVelocityRPM = 0;

                    float X = CTRL == null ? 0f : CTRL.RotationIndicator.Y;
                    float Y = CTRL == null ? 0f : CTRL.RotationIndicator.X;
                    float Z = CTRL == null ? 0f : CTRL.MoveIndicator.Z;

                    if (Z < 0) Fire(true);
                    else Fire(false);


                    Move(X / 20, Y / 20);
                    return output + "Manual Control";
                }
                else {
                    if (currState.Equals(State.IDLE)) {
                        Move(0, 0);
                        return output + "Idle";
                    }
                    else
                    if (currState.Equals(State.RELOAD)) {
                        Move(0, 0);
                        return output + "Reloading...";
                    }
                    else
                    if (currState.Equals(State.MANUAL)) {
                        ChangeState(State.STOW);
                        return output + "...";
                    }
                    Vector3D
                        target = currState.Equals(State.TRACK) ? targetCoords : Vector3D.Add(BSDCon.GetPosition(), Vector3D.Multiply(BSDCon.WorldMatrix.Forward, 100)),
                        me = this.CTRL.GetPosition(),

                        sub = CutVector(Vector3D.Normalize(Vector3D.Subtract(target, me))),
                        curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);


                    List<NavPrompt>
                        prompts = new List<NavPrompt>(),
                        sorted = new List<NavPrompt>();

                    for (int i = 3; i < 7; i++)
                        prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                    sorted = prompts.OrderBy(o => o.vLength).ToList();

                    Vector2I culprit = new Vector2I(sorted[0].dirInt, sorted[1].dirInt);

                    if (currState.Equals(State.STOW)) {
                        if (this.RLDCon.Enabled == false) this.RLDCon.Enabled = true;
                        if ((this.RLDCon.Status == MyShipConnectorStatus.Connectable && curr.Length() < 0.02f) || this.RLDCon.Status == MyShipConnectorStatus.Connected) {
                            this.RLDCon.Connect();
                            if (!hasTarget) ChangeState(State.IDLE);
                            else ChangeState(State.RELOAD);
                        }
                        else {
                            DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);
                        }
                        return output + "Stowing";
                    }
                    else
                    if (currState.Equals(State.TRACK)) {
                        if (this.RLDCon.Enabled == true) this.RLDCon.Enabled = false;
                        if (!this.hasTarget) {
                            ChangeState(State.STOW);
                            return "Lost Target";
                        }

                        DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);

                        double
                            distance = Vector3D.Distance(target, me),
                            currlength = curr.Length();

                        if (currlength < 0.2f && distance <= 800d) {
                            Fire(true);
                            return output + ("Firing");
                        }
                        else {
                            Fire(false);
                            return output + ("Tracking...");
                        }
                        /**/
                    }
                    return "???";
                }
            }
        }

        public class Entry {
            public Vector3D
                position,
                velocity;

            private static Vector3D NOTHING = new Vector3D(44, 44, 44);
            private const double maxSpeed = 400d;

            public Entry(Vector3D position, Vector3D velocity) {
                this.position = position;
                this.velocity = velocity;
            }

            public Entry(double px, double py, double pz, double vx, double vy, double vz) : this(new Vector3D(px, py, pz), new Vector3D(vx, vy, vz)) { }

            public Vector3D EstimatePosition() {
                return ApplyTarSpd(position, velocity);
            }

            double InterCosine(Vector3D first, Vector3D second) {
                double
                    scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                    productOfLengths = first.Length() * second.Length();

                return scalarProduct / productOfLengths;
            }

            Vector3D GetProjectedPos(Vector3D enPos, Vector3D enSpeed, Vector3D myPos) {
                /// do not enter if enSpeed is a "0" vector, or if our speed is 0
                Vector3D
                    A = enPos,
                    B = myPos;

                double
                    t = enSpeed.Length() / maxSpeed,        //t -> b = a*t  
                    projPath,//b
                    dist = Vector3D.Distance(A, B),         //c
                    cos = InterCosine(enSpeed, Vector3D.Subtract(myPos, enPos)),

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
                enSpeed = Vector3D.Normalize(enSpeed);
                enSpeed = Vector3D.Multiply(enSpeed, projPath);

                return Vector3D.Add(enPos, enSpeed);
            }

            Vector3D ApplyTarSpd(Vector3D position, Vector3D speed) {
                double
                    mySpeed = turret.CTRL.GetShipVelocities().LinearVelocity.Length(),
                    enSpeed = speed.Length(),
                    multiplier;

                /**/
                position = Vector3D.Add(position, Vector3D.Multiply(speed, Math.Sqrt(enSpeed) / 60));
                /**/

                if (enSpeed > 0) {
                    Vector3D output = GetProjectedPos(position, speed, turret.CTRL.GetPosition());
                    if (!output.Equals(NOTHING)) {
                        return output;
                    }
                }

                multiplier = (mySpeed != 0 && enSpeed != 0) ? (enSpeed / mySpeed) : 0;

                Vector3D
                    addition = Vector3D.Multiply(speed, multiplier);

                return Vector3D.Add(position, addition);
            }
        }


        public void Save() {
            Storage = hasTurret.ToString() + ";" + turIndx.ToString();
        }

        bool SetAEGISController() {
            /// this should be okay if there are no ships with Radar Control Script docked to the main ship
            AEGIS_Controller = GridTerminalSystem.GetBlockWithName(GetFullScriptName(AEGIS_CONTROLLER_SCRIPT_NAME)) as IMyProgrammableBlock;

            /// if the programmable block we picked is not from this ship, we commence the search to find it anyway
            if (AEGIS_Controller != null && !IsOnThisGrid(AEGIS_Controller)) {
                List<IMyProgrammableBlock> temp = new List<IMyProgrammableBlock>();
                AEGIS_Controller = null;
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyProgrammableBlock prog in temp) {
                    if (IsOnThisGrid(prog) && prog.CustomName.Equals(GetFullScriptName(AEGIS_CONTROLLER_SCRIPT_NAME))) {
                        AEGIS_Controller = prog; return true;
                    }
                }
            }
            /// and if we fail... welp, we can just inform the rest of the script that we can't do nothing
            return AEGIS_Controller != null;
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            MyInstance = this;
            SetAEGISController();
            String[] data = Storage.Split(';');
            try { hasTurret = bool.Parse(data[0]); }    catch (Exception e) { e.ToString(); hasTurret = false; }
            try { turIndx = int.Parse(data[1]); }       catch (Exception e) { e.ToString(); turIndx = 0; }
            SetTurIndx(turIndx);
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

        void SetTurIndx(int index) {
            if (index < 1) {
                turret      = new Turret();
                hasTurret   = false;
                turIndx     = 0;
                Runtime.UpdateFrequency = UpdateFrequency.None;
                MY_PREFIX = "UNS-AEG";
            }
            else {
                turIndx = index;
                if (GetTheTurret()) {
                    hasTurret = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    MY_PREFIX = TURRET_BASE + index;
                }
                else {
                    turret = new Turret();
                    hasTurret = false;
                    turIndx = 0;
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    MY_PREFIX = "UNS-AEG";
                }
            }
            SayMyName(MY_PREFIX);
        }

        bool IsOnThisGrid(IMyCubeBlock one) {
            return AreOnSameGrid(one, Me);
        }

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        static void DisownTheTurret() {
            turret.Lockdown();
            MyInstance.hasTurret = false;
            MyInstance.turIndx = 0;
            turret = null;
            MyInstance.MY_PREFIX = "UNS-AEG";
            MyInstance.Runtime.UpdateFrequency = UpdateFrequency.None;
            MyInstance.SayMyName(MyInstance.MY_PREFIX);
        }

        void MakeInvisible(IMyTerminalBlock block) {
            block.ShowInToolbarConfig = false;
            block.ShowInTerminal = false;
            block.ShowInInventory = false;
            block.ShowOnHUD = false;
        }

        bool GetTheTurret() {
            List<IMyRemoteControl>  remCons = new List<IMyRemoteControl>();
            List<IMyMotorStator>    rots    = new List<IMyMotorStator>();
            List<IMySmallGatlingGun>gatGuns = new List<IMySmallGatlingGun>(), ctrlGuns;
            List<IMyShipConnector>  conns   = new List<IMyShipConnector>();
            List<IMyConveyorSorter> sorters = new List<IMyConveyorSorter>();
            List<IMyCameraBlock>    cameras = new List<IMyCameraBlock>();

            GridTerminalSystem.GetBlocksOfType(remCons);
            GridTerminalSystem.GetBlocksOfType(rots);
            GridTerminalSystem.GetBlocksOfType(gatGuns);
            GridTerminalSystem.GetBlocksOfType(conns);
            GridTerminalSystem.GetBlocksOfType(sorters);
            GridTerminalSystem.GetBlocksOfType(cameras);

            string status = "";
            int turNo;

            turret = null;

            foreach (IMyRemoteControl ctrl in remCons) {
                if (ctrl.CustomData.StartsWith(TURRET_BASE)) {
                    try{
                        turNo = int.Parse(ctrl.CustomData.Substring(TURRET_BASE.Length));
                        if (turNo != turIndx) continue;
                        ctrl.CustomName = "[" + TURRET_BASE + turNo + "] Controller";
                        turret = new Turret(ctrl);
                        ctrlGuns = new List<IMySmallGatlingGun>();

                        foreach (IMySmallGatlingGun gun in gatGuns) { if (AreOnSameGrid(gun, ctrl)) { ctrlGuns.Add(gun); gun.CustomName = "[" + TURRET_BASE + turNo + "] Gun"; MakeInvisible(gun); } }
                        foreach (IMyShipConnector   con in conns)   { if (AreOnSameGrid(con, ctrl)) { turret.SetRLCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Reload Con"; conns.Remove(con); MakeInvisible(con); break; } }
                        foreach (IMyConveyorSorter  sor in sorters) { if (AreOnSameGrid(sor, ctrl)) { sor.CustomName = "[" + TURRET_BASE + turNo + "] Sorter"; MakeInvisible(sor); } }
                        foreach (IMyCameraBlock     cam in cameras) { if (AreOnSameGrid(cam, ctrl)) { turret.SetCamera(cam); cam.CustomName = "[" + TURRET_BASE + turNo + "] Camera"; MakeInvisible(cam); break; } }

                        if (!turret.HasRLDCon()) status += "\n102:The turret does not have a reload connector: " + turNo;

                        if (ctrlGuns.Count > 0) turret.AddWeapon(ctrlGuns);
                        else status += "\n105:There is a turret without weaponry on it: " + turNo;
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\n109:There was a parsing error: \"" + ctrl.CustomData.Substring(TURRET_BASE.Length) + "\"";
                        status += "\n" + e.StackTrace;
                    }
                }
            }
            
            if (turret == null) { 
                status += "\n115:The turret was not found.";
                Me.CustomData = status; return false; 
            }
            else
            foreach (IMyMotorStator rot in rots) {
                if (rot.CustomData.Length <= 0) continue;
                string[] data = rot.CustomData.ToUpper().Split(';');
                if (data.Length < 2) {
                    try {
                        turNo = int.Parse(rot.CustomData.Substring(TURRET_BASE.Length));
                        if (turNo != turIndx) continue;
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\n127:There was a parsing error: \"" + rot.CustomData.Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }
                    status += "\n130:There is no rotor definition for a rotor: " + turNo;
                }
                else {
                    try {
                        turNo = int.Parse(data[0].Substring(TURRET_BASE.Length));
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\n138:There was a parsing error: \"" + data[0].Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }
                    if (turNo != turIndx) continue;

                    if (data[1].ToUpper().Equals("XROT")) {
                        if (!turret.HasXROT()) {
                            turret.SetXROT(rot);
                            rot.CustomName = "[" + TURRET_BASE + turNo + "] XROT";
                        }
                        else status += "\n148:There is a double-XROT situation going on: " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROT")) {
                            if (!turret.HasYROT()) {
                                turret.SetYROT(rot);
                                rot.CustomName = "[" + TURRET_BASE + turNo + "] YROT";
                                if (!turret.HasBSCon()) foreach (IMyShipConnector con in conns) if (AreOnSameGrid(rot, con)) { turret.SetBSCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Base Con"; break; }
                            }
                            else status += "\n157:There is a double-YROT situation going on: " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROTA")) {
                        if (!turret.HasYROTA()) {
                            turret.SetYROTA(rot);
                            rot.CustomName = "[" + TURRET_BASE + turNo + "] YROTA";
                            if (!turret.HasBSCon()) foreach (IMyShipConnector con in conns) if (AreOnSameGrid(rot, con)) { turret.SetBSCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Base Con"; break; }
                        }
                        else status += "\n166:There is a double-YROTA situation going on: " + turNo;
                    }
                    else status += "\n167:There is an incomprehensible definition for a rotor: " + data[1];
                }
            }
            //Echo(status);
            if(status.Length>0) Me.CustomData = status;
            return true;
        }

        public void Output(object input) {
            string message = input is string ? (string)input : input.ToString();
            message = turIndx + ";" + message + "\n";

            if (AEGIS_Controller != null || SetAEGISController()) {
                AEGIS_Controller.CustomData += message;
            }
            else Echo("UnU");
        }


        int TimeNo = 0;
        public void Main(string argument, UpdateType updateSource) {
            /**/
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    int turId;
                    double px, py, pz/**/, sx, sy, sz/**/;
                    switch (args[0]) {
                        case "tar":
                            if (turret==null || !hasTurret) return;
                            if (args.Length > 3) {
                                if (Double.TryParse(args[1], out px) && Double.TryParse(args[2], out py) && Double.TryParse(args[3], out pz)) {
                                    Vector3D vec = new Vector3D(px, py, pz);
                                    turret.TrySetTarget(vec);
                                    turret.RLDCon.CustomData = turret.CanTarget(vec).ToString();
                                } 
                                else turret.ChangeState(Turret.State.STOW);
                            } 
                            else turret.ChangeState(Turret.State.STOW);
                            break;

                        case "assign":
                        case "set":
                            if (args.Length > 1) {
                                if (int.TryParse(args[1], out turId)) {
                                    Echo("Attempting to set Turret "+turId);
                                    SetTurIndx(turId);
                                }
                                else Echo("Failed to parse index.");
                            }
                            break;


                        case "reg": // "you have been provided a fresh data package"
                            /**/
                            if (turret == null || !hasTurret) return;

                            string[]
                                registry = Me.CustomData.Split('\n'),
                                row;

                            Vector3D target;
                            for (int i = 0; i < registry.Length; i++) {
                                row = registry[i].Split(';');
                                int ind = 0;
                                if (row.Length > 5 &&
                                    double.TryParse(row[ind++], out px) &&
                                    double.TryParse(row[ind++], out py) &&
                                    double.TryParse(row[ind++], out pz) &&

                                    double.TryParse(row[ind++], out sx) &&
                                    double.TryParse(row[ind++], out sy) &&
                                    double.TryParse(row[ind++], out sz)) 
                                {
                                    /// dividing by 10, because the fire control provides data multiplied by 10 to aleviate the influence of the number limits on the accuracy of the data 
                                    /// (that is, it ommits the dots in the data provided and multiplies by 10)
                                    target = new Entry(new Vector3D(px / 10, py / 10, pz / 10), new Vector3D(sx / 10, sy / 10, sz / 10)).EstimatePosition();
                                    if (turret.TrySetTarget(target)) return;
                                }
                            }
                            turret.ClearTarget();
                            /**/
                            break;

                        default:
                            Echo("Command unknown");
                            break;
                    }
                }
            }
            else {

                if (hasTurret && turret != null && TimeNo++ >= 600) {
                    TimeNo = 0;
                    turret.PerformSelfDiagnostic();
                }

                if (hasTurret && turret != null) {
                    string output = turret.DoYourJob();
                    Output(output);
                }
            }
            /**/
        }
    }
}
