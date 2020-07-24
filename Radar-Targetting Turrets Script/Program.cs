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

        const string
            TURRET_BASE = "TRRT-";

        bool AreOnSameGrid(IMyCubeBlock one, IMyCubeBlock two) {
            return one.CubeGrid.Equals(two.CubeGrid);
        }


        Dictionary<int,Turret> turrets;

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
            //public int number;

            IMyRemoteControl
                CTRL;

            IMyMotorStator 
                XROT, 
                YROT, 
                YROTA;

            IMyShipConnector
                RLDCon;

            List<IMySmallGatlingGun>
                weaponry;

            public Turret(IMyRemoteControl CTRL = null, IMyMotorStator XROT = null, IMyMotorStator YROT = null, IMyMotorStator YROTA = null, IMyShipConnector RLDCon = null, List<IMySmallGatlingGun> weaponry = null) {
                this.CTRL  = CTRL;
                this.XROT  = XROT;
                this.YROT  = YROT;
                this.YROTA = YROTA;
                this.RLDCon= RLDCon;
                if (weaponry != null)
                    this.weaponry = weaponry;
                else
                    this.weaponry = new List<IMySmallGatlingGun>();
            }

            public void AddWeapon(IMySmallGatlingGun gun) { weaponry.Add(gun); }
            public void AddWeapon(List<IMySmallGatlingGun> guns) { weaponry.AddList(guns); }

            public bool HasCTRL()   { return this.CTRL  != null; }
            public bool HasXROT()   { return this.XROT  != null; }
            public bool HasYROT()   { return this.YROT  != null; }
            public bool HasYROTA()  { return this.YROTA != null; }
            public bool HasCon()    { return this.RLDCon != null; }

            public void SetCTRL(IMyRemoteControl CTRL)  { this.CTRL = CTRL; }
            public void SetXROT(IMyMotorStator XROT)    { this.XROT = XROT; }
            public void SetYROT(IMyMotorStator YROT)    { this.YROT = YROT; }
            public void SetYROTA(IMyMotorStator YROTA)  { this.YROTA = YROTA; }
            public void SetCon(IMyShipConnector RLDCon) { this.RLDCon = RLDCon; }

            public void Fire(bool doThat) {
                foreach(IMySmallGatlingGun gun in weaponry) {
                    gun.Enabled = doThat;
                }
            }

            public void DoYourJob() {
                if (CTRL.IsUnderControl) {
                    XROT.UpperLimitDeg = float.MaxValue;
                    XROT.LowerLimitDeg = float.MinValue;

                    XROT.TargetVelocityRPM = 0;
                    YROT.TargetVelocityRPM = 0;
                    YROTA.TargetVelocityRPM = 0;

                    float X = CTRL == null ? 0f : CTRL.RotationIndicator.Y;
                    float Y = CTRL == null ? 0f : CTRL.RotationIndicator.X;

                    XROT.RotorLock = false;
                    YROT.RotorLock = false;
                    YROTA.RotorLock = false;

                    if (XROT != null) XROT.TargetVelocityRPM = (X / 5f);
                    if (YROT != null) YROT.TargetVelocityRPM = (Y / 5f);
                    if (YROTA != null) YROTA.TargetVelocityRPM = -(Y / 5f);
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
            List<IMySmallGatlingGun>gatGuns = new List<IMySmallGatlingGun>();
            List<IMyShipConnector>  conns   = new List<IMyShipConnector>();
            List<IMySmallGatlingGun>ctrlGuns;
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

                            foreach(IMySmallGatlingGun gun in gatGuns) {if (AreOnSameGrid(gun, ctrl)) ctrlGuns.Add(gun);}

                            foreach(IMyShipConnector con in conns) { if (AreOnSameGrid(con, ctrl)) { turret.SetCon(con); break; } }

                            if (!turret.HasCon()) status += "\nThe turret does not have a reload connector: " + turNo;

                            if (ctrlGuns.Count > 0) turret.AddWeapon(ctrlGuns);
                            else status += "\nThere is a turret without weaponry on it: " + turNo;

                            turrets.Add(turNo, turret);
                        }
                        else status += "\nThere is more than one turret with the same number: " + turNo;
                    }
                    catch(Exception e) {
                        e.ToString();
                        status += "\nThere was a parsing error: \""+ ctrl.CustomData.Substring(TURRET_BASE.Length)+"\"";
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
                        status += "\nThere was a parsing error: \"" + rot.CustomData.Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }
                    status += "\nThere is no rotor definition for a rotor: " + turNo;
                }
                else {
                    try {
                        turNo = int.Parse(data[0].Substring(TURRET_BASE.Length));
                    }
                    catch (Exception e) {
                        e.ToString();
                        status += "\nThere was a parsing error: \"" + data[0].Substring(TURRET_BASE.Length) + "\"";
                        continue;
                    }

                    if (data[1].ToUpper().Equals("XROT")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasXROT()) turret.SetXROT(rot);
                            else status += "\nThere is a double-XROT situation going on: " + turNo;
                        }
                        else status += "\nThere is an abandoned XROT rotor for turret no. " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROT")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasYROT()) turret.SetYROT(rot);
                            else status += "\nThere is a double-YROT situation going on: " + turNo;
                        }
                        else status += "\nThere is an abandoned YROT rotor for turret no. " + turNo;
                    }
                    else
                    if (data[1].ToUpper().Equals("YROTA")) {
                        if (turrets.TryGetValue(turNo, out turret)) {
                            if (!turret.HasYROTA()) turret.SetYROTA(rot);
                            else status += "\nThere is a double-YROTA situation going on: " + turNo;
                        }
                        else status += "\nThere is an abandoned YROTA rotor for turret no. " + turNo;
                    }
                    else status += "\nThere is an incomprehensible definition for a rotor: " + data[1];
                }
            }

            Echo(status);
        }

        public void DoOurJobs() {
            Turret turret;
            foreach(int key in turrets.Keys) {
                if(turrets.TryGetValue(key, out turret)) {
                    turret.DoYourJob();
                }
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            /*
            if((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger))> 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0) {
                    switch (args[0]) {

                    }
                }
            }
            else {
            /**/
            DoOurJobs();
            /*
            }*/
        }
    }
}
