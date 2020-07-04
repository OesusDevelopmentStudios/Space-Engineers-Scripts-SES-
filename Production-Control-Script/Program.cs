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

        Dictionary<string, Component> dictionary;

        List<IMyAssembler> assemblers = new List<IMyAssembler>();
        int StateTim = 0;

        double accDev = 0.2d;

        State currentState;

        public enum State {
            INIT,
            WORK
        }

        void SayMyName(string ScriptName, float textSize = 2f) {
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface(0);
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText(ScriptName);
        }

        public string unpackList(List<object> list) {
            string output = "";
            foreach (object obj in list) {
                output += obj.ToString() + "\n";
            }
            return output;
        }

        public string unpackList(List<MyInventoryItem> list) {
            string output = "";
            foreach (MyInventoryItem obj in list) {
                output += obj.Amount + "x " + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + " [" + obj.ItemId + "]\n";
            }
            return output;
        }

        public string unpackList(List<MyProductionItem> list) {
            string output = "";
            foreach (MyProductionItem obj in list) {
                //output += obj.Amount + "x " + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + " [" + obj.ItemId + "]\n";
                output += obj.Amount + "x " + obj.BlueprintId.ToString().Substring(36) + "\n";
            }
            return output;
        }

        public string listToDictionary(List<MyInventoryItem> list) {
            string output = "";
            foreach (MyInventoryItem obj in list) {
                output += "dictionary.Add(\"" + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + "\", new Component());\n";
            }
            return output;
        }

        public void buildDictionary() {
            dictionary = new Dictionary<string, Component>();
            dictionary.Add("AmmoMagazine/Missile200mm", new Component());
            dictionary.Add("AmmoMagazine/NATO_25x184mm", new Component());
            dictionary.Add("AmmoMagazine/NATO_5p56x45mm", new Component());
            dictionary.Add("Component/BulletproofGlass", new Component());
            dictionary.Add("Component/Canvas", new Component());
            dictionary.Add("Component/Computer", new Component());
            dictionary.Add("Component/Construction", new Component());
            dictionary.Add("Component/Detector", new Component());
            dictionary.Add("Component/Display", new Component());
            dictionary.Add("Component/Explosives", new Component());
            dictionary.Add("Component/Girder", new Component());
            dictionary.Add("Component/GravityGenerator", new Component());
            dictionary.Add("Component/InteriorPlate", new Component());
            dictionary.Add("Component/LargeTube", new Component());
            dictionary.Add("Component/Medical", new Component());
            dictionary.Add("Component/MetalGrid", new Component());
            dictionary.Add("Component/Motor", new Component());
            dictionary.Add("Component/PowerCell", new Component());
            dictionary.Add("Component/RadioCommunication", new Component());
            dictionary.Add("Component/Reactor", new Component());
            dictionary.Add("Component/SmallTube", new Component());
            dictionary.Add("Component/SolarCell", new Component());
            dictionary.Add("Component/SteelPlate", new Component());
            dictionary.Add("Component/Superconductor", new Component());
            dictionary.Add("Component/Thrust", new Component());
            dictionary.Add("Datapad/Datapad", new Component());
            dictionary.Add("GasContainerObject/HydrogenBottle", new Component());
            dictionary.Add("OxygenContainerObject/OxygenBottle", new Component());
            dictionary.Add("PhysicalGunObject/AngleGrinderItem", new Component());
            dictionary.Add("PhysicalGunObject/AngleGrinder2Item", new Component());
            dictionary.Add("PhysicalGunObject/AngleGrinder3Item", new Component());
            dictionary.Add("PhysicalGunObject/AngleGrinder4Item", new Component());
            dictionary.Add("PhysicalGunObject/AutomaticRifleItem", new Component());
            dictionary.Add("PhysicalGunObject/HandDrillItem", new Component());
            dictionary.Add("PhysicalGunObject/HandDrill2Item", new Component());
            dictionary.Add("PhysicalGunObject/HandDrill3Item", new Component());
            dictionary.Add("PhysicalGunObject/HandDrill4Item", new Component());
            dictionary.Add("PhysicalGunObject/PreciseAutomaticRifleItem", new Component());
            dictionary.Add("PhysicalGunObject/RapidFireAutomaticRifleItem", new Component());
            dictionary.Add("PhysicalGunObject/UltimateAutomaticRifleItem", new Component());
            dictionary.Add("PhysicalGunObject/WelderItem", new Component());
            dictionary.Add("PhysicalGunObject/Welder2Item", new Component());
            dictionary.Add("PhysicalGunObject/Welder3Item", new Component());
            dictionary.Add("PhysicalGunObject/Welder4Item", new Component());
        }

        public Program() {
            //Runtime.UpdateFrequency = UpdateFrequency.Update10;
            //buildDictionary();
            changeState(State.INIT);
            SayMyName("PROD CTRL\n SCRIPT",2f);
        }

        public class Component {

            public Component() {
                MyFixedPoint amount = 10;
            }

        }

        public void changeState(State state) {
            StateTim = 0;
            switch (state) {
                case State.INIT:
                    currentState = State.INIT; 
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;

                case State.WORK:
                    currentState = State.WORK;
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;
            }
        }

        public void executeStateWork() {
            switch (currentState) {
                case State.INIT:
                    switch (StateTim++) {
                        case 0: findAssemblers(); break;
                        case 1: changeState(State.WORK);  break;
                    }
                    break;

                case State.WORK:
                    switch (StateTim++) {
                        case  2:
                            distributeWork();
                            break;
                        case  5:
                            distributeWork();
                            break;
                        case  8:
                            distributeWork();
                            break;
                        case 11:
                            distributeWork();
                            StateTim = 0;
                            break;
                    }
                    break;
            }
        }

        public List<MyProductionItem> glueQuota(List<MyProductionItem> input) {
            Dictionary<MyDefinitionId, MyFixedPoint> gluer = new Dictionary<MyDefinitionId, MyFixedPoint>();
            MyFixedPoint temp;
            foreach(MyProductionItem subQuota in input) {
                if (gluer.TryGetValue(subQuota.BlueprintId, out temp)) {
                    gluer.Remove(subQuota.BlueprintId);
                    gluer.Add(subQuota.BlueprintId, temp + subQuota.Amount);
                }
                else gluer.Add(subQuota.BlueprintId, subQuota.Amount);
            }
            List<MyProductionItem> output = new List<MyProductionItem>();
            foreach(MyDefinitionId key in gluer.Keys) {if(gluer.TryGetValue(key, out temp)) output.Add(new MyProductionItem(0, key, temp));}

            return output;
        }

        public void divideWork(List<MyProductionItem> quota, List<IMyAssembler> workers) {
            int workersNo = workers.Count;
            quota = glueQuota(quota);
            Echo("Workers Count: " + workersNo);
            foreach (IMyAssembler worker in workers) {
                worker.ClearQueue();
            }
            foreach (MyProductionItem subQuota in quota) {
                MyFixedPoint amount = (int)subQuota.Amount / workersNo;
                int residue = (int)subQuota.Amount % workersNo;
                Echo("\nSubQuota: " + subQuota.Amount + "x " + subQuota.BlueprintId);
                for (int i = 0; i < workersNo; i++) {
                    MyFixedPoint addition = residue > i ? 1 : 0;
                    if (addition == 0 && amount == 0) break;
                    workers[i].AddQueueItem(subQuota.BlueprintId, amount+addition);
                }
            }
        }

        public MyFixedPoint getAmount(List<MyProductionItem> list) {
            MyFixedPoint output = 0;
            foreach(MyProductionItem quota in list) {output += quota.Amount;}
            return output;
        }

        public MyFixedPoint difference(MyFixedPoint A, MyFixedPoint B) {
            if (A == B) return 0;
            MyFixedPoint 
                min = A > B ? B : A,
                max = A < B ? B : A;

            return max - min;
        }


        public void distributeWork() {
            //int avAss = 0;
            List<MyProductionItem>
                input   = new List<MyProductionItem>(),
                summary = new List<MyProductionItem>();

            List<IMyAssembler> 
                workable= new List<IMyAssembler>();

            List<MyFixedPoint>
                amounts = new List<MyFixedPoint>();

            foreach(IMyAssembler ass in assemblers) {
                if(ass.Mode == MyAssemblerMode.Assembly) {
                    input.Clear();
                    ass.GetQueue(input);
                    summary.AddList(input);
                    if (ass.IsWorking) {
                        amounts.Add(getAmount(input));
                        workable.Add(ass);
                    }
                    else ass.ClearQueue();
                }
            }
            bool isFine = true;
            MyFixedPoint median = (int)getAmount(summary) / workable.Count;

            foreach(MyFixedPoint amount in amounts) {
                if (difference(amount, median) > 100 && ((double)amount > (double)median * (1 + accDev) || (double)amount < (double)median * (1- accDev))) isFine = false;
            }

            if (!isFine) divideWork(summary, workable);
        }

        public bool isOnThisGrid(IMyCubeBlock block) {
            if (Me.CubeGrid.Equals(block.CubeGrid)) return true;
            return false;
        }

        public void findAssemblers(){
            List<IMyAssembler> 
                temp    = new List<IMyAssembler>(), 
                output  = new List<IMyAssembler>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyAssembler ass in temp) { if (isOnThisGrid(ass) && !ass.BlockDefinition.SubtypeName.Contains("SurvivalKit")) output.Add(ass); }

            assemblers = new List<IMyAssembler>();
            assemblers.AddList(output);
        }

        public void output(object input) {
            string output = input is string ? (string)input : input.ToString();
            IMyTextSurface surf = GridTerminalSystem.GetBlockWithName("OutputTest") as IMyTextSurface;
            if (surf == null) return;
            surf.WriteText(output,false);
        }

        public void Main(string argument, UpdateType updateSource) {
            if((updateSource & UpdateType.Update10)>0) executeStateWork();
            else {
                if (argument.ToLower().Equals("clear")) {
                    divideWork(new List<MyProductionItem>(), assemblers);
                }
            }
        }
    }
}
