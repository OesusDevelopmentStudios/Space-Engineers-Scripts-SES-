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

namespace IngameScript {
    partial class Program : MyGridProgram {

        List<IMyTextPanel> screens;

        Dictionary<int, Turret> turrets;

        const string
            TURRET_BASE = "TRRT-";

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }

        public class Entry {
            public Vector3D 
                position,
                velocity;

            public Entry(Vector3D position, Vector3D velocity) {
                this.position = position;
                this.velocity = velocity;
            }

            public Entry(double px, double py, double pz, double vx, double vy, double vz) : this(new Vector3D(px,py,pz), new Vector3D(vx, vy, vz)) {

            }
        }


        public class Register {
            Dictionary<long, Entry> content = new Dictionary<long, Entry>();

            public bool Get(long id, out Entry ent) { return content.TryGetValue(id, out ent); }

            public void Set(long id, Entry ent) { content.Add(id, ent); }

            public void Delet(long id) { content.Remove(id); }
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

            IMyShipConnector
                RLDCon,
                BSDCon;

            List<IMySmallGatlingGun>
                weaponry;

            Vector3D
                currTarget;

            bool
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
                this.currTarget= new Vector3D(0,0,0);
                this.hasTarget = false;
                if (weaponry != null)
                    this.weaponry = weaponry;
                else
                    this.weaponry = new List<IMySmallGatlingGun>();
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
            public void SetBSCon(IMyShipConnector BSDCon)   { this.BSDCon = BSDCon; }

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

            public void SetTarget(double X, double Y, double Z) { SetTarget(new Vector3D(X, Y, Z)); }

            public void SetTarget(Vector3D target) {
                this.currTarget = target;
                this.hasTarget = true;

                ChangeState(State.TRACK);
            }

            public void ClearTarget() {this.hasTarget = false;}

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
                XROT.RotorLock = false;
                YROT.RotorLock = false;
                YROTA.RotorLock = false;

                if (XROT != null)   XROT.TargetVelocityRPM = 5*X;
                if (YROT != null)   YROT.TargetVelocityRPM = 5*Y;
                if (YROTA != null)  YROTA.TargetVelocityRPM = -5*Y;
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

            public string DoYourJob() {
                string output = "Status: ";
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
                    return output+"Manually Controlled.";
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
                        return output + "Stowing back the Turret...";
                    }
                    Vector3D
                        target  = currState.Equals(State.TRACK)?  currTarget:Vector3D.Add(BSDCon.GetPosition(),Vector3D.Multiply(BSDCon.WorldMatrix.Forward,100)),
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
                    /*///Old Culprit System
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
                        return output+"Stowing...";
                    }
                    else
                    if(currState.Equals(State.TRACK)) {
                        if (this.RLDCon.Enabled == true) this.RLDCon.Enabled = false;
                        if (!this.hasTarget) {
                            ChangeState(State.STOW);
                            return "Lost Target.";
                        }
                        //Move(CulpritToMove(culprit, (float)(curr.Length())));
                        DoubleCTM(sorted[0].dirInt, sorted[0].vLength, sorted[1].dirInt, sorted[1].vLength);
                        /**/
                        if(curr.Length() < 0.1f && Vector3D.Subtract(target,me).Length()<=800) { 
                            Fire(true);
                            return output + ("Firing...");
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
                                foreach (IMyShipConnector con in conns) if (AreOnSameGrid(rot, con)) { turret.SetBSCon(con); con.CustomName = "[" + TURRET_BASE + turNo + "] Base Con"; break; }
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
                            }
                            else status += "\n486:There is a double-YROTA situation going on: " + turNo;
                        }
                        else status += "\n488:There is an abandoned YROTA rotor for turret no. " + turNo;
                    }
                    else status += "\n490:There is an incomprehensible definition for a rotor: " + data[1];
                }
            }
            Echo(status);
        }

        public string DoOurJobs() {
            Turret turret;
            string output = "";
            foreach(int key in turrets.Keys) {
                if(turrets.TryGetValue(key, out turret)) {
                    output += TURRET_BASE+key+":\n"+ turret.DoYourJob()+"\n\n";
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
                    switch (args[0]) {
                        case "tar":
                            if (args.Length > 3) {
                                if(turrets.TryGetValue(1,out turret)) {
                                    double
                                        X,
                                        Y,
                                        Z;

                                    if (Double.TryParse(args[1], out X) && Double.TryParse(args[2], out Y) && Double.TryParse(args[3], out Z)) turret.SetTarget(X,Y,Z);
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

                    }
                }
            }
            else {
            /**/
            Output(DoOurJobs());
            /**/
            }
            /**/
        }
    }
}
