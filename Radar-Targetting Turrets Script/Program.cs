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
using VRageRender.Utils;
using System.Diagnostics;

namespace IngameScript {
    partial class Program : MyGridProgram {

        List<IMyTextPanel> screens;

        Dictionary<int, Turret> turrets;

        const string
            TURRET_BASE = "TRRT-",
            MY_PREFIX   = "RAD-TUR";

        string LOG = "";

        int TickNo = 0;

        bool InfoGot = true;

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        void SayMyName(string ScriptName, float textSize = 2f) {
            Me.CustomName = "[" + ScriptName + "] Script";
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        public class TarEntry {
            public long    tarID;
            public int     turID;

            public TarEntry(long tarID, int turID) {
                this.tarID = tarID;
                this.turID = turID;
            }

            public bool Equals(TarEntry entry) {
                if (this.tarID.Equals(entry.tarID) && this.turID.Equals(entry.turID)) return true;
                return false;
            }
        }

        public class TargetDistributor {
            private static List<TarEntry> data = new List<TarEntry>();

            public static void Add(TarEntry entry) {
                foreach(TarEntry ent in data) {
                    if (ent.Equals(entry)) {
                        return;
                    }
                }
                data.Add(entry);
            }

            public static void Distribute() {
                if (data.Count <= 0) return;
                data.Sort((a, b) => (a.tarID.CompareTo(b.tarID)));
                long tar = data[0].tarID;
                int  tur = data[0].turID;
                TarEntry tarentry = new TarEntry(tar, 0), turentry = new TarEntry(tur, 0);
                List <TarEntry> tarNums = new List<TarEntry>();
                List <TarEntry> turNums = new List<TarEntry>();
                for (int i=0; i<data.Count; i++) {
                    if (tar.Equals(data[i].tarID)) tarentry.turID++;
                    else {
                        tarNums.Add(tarentry);
                        tar = data[i].tarID;
                        tarentry = new TarEntry(tar, 1);
                    }
                }

                data.Sort((a, b) => (a.turID.CompareTo(b.turID)));
                for (int i = 0; i < data.Count; i++) {
                    if (tur.Equals(data[i].turID)) turentry.turID++;
                    else {
                        turNums.Add(turentry);
                        tur = data[i].turID;
                        turentry = new TarEntry(tur, 1);
                    }
                }
            }
        }

        public class RegEntry {
            public Vector3D 
                position,
                velocity;

            public RegEntry(Vector3D position, Vector3D velocity) {
                this.position   = position;
                this.velocity   = velocity;
            }

            public RegEntry(double px, double py, double pz, double vx, double vy, double vz) : this(new Vector3D(px,py,pz), new Vector3D(vx, vy, vz)) {

            }
        }


        public class Register {
            private static Dictionary<long, RegEntry> content = new Dictionary<long, RegEntry>();

            public static bool Has  (long id) { return content.ContainsKey(id); }

            public static bool Get  (long id, out RegEntry ent) { return content.TryGetValue(id, out ent); }

            public static void Set  (long id, RegEntry ent) { content.Add(id, ent); }

            public static int Count() { return content.Count(); }

            public static void Delet() { content.Clear(); }

            public static void Delet(long id) { content.Remove(id); }
        }

        public class Turret {
            public enum State{
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
                this.CTRL  = CTRL;
                this.XROT  = XROT;
                this.YROT  = YROT;
                this.YROTA = YROTA;
                this.RLDCon= RLDCon;
                this.BSDCon= BSDCon;
                ChangeState(State.STOW);
                SetAngle(BSDCon);
                this.targetCoords = new Vector3D(0,0,0);
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
                    else    maxAngle = 85;
                }
            }

            public void AddWeapon(IMySmallGatlingGun gun) { weaponry.Add(gun); }
            public void AddWeapon(List<IMySmallGatlingGun> guns) { weaponry.AddList(guns); }

            public bool HasCTRL ()      { return this.CTRL  != null; }
            public bool HasXROT ()      { return this.XROT  != null; }
            public bool HasYROT ()      { return this.YROT  != null; }
            public bool HasYROTA()      { return this.YROTA != null; }
            public bool HasRLCon()      { return this.RLDCon!= null; }
            public bool HasBSCon()      { return this.BSDCon!= null; }

            public void SetCTRL (IMyRemoteControl CTRL)     { this.CTRL = CTRL; }
            public void SetXROT (IMyMotorStator   XROT)     { this.XROT = XROT; }
            public void SetYROT (IMyMotorStator   YROT)     { this.YROT = YROT; }
            public void SetYROTA(IMyMotorStator   YROTA)    { this.YROTA = YROTA; }
            public void SetRLCon(IMyShipConnector RLDCon)   { this.RLDCon = RLDCon; }
            public void SetBSCon(IMyShipConnector BSDCon)   { this.BSDCon = BSDCon; SetAngle(BSDCon); }

            public void ChangeState(State state) {
                if (this.weaponry !=null && this.weaponry.Count>0) this.Fire(false);
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
                foreach(IMySmallGatlingGun gun in weaponry) {
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

            public void Move(float X, float Y){
                if (XROT != null)   XROT    .RotorLock = false;
                if (YROT != null)   YROT    .RotorLock = false;
                if (YROTA != null)  YROTA   .RotorLock = false;

                if (XROT != null)   XROT    .TargetVelocityRPM = 5*X;
                if (YROT != null)   YROT    .TargetVelocityRPM = 5*Y;
                if (YROTA!= null)   YROTA   .TargetVelocityRPM =-5*Y;
            }

            public bool Evaluate(out string message) {
                message = " ";
                if (this.HasBSCon() && this.HasCTRL() && this.HasRLCon() && this.HasXROT() && !(!this.HasYROT() && !this.HasYROTA())) {
                    if (!this.HasYROT())    message += "~YR";
                    if (!this.HasYROTA())   message += "~YRA";
                    if (!this.HasYROT() || !this.HasYROTA()) message += " ";
                    return false;
                }
                else {
                    if (!this.HasBSCon())   message += "~BS";
                    if (!this.HasCTRL() )   message += "~CT";
                    if (!this.HasRLCon())   message += "~RL";
                    if (!this.HasXROT() )   message += "~XR";
                    if (!this.HasYROT() )   message += "~YR";
                    if (!this.HasYROTA())   message += "~YA";
                    return true;
                }
            }

            public void DoubleCTM(int C1, double VL1, int C2, double VL2) {Move(Vector2.Add(CulpritToMove(C1, (float)Difference(VL1,1.4142d)), CulpritToMove(C2, (float)Difference(VL2, 1.4142d))));}

            public Vector2 CulpritToMove(int culprit, float deviation){
                if (deviation < 0.005f) return new Vector2(0, 0);

                deviation = deviation > 1 ? deviation*deviation*deviation : (float)Math.Sqrt((double)deviation);
                /**/
                if (culprit <= 4) {
                    if (culprit % 2 == 0) {
                        return  new Vector2(deviation,0);
                    }
                    else{
                        return new Vector2(-deviation,0);
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

            /// 24 (longest string in 'Status') 6 + 18
            /// 

            public string DoYourJob() {
                string output;
                if(Evaluate(out output)) {
                    return output;
                }
                if (CTRL.IsUnderControl) {
                    if (this.currState != State.MANUAL) ChangeState(State.MANUAL);
                    if (RLDCon.Enabled==true) RLDCon.Enabled = false;

                    XROT.UpperLimitDeg = float.MaxValue;
                    XROT.LowerLimitDeg = float.MinValue;

                    XROT.TargetVelocityRPM = 0;
                    YROT.TargetVelocityRPM = 0;
                    YROTA.TargetVelocityRPM = 0;

                    float X = CTRL == null ? 0f : CTRL.RotationIndicator.Y;
                    float Y = CTRL == null ? 0f : CTRL.RotationIndicator.X;
                    float Z = CTRL == null ? 0f : CTRL.MoveIndicator.Z;

                    if (Z < 0)  Fire(true);
                    else        Fire(false);


                    Move(X/25,Y/25);
                    return output + "Manual Control";
                }
                else{
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
                        target  = currState.Equals(State.TRACK)? targetCoords : Vector3D.Add(BSDCon.GetPosition(),Vector3D.Multiply(BSDCon.WorldMatrix.Forward,100)),
                        me      = this.CTRL.GetPosition(),
                        
                        sub     = CutVector(Vector3D.Normalize(Vector3D.Subtract(target, me))),
                        curr    = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);


                    List<NavPrompt> 
                        prompts = new List<NavPrompt>(), 
                        sorted = new List<NavPrompt>();
                    /**/
                    for (int i = 3; i < 7; i++)
                        prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                    sorted = prompts.OrderBy(o => o.vLength).ToList();

                    Vector2I culprit = new Vector2I(sorted[0].dirInt,sorted[1].dirInt);
                    /**/
                    //// Old Culprit System Below
                    /**
                    int culprit;

                    for (int i = 1; i < 7; i++)
                        prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
                    sorted = prompts.OrderBy(o => o.vLength).ToList();

                    for (culprit = 0; culprit < 3; culprit++) {
                        if (sorted[culprit].dirInt != 1 && sorted[culprit].dirInt != 2) {
                            if(culprit==1 && (sorted[culprit].dirInt==5 || sorted[culprit].dirInt == 6)) {
                                if (!AreQuiteSame(prompts[2].vLength, prompts[3].vLength)) {culprit=2;}
                            } 
                            break;
                        }
                    }
                    culprit = sorted[culprit].dirInt;
                    /**/

                    if (currState.Equals(State.STOW)){
                        if(this.RLDCon.Enabled == false) this.RLDCon.Enabled = true;
                        if((this.RLDCon.Status == MyShipConnectorStatus.Connectable && curr.Length()<0.02f) || this.RLDCon.Status == MyShipConnectorStatus.Connected) {
                            this.RLDCon.Connect();
                            if(!hasTarget)  ChangeState(State.IDLE);
                            else            ChangeState(State.RELOAD);
                        }
                        else{
                            //Move(CulpritToMove(culprit,(float)curr.Length()));
                            DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);
                        }
                        return output+"Stowing";
                    }
                    else
                    if(currState.Equals(State.TRACK)) {
                        if (this.RLDCon.Enabled == true) this.RLDCon.Enabled = false;
                        if (!this.hasTarget) {
                            ChangeState(State.STOW);
                            return "Lost Target";
                        }
                        //Move(CulpritToMove(culprit, (float)(curr.Length())));
                        DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);
                        /**/
                        if(curr.Length() < 0.1f && Vector3D.Subtract(target,me).Length()<=800) { 
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

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SayMyName(MY_PREFIX);

            GetTurrets();
        }

        public void Save() {

        }

        public void GetTurrets() {
            turrets = new Dictionary<int, Turret>();

            List<IMyRemoteControl>  remCons = new List<IMyRemoteControl>();
            List<IMyMotorStator>    rots    = new List<IMyMotorStator>();
            List<IMySmallGatlingGun>gatGuns = new List<IMySmallGatlingGun>(), ctrlGuns;
            List<IMyShipConnector>  conns   = new List<IMyShipConnector>();
            Turret turret;

            GridTerminalSystem.GetBlocksOfType(remCons);
            GridTerminalSystem.GetBlocksOfType(rots);
            GridTerminalSystem.GetBlocksOfType(gatGuns);
            GridTerminalSystem.GetBlocksOfType(conns);

            string status = "";
            int turNo;

            foreach (IMyRemoteControl ctrl in remCons) {
                if (ctrl.CustomData.StartsWith(TURRET_BASE)) {
                    try {
                        turNo = int.Parse(ctrl.CustomData.Substring(TURRET_BASE.Length));
                        if(!turrets.TryGetValue(turNo, out turret)) {
                            turret = new Turret(ctrl);
                            ctrlGuns = new List<IMySmallGatlingGun>();

                            foreach(IMySmallGatlingGun gun in gatGuns) {if (AreOnSameGrid(gun, ctrl)) {ctrlGuns.Add(gun); gun.CustomName = "["+TURRET_BASE+turNo+"] Gun";}}

                            foreach(IMyShipConnector con in conns) { if (AreOnSameGrid(con, ctrl)) { turret.SetRLCon(con); con.CustomName = "["+TURRET_BASE+turNo+"] Reload Con"; conns.Remove(con); break; } }

                            if (!turret.HasRLCon()) status += "\n417:The turret does not have a reload connector: " + turNo;

                            if (ctrlGuns.Count > 0) turret.AddWeapon(ctrlGuns);
                            else status += "\n420:There is a turret without weaponry on it: " + turNo;

                            turrets.Add(turNo, turret);
                        }
                        else status += "\n424:There is more than one turret with the same number: " + turNo;
                    }
                    catch(Exception e) {
                        e.ToString();
                        status += "\n428:There was a parsing error: \""+ ctrl.CustomData.Substring(TURRET_BASE.Length)+"\"";
                        status += "\n"+e.StackTrace;
                    }
                }
            }

            foreach(IMyMotorStator rot in rots) {
                if (rot.CustomData.Length <= 0) continue;
                string[] data = rot.CustomData.ToUpper().Split(';');
                if (data.Length < 2) {
                    try {
                        turNo = int.Parse(rot.CustomData.Substring(TURRET_BASE.Length));
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\n442:There was a parsing error: \"" + rot.CustomData.Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }
                    status += "\n445:There is no rotor definition for a rotor: " + turNo;
                }
                else {
                    try {
                        turNo = int.Parse(data[0].Substring(TURRET_BASE.Length));
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\n453:There was a parsing error: \"" + data[0].Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }

                    if (data[1].ToUpper().Equals("XROT")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasXROT()) {
                                turret.SetXROT(rot);
                                rot.CustomName = "["+TURRET_BASE+turNo+"] XROT";
                            }
                            else status += "\n463:There is a double-XROT situation going on: " + turNo;
                        }
                        else status += "\n465:There is an abandoned XROT rotor for turret no. " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROT")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasYROT()) {
                                turret.SetYROT(rot);
                                rot.CustomName = "["+TURRET_BASE+turNo+"] YROT";
                                if(!turret.HasBSCon()) foreach (IMyShipConnector con in conns) if (AreOnSameGrid(rot, con)) { turret.SetBSCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Base Con"; break; }
                            }
                            else status += "\n475:There is a double-YROT situation going on: " + turNo;
                        }
                        else status += "\n477:There is an abandoned YROT rotor for turret no. " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROTA")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasYROTA()) {
                                turret.SetYROTA(rot);
                                rot.CustomName = "["+TURRET_BASE+turNo+"] YROTA";
                                if (!turret.HasBSCon()) foreach (IMyShipConnector con in conns) if (AreOnSameGrid(rot, con)) { turret.SetBSCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Base Con"; break; }
                            }
                            else status += "\n486:There is a double-YROTA situation going on: " + turNo;
                        }
                        else status += "\n488:There is an abandoned YROTA rotor for turret no. " + turNo;
                    }
                    else status += "\n490:There is an incomprehensible definition for a rotor: " + data[1];
                }
            }
            Echo(status);
            Me.CustomData = status;
        }

        public string DoOurJobs() {
            Turret turret;
            string output = "";
            foreach(int key in turrets.Keys) {
                if(turrets.TryGetValue(key, out turret)) {
                    output += " "+TURRET_BASE+key+(key < 10 ? " " : "")+":" + turret.DoYourJob()+"\n\n";
                }
            }
            return output;
        }

        public void Output(object input, bool append=false) {
            string message = input is string ? (string)input : input.ToString();
            if (screens == null) {
                screens = new List<IMyTextPanel>();
                List<IMyTextPanel> temp = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(temp);
                foreach(IMyTextPanel screen in temp) { if (AreOnSameGrid(Me, screen) && screen.CustomName.Contains("[TRRT]")) screens.Add(screen); }
            }
            foreach (IMyTextPanel screen in screens) screen.WriteText(message, append);
        }

        public void Main(string argument, UpdateType updateSource) {
            /**/
            if((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger))> 0) {
                string[] args = argument.ToLower().Split(' ');
                Turret turret;
                if (args.Length > 0) {
                    long    id;
                    double  px, py, pz, sx, sy, sz;
                    switch (args[0]) {
                        case "tar":
                            if (args.Length > 3) {
                                if(turrets.TryGetValue(1,out turret)) {
                                    double
                                        X,
                                        Y,
                                        Z;

                                    if (Double.TryParse(args[1], out X) && Double.TryParse(args[2], out Y) && Double.TryParse(args[3], out Z)) {
                                        Vector3D vec = new Vector3D(X, Y, Z);
                                        turret.TrySetTarget(vec);
                                        turret.RLDCon.CustomData = turret.CanTarget(vec).ToString();
                                    }
                                    else {
                                        turret.ChangeState(Turret.State.STOW);
                                    }
                                }
                            }
                            else {
                                if (turrets.TryGetValue(1, out turret))
                                    turret.ChangeState(Turret.State.STOW);
                            }
                            break;

                        case "tars":
                            if (args.Length > 3) {
                                foreach(int key in turrets.Keys)
                                if (turrets.TryGetValue(key, out turret)) {
                                    double
                                        X,
                                        Y,
                                        Z;

                                    if (Double.TryParse(args[1], out X) && Double.TryParse(args[2], out Y) && Double.TryParse(args[3], out Z)) {
                                        Vector3D vec = new Vector3D(X, Y, Z);
                                        turret.TrySetTarget(vec);
                                        turret.RLDCon.CustomData = turret.CanTarget(vec).ToString();
                                    }
                                    else {
                                        turret.ClearTarget();
                                    }
                                }
                            }
                            else {
                                foreach (int key in turrets.Keys)
                                if (turrets.TryGetValue(key, out turret))
                                    turret.ClearTarget();
                            }
                            break;

                        case "test":
                            if (turrets.TryGetValue(1, out turret)) {
                                Vector3D vec = turret.CTRL.GetPosition(), fwd = Vector3D.Add(Vector3D.Multiply(turret.BSDCon.WorldMatrix.Backward, -50d), Vector3D.Multiply(turret.BSDCon.WorldMatrix.Left, 450d));
                                vec = Vector3D.Add(vec, fwd);
                                Vector3D frwd = Vector3D.Add(turret.CTRL.GetPosition(), Vector3D.Multiply(turret.BSDCon.WorldMatrix.Forward, 1000d));
                                turret.RLDCon.CustomData = turret.CanTarget(vec).ToString();

                                turret.TrySetTarget(vec);
                            }
                            break;

                        /*/
                        reg 43535435435435;123;43;-21;41;251;23
                        ID has 17-18 digits, and we will prepare for XYZ's in milions
                        formula for maximum number of chars in string taken by those coords would be
                        4 + X*(18+1+8+1+8+1+8+1+4+1+4+1+4) + (X-1)
                        which translates to
                        3 + X*61

                        IF we 'step up' our game to a tenth of a meter precision, this should bring us up to
                        4 + X*(18+1+9+1+9+1+9+1+5+1+5+1+5) + (X-1)
                        3 + X*67
                        /**/
                        case "reg":
                            string[] 
                                registry = Me.CustomData.Split('\n'),
                                row;
                            RegEntry entry;
                            Register.Delet();
                            for (int i=0; i < registry.Length; i++) {
                                row = registry[i].Split(';');
                                int ind = 0;
                                if (row     .Length > 6                 &&
                                    long    .TryParse(row[ind++], out id)   &&

                                    double  .TryParse(row[ind++], out px)   &&
                                    double  .TryParse(row[ind++], out py)   &&
                                    double  .TryParse(row[ind++], out pz)   &&

                                    double  .TryParse(row[ind++], out sx)   &&
                                    double  .TryParse(row[ind++], out sy)   &&
                                    double  .TryParse(row[ind++], out sz)   ) {
                                    entry = new RegEntry(new Vector3D(px/10, py/10, pz/10), new Vector3D(sx/10, sy/10, sz/10));
                                    Register.Set(id, entry);
                                }
                            }

                            break;

                        case "add":
                            if( args    .Length>7                   &&
                                long    .TryParse(args[1],  out id) &&

                                double  .TryParse(args[2],  out px) &&
                                double  .TryParse(args[3],  out py) &&
                                double  .TryParse(args[4],  out pz) &&

                                double  .TryParse(args[5],  out sx) &&
                                double  .TryParse(args[6],  out sy) &&
                                double  .TryParse(args[7],  out sz)) {
                                if (Register.Has(id)) {
                                    //TODO: Stuff here
                                }
                                Register.Set(id, new RegEntry(new Vector3D(px,py,pz), new Vector3D(sx,sy,sz)));
                            }
                            break;

                        case "del":
                            if (long.TryParse(args[1], out id)) Register.Delet(id);
                            break;
                    }
                }
            }
            else {
                Turret turret;
                /**/
                if (!InfoGot) {
                    TickNo++;
                    if (TickNo >= 10) {
                        TickNo = 10;
                        foreach (int key in turrets.Keys)
                            if (turrets.TryGetValue(key, out turret) && turret.hasTarget)
                                turret.ClearTarget();
                    }
                }
                else {
                    TickNo  = 0;
                    InfoGot = false;
                }
                LOG = DoOurJobs();
                Output(LOG);
                Echo(""+Register.Count());
                /**/
            }
            /**/
        }
    }
}