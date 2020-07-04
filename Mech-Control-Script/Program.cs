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

        IMyShipController SHIP_CONTROLLER;

        Vector3D UPP_CMD = new Vector3D(0, -1, 0),
            DWN_CMD = new Vector3D(0, 1, 0),
            LFT_CMD = new Vector3D(-1, 0, 0),
            RIG_CMD = new Vector3D(1, 0, 0),
            CLK_CMD = new Vector3D(0, 0, 1),
            ALK_CMD = new Vector3D(0, 0, -1),
            NOTHING = new Vector3D(44,44,44);

        const int FW_VAL = 2,
                  UP_VAL = 6,
                  LF_VAL = 3,
                  RT_VAL = 4,
                  BW_VAL = 1,
                  DW_VAL = 5;

        public Program() {
            //Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update1;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Leg.buildDictionary();
            SHIP_CONTROLLER = GridTerminalSystem.GetBlockWithName("Mech/Cockpit") as IMyShipController;
            setUp();
        }

        public void setUp() {
            IMyMotorStator
                hipp = GridTerminalSystem.GetBlockWithName("Left Hip Rotor") as IMyMotorStator,
                knee = GridTerminalSystem.GetBlockWithName("Left Knee Rotor") as IMyMotorStator,
                ankl = GridTerminalSystem.GetBlockWithName("Left Ankle Rotor") as IMyMotorStator,
                anrt = GridTerminalSystem.GetBlockWithName("Left Rotat Ankle Rotor") as IMyMotorStator;

            ///LHip Light

            IMyInteriorLight
                hipL = GridTerminalSystem.GetBlockWithName("LHip Light") as IMyInteriorLight,
                kneL = GridTerminalSystem.GetBlockWithName("LKne Light") as IMyInteriorLight,
                ankL = GridTerminalSystem.GetBlockWithName("LAnk Light") as IMyInteriorLight,
                legL = GridTerminalSystem.GetBlockWithName("LLeg Light") as IMyInteriorLight;

            if (hipp == null || knee == null || ankl == null || anrt == null) left = null;
            else left = new Leg(hipp, knee, ankl, anrt, true, 90f, 180f);
            left .setLights(hipL, kneL, ankL, legL);

                hipp = GridTerminalSystem.GetBlockWithName("Right Hip Rotor") as IMyMotorStator;
                knee = GridTerminalSystem.GetBlockWithName("Right Knee Rotor") as IMyMotorStator;
                ankl = GridTerminalSystem.GetBlockWithName("Right Ankle Rotor") as IMyMotorStator;
                anrt = GridTerminalSystem.GetBlockWithName("Right Rotat Ankle Rotor") as IMyMotorStator;


                hipL = GridTerminalSystem.GetBlockWithName("RHip Light") as IMyInteriorLight;
                kneL = GridTerminalSystem.GetBlockWithName("RKne Light") as IMyInteriorLight;
                ankL = GridTerminalSystem.GetBlockWithName("RAnk Light") as IMyInteriorLight;
                legL = GridTerminalSystem.GetBlockWithName("RLeg Light") as IMyInteriorLight;

            if (hipp == null || knee == null || ankl == null || anrt == null) right = null;
            else right = new Leg(hipp, knee, ankl, anrt, false, 270f, 360f);
            right.setLights(hipL, kneL, ankL, legL);

            if (left != null)  left .set(init.X, init.Y);
            if (right != null) right.set(init.X, init.Y);

            List<IMyLandingGear> temp   = new List<IMyLandingGear>();
            List<IMyLandingGear> Lgears  = new List<IMyLandingGear>();
            List<IMyLandingGear> Rgears = new List<IMyLandingGear>();
            GridTerminalSystem.GetBlocksOfType(temp);

            foreach (IMyLandingGear gear in temp) {
                if(gear.CustomName.Equals("Left Gear" )) {
                    Lgears.Add(gear);
                }
                else
                if(gear.CustomName.Equals("Right Gear")) {
                    Rgears.Add(gear);
                }
            }

            if (left != null)  left .setGears(Lgears);
            if (right != null) right.setGears(Rgears);
        }

        /**/
        class Leg {
            float precision = 0.25f;
            float minSpeed  = 0.5f;

            IMyMotorStator
                hipp,
                knee,
                ankl,
                anrt;

            IMyInteriorLight
                hipL,
                kneL,
                ankL,
                legL;

            List<IMyLandingGear>
                gears;

            static Dictionary<string, Profile> Profiles;
            /*/

            Vector2 init = new Vector2(-55f, 110f),
                    stdby = new Vector2(-90f, 145f);
            /**/

            public static void buildDictionary() {
                Profiles = new Dictionary<string, Profile>();
                Vector2[] array = { new Vector2(-55.0f, 130.0f), new Vector2(-22.5f, 120.0f), new Vector2(10.0f, 70.0f), new Vector2(-22.5f, 100.0f), new Vector2(-55.0f, 110.0f), new Vector2(-70.0f, 100.0f), new Vector2(-85.0f, 90.0f), new Vector2(-70.0f, 130.0f) };
                Profiles.Add("init", new Profile(array));
                //Profiles.Add("stdby", new Profile(-90f, -90f, -90f, 145f,1));
            }

            public Profile currProf;

            public 
            bool 
                isLeft,
                hasWork;

            float
                hipOffset,
                kneeOffset;

            public
            float
                hippTarget,
                kneeTarget,
                anklTarget;
            
            public class Profile {
                public 
                    List<Vector2>
                    steps;

                public
                    int
                    progress;

                public Profile(Profile copy) {
                    this.steps = new List<Vector2>();
                    this.steps.AddList(copy.steps);
                }

                public Profile(Vector2[] array) {
                    this.steps = new List<Vector2>();
                    this.steps.AddArray(array);
                }

                /**/
                public float getHValue() {return steps[progress].X;}

                public float getKValue() {return steps[progress].Y;}

                public void setProgress(int prog) {
                    this.progress = prog;
                }

                public void goFurther() {
                    progress++;
                    if (progress >= steps.Count) progress = 0;
                }
                /**/
            }

            public Leg(IMyMotorStator hipp, IMyMotorStator knee, IMyMotorStator ankl, IMyMotorStator anrt,
                       bool left, float hipOffset, float kneeOffset) {
                this.hipp       = hipp;
                this.knee       = knee;
                this.ankl       = ankl;
                this.anrt       = anrt;
                this.isLeft     = left;
                this.hasWork    = false;
                this.hipOffset  = hipOffset;
                this.kneeOffset = kneeOffset;
                this.gears      = new List<IMyLandingGear>();
                setProfile("init");
                set(-60f, 120f);
            }
            /**/
            // -55.0f, 110.0f  -22.5f, 120.0f  10.0f, 70.0f  -22.5f, 100.0f  -55.0f, 110.0f  -70.0f, 100.0f  -85.0f, 90.0f  -70.0f, 130.0f
            // false           false           true          true            true            true            true           false

            // -55.0f,110.0f  -33.33f,120.0f  -11.66f,110.0f  10.0f,70.0f  -11.66f,93.33f  -33.33f,100.0f  -55.0f,110.0f  -65.0f,110.0f  -75.0f,60.0f  -85.0f,84.44f  -75.0f,130.0f  -65.0f,135.0f
            // false           false          false           true         true            true            true           true           true          true           false          false


            public bool shouldILock() {
                int falseNo = currProf.steps.Count/4,
                    falseEnd= falseNo/2;
                falseEnd = currProf.steps.Count - 1 - falseEnd;

                if (currProf.progress <= falseNo /*|| currProf.progress > (falseEnd)/**/) return false;
                else return true;
            }

            public float moveWithProf(){
                float 
                    HValue = currProf.getHValue(), 
                    KValue = currProf.getKValue();
                currProf.goFurther();
                lockGears(shouldILock());
                return set(HValue, KValue);
            }

            public float setProfile(string name) {
                name = name.ToLower();
                Profile temp;
                if(Profiles.TryGetValue(name, out temp)) {
                    this.currProf = new Profile(temp);
                    if (this.isLeft) {
                        int index = this.currProf.steps.Count / 2;
                        this.currProf.setProgress(index);
                    }
                    else
                        this.currProf.setProgress(0);
                    return moveWithProf();
                }
                return 0;
            }
            /**/
            public void set() {
                float
                    hAngle = radToDeg(hipp.Angle),
                    kAngle = radToDeg(knee.Angle),
                    aAngle = radToDeg(ankl.Angle);

                float multiplier = isLeft ? 1f : -1f;

                float hip = (hAngle - hipOffset) * multiplier,
                      kne = (kAngle - kneeOffset)* multiplier,
                      ang;

                if (hip < 0) hip += 360;
                else if (hip > 360) hip -= 360;

                if (kne < 0) kne += 360;
                else if (kne > 360) kne -= 360;

                ang = (hip + kne) % 360;

                set(hip, kne, ang);

                anrt.RotorLock = true;
            }

            public float set(float hippTarget, float kneeTarget) {
                float multiplier = isLeft ?  1f : -1f;

                hippTarget = hippTarget < -85 ? -85 : (hippTarget > 20 ? 20 : hippTarget);

                float hip = (multiplier * hippTarget) + hipOffset,
                      kne = (multiplier * kneeTarget) + kneeOffset;

                if      (hip < 0  ) hip += 360;
                else if (hip > 360) hip -= 360;

                if      (kne < 0)   kne += 360;
                else if (kne > 360) kne -= 360;

                return set(hippTarget, kneeTarget, (hip + kne) % 360);
            }

            public float set(float hippTarget, float kneeTarget, float anklTarget) {
                float multiplier = isLeft ? 1f : -1f;

                float hip = (multiplier * hippTarget) + hipOffset,
                      kne = (multiplier * kneeTarget) + kneeOffset;

                float output = (float)((9.5f * Math.Cos(Leg.degToRad(hippTarget))) + (9.5f * Math.Cos(Leg.degToRad(kneeTarget + hippTarget))));

                if      (hip < 0) hip += 360;
                else if (hip > 360) hip -= 360;

                if      (kne < 0) kne += 360;
                else if (kne > 360) kne -= 360;

                this.hippTarget = hip;
                this.kneeTarget = kne;
                this.anklTarget = anklTarget;

                this.hasWork    = true;
                return output;
            }

            public void setGears(List<IMyLandingGear> gears) {
                this.gears = new List<IMyLandingGear>();
                this.gears.AddList(gears);
            }

            public void setLights(IMyInteriorLight hip, IMyInteriorLight kne, IMyInteriorLight ank, IMyInteriorLight leg) {
                this.hipL = hip;
                this.kneL = kne;
                this.ankL = ank;
                this.legL = leg;

            }

            public void work() {
                float
                    hAngle = radToDeg(hipp.Angle),
                    kAngle = radToDeg(knee.Angle),
                    aAngle = radToDeg(ankl.Angle);

                if (isVeryDifferent(hAngle, hippTarget)) {
                    hipp.RotorLock = false;
                    float multiplier = 1f;
                    if (hippTarget > hAngle) {
                        if (getDifference(hAngle, hippTarget) <= 180)
                            hipp.TargetVelocityRPM = multiplier *  getVel(hippTarget, hAngle);
                        else
                            hipp.TargetVelocityRPM = multiplier * -getVel(hippTarget, hAngle);
                    }
                    else {
                        if (getDifference(hAngle, hippTarget) <= 180)
                            hipp.TargetVelocityRPM = multiplier * -getVel(hippTarget, hAngle);
                        else
                            hipp.TargetVelocityRPM = multiplier * getVel(hippTarget, hAngle);
                    }
                    if (hipL != null) hipL.Color = new Color(20, 0, 0);
                }
                else {
                    hipp.TargetVelocityRPM = 0;
                    hipp.RotorLock = true;
                    if (hipL != null) hipL.Color = new Color( 0,20, 0);
                }

                if (isVeryDifferent(kAngle, kneeTarget)) {
                    knee.RotorLock = false;
                    float multiplier = 1.5f;
                    if (kneeTarget > kAngle) {
                        if(getDifference(kAngle, kneeTarget)<=180)
                            knee.TargetVelocityRPM = multiplier *  getVel(kneeTarget, kAngle);
                        else
                            knee.TargetVelocityRPM = multiplier * -getVel(kneeTarget, kAngle);
                    }
                    else {
                        if (getDifference(kAngle, kneeTarget) <= 180)
                            knee.TargetVelocityRPM = multiplier * -getVel(kneeTarget, kAngle);
                        else
                            knee.TargetVelocityRPM = multiplier * getVel(kneeTarget, kAngle);
                    }
                    if (kneL != null) kneL.Color = new Color(20, 0, 0);
                }
                else {
                    knee.TargetVelocityRPM = 0;
                    knee.RotorLock = true;
                    if (kneL != null) kneL.Color = new Color( 0,20, 0);
                }

                if (isVeryDifferent(aAngle, anklTarget)) {
                    ankl.RotorLock = false;
                    float multiplier = 1f;
                    if (anklTarget > aAngle) {
                        if (getDifference(aAngle, anklTarget) <= 180)
                            ankl.TargetVelocityRPM = multiplier *  getVel(anklTarget, aAngle);
                        else
                            ankl.TargetVelocityRPM = multiplier * -getVel(anklTarget, aAngle);
                    }
                    else {
                        if (getDifference(aAngle, anklTarget) <= 180)
                            ankl.TargetVelocityRPM = multiplier * -getVel(anklTarget, aAngle);
                        else
                            ankl.TargetVelocityRPM = multiplier * getVel(anklTarget, aAngle);
                    }
                    if (ankL != null) ankL.Color = new Color(20, 0, 0);
                }
                else {
                    ankl.TargetVelocityRPM = 0;
                    ankl.RotorLock = true;
                    if (ankL != null) ankL.Color = new Color( 0,20, 0);
                }

                if (hipp.RotorLock && knee.RotorLock && ankl.RotorLock) {
                    this.hasWork = false;
                    if (legL != null) legL.Color = new Color( 0,20, 0);
                }
                else {
                    this.hasWork = true;
                    if (legL != null) legL.Color = new Color(20, 0, 0);
                }
            }

            bool isVeryDifferent(float current, float target) {
                if (getDifference(current, target) < precision) {
                    return false;
                }
                return true;
            }

            float getVel(float first, float second) {
                float angDif = getDifference(first, second);
                float vel = (angDif / 180) * 30;
                if (vel < minSpeed) vel = minSpeed;
                return vel;
            }

            public static float radToDeg(float rad) {return rad * 180f / (float)(Math.PI);}

            public static float degToRad(float deg) {return (float)(deg * Math.PI) / 180f;}

            float getDifference(float first, float second) {
                if (first == second) return 0f;
                float
                    smol = first > second ? second : first,
                    big = first < second ? second : first;
                return big - smol;
            }

            void lockGears(bool doLock) {
                foreach(IMyLandingGear gear in this.gears) {
                    /**/
                    if (doLock) 
                        gear.Lock();
                    else 
                        gear.Unlock();
                    /**/
                    gear.AutoLock = doLock;
                }
            }
        
            float getReadyGearsPercentage() {
                int readyCount = 0;
                List<IMyLandingGear> temp = new List<IMyLandingGear>();
                foreach (IMyLandingGear gear in gears) {
                    if (gear != null && gear.IsFunctional) {
                        temp.Add(gear);
                        if (gear.LockMode.Equals(LandingGearMode.ReadyToLock)) readyCount++;
                    }
                }
                this.gears.Clear();
                this.gears.AddList(temp);
                if (gears.Count <= 0) return 0f;

                return ((float)(readyCount) / (float)(this.gears.Count));
            }

        }
        /**/

        public bool isOnThisGrid(IMyCubeBlock block) {
            if (block != null && block.CubeGrid.Equals(Me.CubeGrid)) 
                return true;
            else 
                return false;
        }

        public Vector3D DirintToVec(int dirint) {
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


        public Vector3D CutVector(Vector3D vector) { return CutVector(vector, 3); }

        public Vector3D CutVector(Vector3D vector, int decNo) {
            double X = Math.Round(vector.X, decNo),
                Y = Math.Round(vector.Y, decNo),
                Z = Math.Round(vector.Z, decNo);

            return new Vector3D(X, Y, Z);
        }

        class NavPrompt {
            public int dirInt;
            public double vLength;

            public NavPrompt(int dir, Vector3D input) {
                this.dirInt = dir;
                this.vLength = input.Length();
            }
        }

        public Vector3D checkIfGrav() {
            Vector3D planet;
            bool gravMode;

            gravMode = SHIP_CONTROLLER.TryGetPlanetPosition(out planet);

            return gravMode ? planet : NOTHING;
        }

        public bool isAlmostSame(double d1, double d2) {
            if (d1 == d2) return true;
            double first = d1 > d2 ? d1 : d2,
                   second = d1 < d2 ? d1 : d2;

            if (first - second < (first / 10)) return true;
            else return false;
        }

        public List<IMyGyro> GetGyros() {
            List<IMyGyro> list = new List<IMyGyro>();
            List<IMyGyro> temp = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(temp);

            foreach (IMyGyro gyro in temp) if (isOnThisGrid(gyro)) list.Add(gyro);

            return list;
        }

        public void OverrideGyros(bool doThat) {
            foreach (IMyGyro gyro in GetGyros()) {
                gyro.GyroOverride = doThat;
            }
        }

        public Vector3D DirToCmd(int lndDir, int culprit) {
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


        public void Align() {
            Vector3D
                    planet = checkIfGrav(),
                    ship = SHIP_CONTROLLER.GetPosition(),
                    curr = NOTHING, command; 

            Vector3D algn = CutVector(Vector3D.Normalize(Vector3D.Subtract(planet, ship))),
                     alVec = Vector3D.Subtract(CutVector(DirintToVec(6)), algn);

            int culprit;

            List<NavPrompt> algPr = new List<NavPrompt>();
            for (int i = 1; i < 3; i++)
                algPr.Add(new NavPrompt(i, Vector3D.Subtract(CutVector(DirintToVec(i)), algn)));

            algPr = algPr.OrderBy(o => o.vLength).ToList();

            culprit = algPr[0].dirInt;

            command = DirToCmd(5, culprit);
            MoveAllGyros((float)(command.X * 10
                ), (float)(command.Y * 10
                ), (float)(command.Z * 10
                ));

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
                case VRageMath.Base6Directions.Direction.Forward:
                    return 1;
                case VRageMath.Base6Directions.Direction.Backward:
                    return 2;
                case VRageMath.Base6Directions.Direction.Left:
                    return 3;
                case VRageMath.Base6Directions.Direction.Right:
                    return 4;
                case VRageMath.Base6Directions.Direction.Up:
                    return 5;
                case VRageMath.Base6Directions.Direction.Down:
                    return 6;
                default:
                    Output("*ANGERY SIREN NOISES*");
                    return 44;
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

        public void MoveAllGyros(float Yaw, float Pitch, float Roll) {
            List<IMyGyro> gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
            foreach (IMyGyro gyro in gyros) {
                MoveGyroInAWay(gyro, Yaw, Pitch, Roll);
            }
        }

        public void MoveGyroInAWay(IMyGyro target, float Yaw, float Pitch, float Roll) {
            target.GyroOverride = true;
            switch (TranslateDirection(target)) {
                case 13:
                    target.Yaw = Roll;
                    target.Pitch = Yaw;
                    target.Roll = Pitch;
                    break;
                case 14:
                    target.Yaw = Roll;
                    target.Pitch = -Yaw;
                    target.Roll = -Pitch;
                    break;
                case 15:
                    target.Yaw = Roll;
                    target.Pitch = -Pitch;
                    target.Roll = Yaw;
                    break;
                case 16:
                    target.Yaw = Roll;
                    target.Pitch = Pitch;
                    target.Roll = -Yaw;
                    break;
                case 23:
                    target.Yaw = -Roll;
                    target.Pitch = -Yaw;
                    target.Roll = Pitch;
                    break;
                case 24:
                    target.Yaw = -Roll;
                    target.Pitch = Yaw;
                    target.Roll = -Pitch;
                    break;
                case 25:
                    target.Yaw = -Roll;
                    target.Pitch = Pitch;
                    target.Roll = Yaw;
                    break;
                case 26:
                    target.Yaw = -Roll;
                    target.Pitch = -Pitch;
                    target.Roll = -Yaw;
                    break;
                case 31:
                    target.Yaw = Pitch;
                    target.Pitch = -Yaw;
                    target.Roll = -Roll;
                    break;
                case 32:
                    target.Yaw = -Pitch;
                    target.Pitch = Yaw;
                    target.Roll = Roll;
                    break;
                case 35:
                    target.Yaw = -Pitch;
                    target.Pitch = -Roll;
                    target.Roll = Yaw;
                    break;
                case 36:
                    target.Yaw = -Pitch;
                    target.Pitch = Roll;
                    target.Roll = -Yaw;
                    break;
                case 41:
                    target.Yaw = Pitch;
                    target.Pitch = Yaw;
                    target.Roll = -Roll;
                    break;
                case 42:
                    target.Yaw = Pitch;
                    target.Pitch = Yaw;
                    target.Roll = Roll;
                    break;
                case 45:
                    target.Yaw = Pitch;
                    target.Pitch = Roll;
                    target.Roll = Yaw;
                    break;
                case 46:
                    target.Yaw = Pitch;
                    target.Pitch = -Roll;
                    target.Roll = -Yaw;
                    break;
                case 51:
                    target.Yaw = -Yaw;
                    target.Pitch = Pitch;
                    target.Roll = -Roll;
                    break;
                case 52:
                    target.Yaw = -Yaw;
                    target.Pitch = -Pitch;
                    target.Roll = Roll;
                    break;
                case 53:
                    target.Yaw = -Yaw;
                    target.Pitch = -Roll;
                    target.Roll = Pitch;
                    break;
                case 54:
                    target.Yaw = -Yaw;
                    target.Pitch = -Roll;
                    target.Roll = -Pitch;
                    break;
                case 61:
                    target.Yaw = Yaw;
                    target.Pitch = -Pitch;
                    target.Roll = -Roll;
                    break;
                case 62:
                    target.Yaw = Yaw;
                    target.Pitch = Pitch;
                    target.Roll = Roll;
                    break;
                case 63:
                    target.Yaw = Yaw;
                    target.Pitch = -Roll;
                    target.Roll = Pitch;
                    break;
                case 64:
                    target.Yaw = Yaw;
                    target.Pitch = -Roll;
                    target.Roll = -Pitch;
                    break;
                default:
                    Output("ERROR: " + target.CustomName + " GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
                    target.ShowOnHUD = true;
                    break;
            }
        }

        public void Output(object input) {
            string message;
            if (input is string) message = (string)input;
            else message = input.ToString();

            IMyTextSurface parostatek = GridTerminalSystem.GetBlockWithName("Mech Screen") as IMyTextSurface;
            if (parostatek != null) parostatek.WriteText("\n"+message);
        }

        Leg left = null, right = null;


        Vector2 init  = new Vector2(-55f, 110f),
                stdby = new Vector2(-90f, 145f);

        public void Main(string argument, UpdateType updateSource) {
            if (argument.Equals("")) {
                if ((updateSource & (UpdateType.Update10)) > 0) {
                    //if (left != null && left.hasWork) left.work();
                    //if (right != null && right.hasWork) right.work();
                }
                else
                if ((updateSource & (UpdateType.Update1)) > 0) {
                    Align();
                    if (left != null && left.hasWork) left.work();
                    if (right != null && right.hasWork) right.work();
                }
            }
            else {
                string bigBoi = argument.ToLower();
                string[] bois = bigBoi.Split(' ');

                float hTG, kTG;
                string output = "";
                switch (bois[0]) {
                    case "set":
                        /**/
                            hTG = bois.Length > 1 ? float.Parse(bois[1]) : 0f;
                            kTG = bois.Length > 2 ? float.Parse(bois[2]) : 0f;

                        

                        if (left != null) output += left .set(hTG, kTG);
                        if (right!= null) output += right.set(hTG, kTG);

                        Output(output);
                        /**/
                        break;

                    case "lft":
                    case "left":
                        /**/
                            hTG = bois.Length > 1 ? float.Parse(bois[1]) : 0f;
                            kTG = bois.Length > 2 ? float.Parse(bois[2]) : 0f;

                        if (left != null) output += left.set(hTG, kTG);

                        Output(output);
                        /**/
                        break;

                    case "rig":
                    case "right":
                        /**/
                            hTG = bois.Length > 1 ? float.Parse(bois[1]) : 0f;
                            kTG = bois.Length > 2 ? float.Parse(bois[2]) : 0f;
                        if (right != null) output += right.set(hTG, kTG);

                        Output(output);
                        /**/
                        break;

                    case "prof":
                    case "profile":
                        /**/
                        if (bois.Length > 2) {
                            string name = bois[1];
                            if (name.Equals("left") || name.Equals("lft")) {
                                output += left.setProfile(bois[2]);
                                Output(output);
                                return;
                            }
                            else
                            if (name.Equals("rig") || name.Equals("right")) {
                                output += right.setProfile(bois[2]);
                                Output(output);
                                return;
                            }
                        }
                        string prof = bois.Length > 1 ? bois[1] : "";
                        if(left!=null)  output+=left .setProfile(prof);
                        if(right!=null) output+=right.setProfile(prof);
                        Output(output);
                        /**/
                        break;

                    case "progress":
                        /**/
                        List<Vector2> list;
                        if (left != null) {
                            list = left.currProf.steps;
                            foreach (Vector2 vec in list) output += "( H:" + vec.X + " | K:" + vec.Y + " ) ";
                            output += "\n(" + left.currProf.progress + ") + "+left.currProf.steps[left.currProf.progress] + "\n\n";
                            left.moveWithProf();
                        }
                        if (right != null) {
                            list = right.currProf.steps;
                            foreach (Vector2 vec in list) output += "( H:" + vec.X + " | K:" + vec.Y + " ) ";
                            output += "\n\n";
                            right.moveWithProf();
                        }
                        Output(output);
                        /**/
                        break;

                    case "reset":
                        /**/
                        if (left != null)  left.set();
                        if (right != null) right.set();
                        /**/
                        break;

                    case "init":
                        /**/
                        setUp();
                        if (left != null)  output += left.setProfile("init");
                        if (right != null) output += right.setProfile("init");
                        Output(output);
                        /**/
                        break;

                    case "stdby":
                        /**/
                        if (left != null) output += left.setProfile("stdby");
                        if (right != null) output += right.setProfile("stdby");
                        Output(output);
                        /**/
                        break;
                }
            }
        }
    }
}
