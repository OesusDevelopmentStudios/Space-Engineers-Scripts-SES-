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


		List<IMyCameraBlock> cameras;
		List<MyDetectedEntityInfo> targets;
		List<ActiveSensor> acSens = new List<ActiveSensor>();
		IMyTextPanel tPanel;
		MyDetectedEntityInfo current;
		MyDetectedEntityInfo target;
		Vector3D curTarget;
		Vector3D NOTHING = new Vector3D(-0.5d, -0.5d, -0.5d);
		double range = 5000d;
		float y, x, step = 5f, max;
		int timesTriedScan = 0, timeToScan = 10;

		Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
			cameras = new List<IMyCameraBlock>();
			targets = new List<MyDetectedEntityInfo>();
			curTarget = NOTHING;
			tPanel = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;
			max = cameras.Count() > 0 ? cameras[0].RaycastConeLimit : 45;
			x = 0;//-1*max;
			y = 0;//-1*max;

			for(int i=0; i<6; i++) {
				string preffix = "";
				if		(i == 1) { preffix = "FWD "; }
				else if (i == 2) { preffix = "BWD "; }
				else if (i == 3) { preffix = "LFT "; }
				else if (i == 4) { preffix = "RIG "; }
				else if (i == 5) { preffix = "TOP "; }
				else if (i == 6) { preffix = "BOT "; }

				IMyMotorStator		XRot = GridTerminalSystem.GetBlockWithName(preffix + "Targetting Ray/Pitch Rotor") as IMyMotorStator; //Targetting Ray/Pitch Rotor
				IMyMotorStator		YRot = GridTerminalSystem.GetBlockWithName(preffix + "Targetting Ray/Yaw Rotor") as IMyMotorStator; //Targetting Ray/Yaw Rotor
				IMyShipController	Cont = GridTerminalSystem.GetBlockWithName(preffix + "Targetting Ray/Remote") as IMyShipController; //Targetting Ray/Remote
				IMyCameraBlock		Cam  = GridTerminalSystem.GetBlockWithName(preffix + "Targetting Ray/Camera") as IMyCameraBlock; //Targetting Ray/Camera

				if (XRot == null || YRot == null || Cont == null || Cam == null) {
					if (XRot == null && YRot == null && Cont == null && Cam == null) continue;
					if (XRot == null) Echo("XRot\n");
					if (YRot == null) Echo("YRot\n");
					if (Cont == null) Echo("Cont\n");
					if (Cam  == null) Echo("Cam\n");
					acSens.Add(new ActiveSensor(null, null, null, null)); 
				}
				else acSens.Add(new ActiveSensor(Cont, Cam, XRot, YRot));
			}

			GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(cameras);
			foreach (IMyCameraBlock cam in cameras) cam.EnableRaycast = true;
			foreach (ActiveSensor sens in acSens) {
				for(int i = 0; i < cameras.Count(); i++) {
					if (sens.Cam == null) continue;
					if (sens.Cam.Equals(cameras[i])) {
						cameras.RemoveAt(i); break;
					}
				}
			}
		}

		private class ActiveSensor {
			public IMyMotorStator		XRot;
			public IMyMotorStator		YRot;
			public IMyShipController	Cont;
			public IMyCameraBlock		Cam;


			float trackingSpeed = 30f;
			double maxDev = 0.04d;

			Vector2 
				UPP_CMD = new Vector2( 0,-1),
				DWN_CMD = new Vector2( 0, 1),
				LFT_CMD = new Vector2(-1, 0),
				RIG_CMD = new Vector2( 1, 0),
				STP_CMD = new Vector2( 0, 0);

			static Vector3D NOTHING = new Vector3D(-0.5d, -0.5d, -0.5d);

			public ActiveSensor(IMyShipController Cont, IMyCameraBlock Cam, IMyMotorStator XRot, IMyMotorStator YRot) {
				this.Cont	= Cont;
				this.Cam	= Cam;
				this.XRot	= XRot;
				this.YRot	= YRot;
			}

			class NavPrompt {
				public int dirInt;
				public double vLength;

				public NavPrompt(int dir, Vector3D input) {
					this.dirInt = dir;
					this.vLength = input.Length();
				}
			}

			public Vector2 DirToCmd(int lndDir, int culprit) {
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
					else return LFT_CMD;
				}
				else {
					if (culprit <= 2) {
						if (lndDir % 2 == culprit % 2) return UPP_CMD; /// UPP
						else return DWN_CMD; /// DWN
					}
					else return UPP_CMD;
				}
			}

			public Vector3D DirintToVec(int dirint) {
				switch (dirint) {
					case 1:
						return Cont.WorldMatrix.Forward;
					case 2:
						return Cont.WorldMatrix.Backward;
					case 3:
						return Cont.WorldMatrix.Left;
					case 4:
						return Cont.WorldMatrix.Right;
					case 5:
						return Cont.WorldMatrix.Up;
					case 6:
						return Cont.WorldMatrix.Down;
				}
				return NOTHING;
			}

			public Vector3D CutVector(Vector3D vector) { return CutVector(vector, 3); }

			public Vector3D CutVector(Vector3D vector, int decNo) {
				double X = Math.Round(vector.X, decNo),
					Y = Math.Round(vector.Y, decNo),
					Z = Math.Round(vector.Z, decNo);

				return new Vector3D(X, Y, Z);
			}

			public MyDetectedEntityInfo Scan(Vector3D curTarget) {return Cam.Raycast(curTarget);}

			public string Align(Vector3D curTarget) {
				if (Cont.IsUnderControl) {
					float X = Cont == null ? 0f : Cont.RotationIndicator.X;
					float Y = Cont == null ? 0f : Cont.RotationIndicator.Y;

					if (XRot != null) XRot.TargetVelocityRPM = X / 2;
					if (YRot != null) YRot.TargetVelocityRPM = Y / 2;
					return "";
				}
				string putin = "";
				Vector3D ship = Cont.GetPosition(), sub;
				Vector2 command;
				sub = (curTarget==null || curTarget == NOTHING)? ship : CutVector(Vector3D.Normalize(Vector3D.Subtract(curTarget, ship)));
				Vector3D curr = Vector3D.Subtract(CutVector(DirintToVec(1)), sub);
				double distance = Vector3D.Subtract(curTarget, ship).Length();
				List<NavPrompt> prompts = new List<NavPrompt>();
				List<IMyThrust> group = new List<IMyThrust>();
				int culprit;
				for (int i = 1; i < 7; i++) prompts.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), sub)));
				if (prompts[0].vLength <= maxDev) {
					command = STP_CMD;
				}
				else {
					prompts = prompts.OrderBy(o => o.vLength).ToList();

					putin = "\nNavigational prompts:";

					for (int i = 0; i < 6; i++) putin += "\n" + (i + 1) + " length : " + prompts[i].vLength;

					for (culprit = 0; culprit < 3; culprit++) {
						if (prompts[culprit].dirInt != 1 && prompts[culprit].dirInt != 2) break;
					}
					culprit = prompts[culprit].dirInt;

					command = DirToCmd(2, culprit);

					putin += "\n\n" + command;
				}
				XRot.TargetVelocityRPM = (float)(/*/curr.Length() * /**/trackingSpeed * command.Y);
				YRot.TargetVelocityRPM = (float)(/*/curr.Length() * /**/trackingSpeed * command.X);
				return putin;
			}
		}

		public void ActiveScan() {
			MyDetectedEntityInfo info;
			foreach (ActiveSensor sens in acSens) {
				if (sens.Cam == null) {
					continue;
				}
				sens.Align(curTarget);
				if (timesTriedScan++ >= timeToScan) {
					timesTriedScan = 0;
					timeToScan = timeToScan > 0 ? timeToScan - 1 : 0;
					MyDetectedEntityInfo temp = sens.Scan(curTarget);
					if (
						!temp.IsEmpty() &&/*/ temp.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies && (temp.Type == MyDetectedEntityType.SmallGrid ||/**/ temp.Type == MyDetectedEntityType.LargeGrid//)
						) {
						curTarget = temp.Position;
						target = temp;
						return;
					}
				}
			}
			if (cameras.Count() <= 0) return;
			foreach (IMyCameraBlock cam in cameras) {
				info = cam.Raycast(curTarget);
				if (!info.IsEmpty()) { curTarget = info.Position; target = info; return; }
			}
			info = cameras[0].Raycast(curTarget);
		}

		public void Output(string message) {
			if (tPanel != null) tPanel.WriteText(message, false);
		}

		public void CLS() {
			if(tPanel!=null) tPanel.WriteText("", false);
		}

		public void RunPassiveScan() {
			if (cameras[0].CanScan(range)) {
				int onSens = 0;
				string scrap = "";
				foreach (ActiveSensor AS in acSens) if (AS.Cam != null) { onSens++; scrap = "\nActive Scan Range: "+ AS.Cam.AvailableScanRange; }

				string message = "Active Sensors: "+onSens+scrap+"\nPassive Scan Range: " + cameras[0].AvailableScanRange + "\nTargets:";
				if (!target.IsEmpty()) message += "\n CURRENT: "+target.Position;
				foreach (IMyCameraBlock cam in cameras) {
					current = cam.Raycast(range, y, x);
					if (!current.IsEmpty()) {
						int index = -1;
						for (int i = 0; i < targets.Count(); i++) if (targets[i].EntityId == current.EntityId) index = i;
						if (index != -1) targets.RemoveAt(index);

						if (current.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies && (current.Type == MyDetectedEntityType.SmallGrid || current.Type == MyDetectedEntityType.LargeGrid)) curTarget = current.Position;
						targets.Add(current);
					}
				}
				x += step;

				if (y > max) {
					y = -1 * max;
					x = -1 * max;
				}
				else if (x > max) {
					y += step;
					x = -1 * max;
				}

				foreach (MyDetectedEntityInfo enti in targets) message += "\n" + enti.Name + " " + enti.Type + " " + enti.Relationship + enti.Position;

				Output(message);
			}
		}

		public void Main(string argument, UpdateType updateSource) {
			String[] eval = argument.ToUpper().Split(' ');
			if (eval[0].Equals("TRACK")) {
				if (eval.Length > 3) { curTarget = new Vector3D(float.Parse(eval[1]), float.Parse(eval[2]), float.Parse(eval[3])); }
				else { curTarget = new Vector3D(0f, 0f, 0f); }
			}
				
				if(curTarget!=NOTHING)	ActiveScan();
				if(cameras.Count()>0)	RunPassiveScan();
		}
    }
}
