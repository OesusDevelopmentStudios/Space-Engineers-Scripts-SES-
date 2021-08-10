using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        public const string SCRIPT_TAG = "BRMS-";
        public Arm arm;

        public class Arm {
            public IMyMotorAdvancedStator 
                BaseHinge, 
                ElbowHinge1, 
                ElbowHinge2;

            public List<IMyExtendedPistonBase>
                BasePistons, 
                ElbowPistons;

            float
                BaseTarget=0,
                ElbowTarget=0;

            float RadToDeg(float radians) {
                return radians*  180 / (float)(Math.PI);
            }

            float DegToRad(float deg) {
                return deg * (float)(Math.PI) / 180;
            }

            float Difference(float num1, float num2) {
                float 
                    bigger  = num1 > num2 ? num1 : num2,
                    smaller = num1 < num2 ? num1 : num2;

                return bigger - smaller;
            }

            public void SetBaseHinge(float baseDeg) {
                baseDeg = baseDeg < 2 ? 2 : baseDeg;
                BaseTarget = baseDeg;
            }

            public void SetElbHinge(float elbDeg) {
                elbDeg = elbDeg < 2 ? 2 : elbDeg;
                ElbowTarget = elbDeg;
            }

            public void SetHinges(float baseDeg, float elbDeg) {
                SetBaseHinge(baseDeg);
                SetElbHinge(elbDeg);
            }

            public void SetPistonVelocity(float velocity, bool elbow = true) {
                List<IMyExtendedPistonBase> list = elbow ? ElbowPistons : BasePistons;

                foreach (IMyExtendedPistonBase piston in list) {
                    piston.Velocity = velocity;
                }
            }

            public string DoYourJob() {
                string output = "";

                float 
                    BaseAngle = 90 + RadToDeg(BaseHinge.Angle),
                    ElbAngle = 180 - (RadToDeg(ElbowHinge1.Angle) + RadToDeg(ElbowHinge2.Angle)),
                    difference;

                output += "BT: " + BaseTarget + " BA: " + BaseAngle + " ET: " + ElbowTarget + " EA: " + ElbAngle;

                if ((difference=Difference(BaseTarget, BaseAngle)) > 1) {
                    if (BaseTarget > BaseAngle) {
                        BaseHinge.TargetVelocityRPM = difference / 5;
                    }
                    else {
                        BaseHinge.TargetVelocityRPM = -difference / 5;
                    }
                }
                else BaseHinge.TargetVelocityRPM = 0;


                if ((difference = Difference(ElbowTarget, ElbAngle)) > 1) {
                    if (ElbowTarget > ElbAngle) {
                        ElbowHinge1.TargetVelocityRPM = -difference / 10;
                        ElbowHinge2.TargetVelocityRPM = -difference / 10;
                    }
                    else {
                        ElbowHinge1.TargetVelocityRPM = difference / 10;
                        ElbowHinge2.TargetVelocityRPM = difference / 10;
                    }
                }
                else {
                    ElbowHinge1.TargetVelocityRPM = 0;
                    ElbowHinge2.TargetVelocityRPM = 0;
                }

                return output;
            }

            public bool Evaluate() {
                if (BaseHinge == null || ElbowHinge1 == null || ElbowHinge2 == null || BasePistons == null || ElbowPistons == null ) return false;
                BaseTarget  = 90 + RadToDeg(BaseHinge.Angle);
                ElbowTarget = 180 - (RadToDeg(ElbowHinge1.Angle) + RadToDeg(ElbowHinge2.Angle));
                return true;
            }

        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save() {

        }

        public bool GetTheArm() {
            List<IMyMotorAdvancedStator> hinges = new List<IMyMotorAdvancedStator>();
            List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();
            GridTerminalSystem.GetBlocksOfType(hinges);
            GridTerminalSystem.GetBlocksOfType(pistons);

            arm = new Arm();

            foreach(IMyMotorAdvancedStator hinge in hinges) {
                if (hinge.CustomData.StartsWith(SCRIPT_TAG)) {
                    string type = hinge.CustomData.Substring(SCRIPT_TAG.Length);
                    switch (type.ToUpper()) {
                        case "B":
                            arm.BaseHinge = hinge;
                            break;

                        case "E":
                            if (arm.ElbowHinge1 != null) 
                                arm.ElbowHinge2 = hinge;
                            else 
                                arm.ElbowHinge1 = hinge;
                            break;
                    }
                }
            }

            foreach(IMyExtendedPistonBase piston in pistons) {
                if (piston.CustomData.StartsWith(SCRIPT_TAG)) {
                    string type = piston.CustomData.Substring(SCRIPT_TAG.Length);
                    switch (type.ToUpper()) {
                        case "B":
                            if (arm.BasePistons == null) arm.BasePistons = new List<IMyExtendedPistonBase>();
                            arm.BasePistons.Add(piston);
                            break;

                        case "E":
                            if (arm.ElbowPistons == null) arm.ElbowPistons = new List<IMyExtendedPistonBase>();
                            arm.ElbowPistons.Add(piston);
                            break;
                    }
                }
            }

            return arm.Evaluate();
        }

        public void Main(string argument, UpdateType updateSource) {
            /**/
            if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) > 0) {
                string[] args = argument.ToLower().Split(' ');
                if (args.Length > 0 && arm != null) {
                    float x, y;
                    switch (args[0].ToLower()) {
                        case "set":
                            if(args.Length>2 && float.TryParse(args[1], out x) && float.TryParse(args[2], out y)) {
                                arm.SetHinges(x,y);
                            }
                            break;

                        case "lightunpack":
                            arm.SetHinges(120, 30);
                            break;

                        case "unpack":
                            arm.SetHinges(130, 40);
                            break;

                        case "pack":
                            arm.SetHinges(0, 0);
                            break;

                        case "extend":
                            arm.SetPistonVelocity(0.03f);
                            break;

                        case "retract":
                            arm.SetPistonVelocity(-1f);
                            break;

                        case "baseextend":
                            arm.SetPistonVelocity(0.03f,false);
                            break;

                        case "baseretract":
                            arm.SetPistonVelocity(-0.06f, false);
                            break;

                        default:
                            Echo("Command unknown");
                            break;
                    }
                }
            }
            else {
                if (arm != null) {
                    string output = arm.DoYourJob();
                    Echo(output);
                }
                else {
                    if (!GetTheArm()) {
                        arm = null;
                    }
                }
            }
            /**/
        }
    }
}
