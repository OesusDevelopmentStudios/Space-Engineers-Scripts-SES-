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

        string
                TURRET_BASE = "AEG-",
                MY_PREFIX   = "UNS-AEG";

        bool    hasTurret   = false;
        int     turIndx     = 0;
        static Turret  turret;

        List<IMyTextPanel> screens;

        public void Save() {
            Storage = hasTurret.ToString() + ";" + turIndx.ToString();
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
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
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
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
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
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

        public bool GetTheTurret() {
            List<IMyRemoteControl>  remCons = new List<IMyRemoteControl>();
            List<IMyMotorStator>    rots    = new List<IMyMotorStator>();
            List<IMySmallGatlingGun>gatGuns = new List<IMySmallGatlingGun>(), ctrlGuns;
            List<IMyShipConnector>  conns   = new List<IMyShipConnector>();

            GridTerminalSystem.GetBlocksOfType(remCons);
            GridTerminalSystem.GetBlocksOfType(rots);
            GridTerminalSystem.GetBlocksOfType(gatGuns);
            GridTerminalSystem.GetBlocksOfType(conns);

            string status = "Turret "+turIndx+":";
            int turNo;

            turret = null;

            foreach (IMyRemoteControl ctrl in remCons) {
                if (ctrl.CustomData.StartsWith(TURRET_BASE)) {
                    try{
                        turNo = int.Parse(ctrl.CustomData.Substring(TURRET_BASE.Length));
                        if (turNo != turIndx) continue;
                            turret = new Turret(ctrl);
                            ctrlGuns = new List<IMySmallGatlingGun>();

                            foreach (IMySmallGatlingGun gun in gatGuns) { if (AreOnSameGrid(gun, ctrl)) { ctrlGuns.Add(gun); gun.CustomName = "[" + TURRET_BASE + turNo + "] Gun"; } }

                            foreach (IMyShipConnector con in conns) { if (AreOnSameGrid(con, ctrl)) { turret.SetRLCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Reload Con"; conns.Remove(con); break; } }

                            if (!turret.HasRLCon()) status += "\n102:The turret does not have a reload connector: " + turNo;

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
            
            if (turret == null) { status += "\n115:The turret was not found."; return false; }
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
            Echo(status);
            Me.CustomData = status;
            return true;
        }

        public void Output(object input, bool append = true) {
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach (IMyTextPanel screen in temp) { if (AreOnSameGrid(Me, screen) && screen.CustomName.Contains("[TRRT]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) screen.WriteText(message, append);
        }

        public void Main(string argument, UpdateType updateSource) {
            /**/
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    long id;
                    int turId;
                    double px, py, pz/**/, sx, sy, sz/**/;
                    switch (args[0]) {
                        case "tar":
                            if (args.Length > 3 && hasTurret) {
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
                            if (args.Length > 1) {
                                if(int.TryParse(args[1], out turId)) {
                                    SetTurIndx(turId);
                                }
                            }
                            break;


                        case "reg": // "you have been provided a fresh data package"
                            /**/
                            string[]
                                registry = Me.CustomData.Split('\n'),
                                row;

                            Vector3D target;
                            for (int i = 0; i < registry.Length; i++) {
                                row = registry[i].Split(';');
                                int ind = 0;
                                if (row.Length > 6 &&
                                    long.TryParse(row[ind++], out id) &&

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
                                    if (turret.CanTarget(target)) {
                                        turret.SetTarget(target);
                                        return;
                                    }
                                }
                            }
                            /**/
                            break;
                    }
                }
            }
            else {
                // Update1-100
                // TODO: Make it so that the orders from the fire control expire after a period of time
                if (hasTurret) {
                    string output = " " + TURRET_BASE + turIndx + (turIndx < 10 ? " " : "") + ":" + turret.DoYourJob() + "\n\n";
                    Output(output);
                }
            }
            /**/
        }

        public class Turret {
            public enum State {
                STOW,
                MANUAL,
                IDLE,
                RELOAD,
                TRACK
            }

            public IMyRemoteControl
                CTRL;

            IMyMotorStator
                XROT,
                YROT,
                YROTA;

            public IMyShipConnector
                RLDCon,
                BSDCon;

            List<IMySmallGatlingGun>
                weaponry;

            Vector3D
                targetCoords;

            public double
                maxAngle;

            public bool
                hasTarget;

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

            public void SetAngle(IMyShipConnector control) {
                if (control == null) {
                    maxAngle = 85;
                    return;
                }
                else {
                    double angle;
                    if (double.TryParse(control.CustomData, out angle))
                        maxAngle = 85 - angle;
                    else maxAngle = 85;
                }
            }

            public void AddWeapon(IMySmallGatlingGun gun) { weaponry.Add(gun); }
            public void AddWeapon(List<IMySmallGatlingGun> guns) { weaponry.AddList(guns); }

            public bool HasCTRL() { return this.CTRL != null; }
            public bool HasXROT() { return this.XROT != null; }
            public bool HasYROT() { return this.YROT != null; }
            public bool HasYROTA() { return this.YROTA != null; }
            public bool HasRLCon() { return this.RLDCon != null; }
            public bool HasBSCon() { return this.BSDCon != null; }

            public void SetCTRL(IMyRemoteControl CTRL) { this.CTRL = CTRL; }
            public void SetXROT(IMyMotorStator XROT) { this.XROT = XROT; }
            public void SetYROT(IMyMotorStator YROT) { this.YROT = YROT; }
            public void SetYROTA(IMyMotorStator YROTA) { this.YROTA = YROTA; }
            public void SetRLCon(IMyShipConnector RLDCon) { this.RLDCon = RLDCon; }
            public void SetBSCon(IMyShipConnector BSDCon) { this.BSDCon = BSDCon; SetAngle(BSDCon); }

            public void ChangeState(State state) {
                if (this.weaponry != null && this.weaponry.Count > 0) this.Fire(false);
                this.currState = state;
                switch (state) {
                    case State.IDLE:

                        break;

                    case State.MANUAL:

                        break;

                    case State.RELOAD:

                        break;

                    case State.STOW:

                        break;

                    case State.TRACK:

                        break;
                }
            }

            public void Fire(bool doThat) {
                foreach (IMySmallGatlingGun gun in weaponry) {
                    if (doThat)
                        gun.ApplyAction("Shoot_On");
                    else
                        gun.ApplyAction("Shoot_Off");
                }
            }

            public bool CanTarget(Vector3D target) {
                Vector3D fwd = Vector3D.Add(this.CTRL.GetPosition(), Vector3D.Multiply(this.BSDCon.WorldMatrix.Forward, 1000d));
                if (InterCosine(fwd, target) >= Math.Cos(maxAngle * (Math.PI / 180.0)))
                    return true;
                return false;
            }

            public void TrySetTarget(Vector3D target) {
                if (CanTarget(target))
                    SetTarget(target);
                else
                    ClearTarget();
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
                if (XROT != null) XROT.RotorLock = false;
                if (YROT != null) YROT.RotorLock = false;
                if (YROTA != null) YROTA.RotorLock = false;

                if (XROT != null) XROT.TargetVelocityRPM = 5 * X;
                if (YROT != null) YROT.TargetVelocityRPM = 5 * Y;
                if (YROTA != null) YROTA.TargetVelocityRPM = -5 * Y;
            }

            public bool Evaluate(out string message) {
                message = " ";
                if (this.HasBSCon() && this.HasCTRL() && this.HasRLCon() && this.HasXROT() && !(!this.HasYROT() && !this.HasYROTA())) {
                    if (!this.HasYROT()) message += "~YR";
                    if (!this.HasYROTA()) message += "~YRA";
                    if (!this.HasYROT() || !this.HasYROTA()) message += " ";
                    return false;
                }
                else {
                    if (!this.HasBSCon()) message += "~BS";
                    if (!this.HasCTRL()) message += "~CT";
                    if (!this.HasRLCon()) message += "~RL";
                    if (!this.HasXROT()) message += "~XR";
                    if (!this.HasYROT()) message += "~YR";
                    if (!this.HasYROTA()) message += "~YA";
                    return true;
                }
            }

            public void DoubleCTM(int C1, double VL1, int C2, double VL2) { Move(Vector2.Add(CulpritToMove(C1, (float)Difference(VL1, 1.4142d)), CulpritToMove(C2, (float)Difference(VL2, 1.4142d)))); }

            public Vector2 CulpritToMove(int culprit, float deviation) {
                if (deviation < 0.005f) return new Vector2(0, 0);

                deviation = deviation > 1 ? deviation * deviation * deviation : (float)Math.Sqrt((double)deviation);
                /**/
                if (culprit <= 4) {
                    if (culprit % 2 == 0) {
                        return new Vector2(deviation, 0);
                    }
                    else {
                        return new Vector2(-deviation, 0);
                    }
                }
                else {
                    if (culprit % 2 == 0) {
                        return new Vector2(0, deviation);
                    }
                    else {
                        return new Vector2(0, -deviation);
                    }
                }
                /**/
            }

            public static double InterCosine(Vector3D first, Vector3D second) {
                double
                    scalarProduct = first.X * second.X + first.Y * second.Y + first.Z * second.Z,
                    productOfLengths = first.Length() * second.Length();

                return scalarProduct / productOfLengths;
            }

            public string DoYourJob() {
                string output;
                if (Evaluate(out output)) {
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
                            //Move(CulpritToMove(culprit,(float)curr.Length()));
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
                        //Move(CulpritToMove(culprit, (float)(curr.Length())));
                        DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);
                        /**/
                        if (curr.Length() < 0.1f && Vector3D.Subtract(target, me).Length() <= 800) {
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
                return applyTarSpd(position, velocity);
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

            Vector3D applyTarSpd(Vector3D position, Vector3D speed) {
                double
                    mySpeed = turret.CTRL.GetShipVelocities().LinearVelocity.Length(),
                    enSpeed = speed.Length(),
                    multiplier;

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
    }
}
