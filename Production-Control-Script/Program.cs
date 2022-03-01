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

        //Dictionary<string, Component> dictionary;

        List<IMyAssembler> assemblers = new List<IMyAssembler>();
        int StateTim = 0;
        readonly double acceptableDeviation = 0.2d;

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

        public string UnpackList(List<object> list) {
            string output = "";
            foreach (object obj in list) {
                output += obj.ToString() + "\n";
            }
            return output;
        }

        public string UnpackList(List<MyInventoryItem> list) {
            string output = "";
            foreach (MyInventoryItem obj in list) {
                output += obj.Amount + "x " + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + " [" + obj.ItemId + "]\n";
            }
            return output;
        }

        public string UnpackList(List<MyProductionItem> list) {
            string output = "";
            foreach (MyProductionItem obj in list) {
                //output += obj.Amount + "x " + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + " [" + obj.ItemId + "]\n";
                output += obj.Amount + "x " + obj.BlueprintId.ToString().Substring(36) + "\n";
            }
            return output;
        }

        public string ListToDictionary(List<MyInventoryItem> list) {
            string output = "";
            foreach (MyInventoryItem obj in list) {
                output += "dictionary.Add(\"" + obj.Type.TypeId.Substring(16) + "/" + obj.Type.SubtypeId + "\", new Component());\n";
            }
            return output;
        }
        /*/
        public void BuildDictionary() {
            dictionary = new Dictionary<string, Component>
            {
                { "AmmoMagazine/Missile200mm", new Component( ) },
                { "AmmoMagazine/NATO_25x184mm", new Component( ) },
                { "AmmoMagazine/NATO_5p56x45mm", new Component( ) },
                { "Component/BulletproofGlass", new Component( ) },
                { "Component/Canvas", new Component( ) },
                { "Component/Computer", new Component( ) },
                { "Component/Construction", new Component( ) },
                { "Component/Detector", new Component( ) },
                { "Component/Display", new Component( ) },
                { "Component/Explosives", new Component( ) },
                { "Component/Girder", new Component( ) },
                { "Component/GravityGenerator", new Component( ) },
                { "Component/InteriorPlate", new Component( ) },
                { "Component/LargeTube", new Component( ) },
                { "Component/Medical", new Component( ) },
                { "Component/MetalGrid", new Component( ) },
                { "Component/Motor", new Component( ) },
                { "Component/PowerCell", new Component( ) },
                { "Component/RadioCommunication", new Component( ) },
                { "Component/Reactor", new Component( ) },
                { "Component/SmallTube", new Component( ) },
                { "Component/SolarCell", new Component( ) },
                { "Component/SteelPlate", new Component( ) },
                { "Component/Superconductor", new Component( ) },
                { "Component/Thrust", new Component( ) },
                { "Datapad/Datapad", new Component( ) },
                { "GasContainerObject/HydrogenBottle", new Component( ) },
                { "OxygenContainerObject/OxygenBottle", new Component( ) },
                { "PhysicalGunObject/AngleGrinderItem", new Component( ) },
                { "PhysicalGunObject/AngleGrinder2Item", new Component( ) },
                { "PhysicalGunObject/AngleGrinder3Item", new Component( ) },
                { "PhysicalGunObject/AngleGrinder4Item", new Component( ) },
                { "PhysicalGunObject/AutomaticRifleItem", new Component( ) },
                { "PhysicalGunObject/HandDrillItem", new Component( ) },
                { "PhysicalGunObject/HandDrill2Item", new Component( ) },
                { "PhysicalGunObject/HandDrill3Item", new Component( ) },
                { "PhysicalGunObject/HandDrill4Item", new Component( ) },
                { "PhysicalGunObject/PreciseAutomaticRifleItem", new Component( ) },
                { "PhysicalGunObject/RapidFireAutomaticRifleItem", new Component( ) },
                { "PhysicalGunObject/UltimateAutomaticRifleItem", new Component( ) },
                { "PhysicalGunObject/WelderItem", new Component( ) },
                { "PhysicalGunObject/Welder2Item", new Component( ) },
                { "PhysicalGunObject/Welder3Item", new Component( ) },
                { "PhysicalGunObject/Welder4Item", new Component( ) }
            };
        }
        /**/

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            //buildDictionary();
            ChangeState(State.INIT);
            SayMyName("PROD CTRL\n SCRIPT",2f);
        }

        public class Component {

            public Component() {
                MyFixedPoint amount = 10;
            }

        }

        public void ChangeState(State state) {
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

        public void ExecuteStateWork() {
            switch (currentState) {
                case State.INIT:
                    switch (StateTim++) {
                        case 0: FindAssemblers(); break;
                        case 1: if (assemblers.Count>0) ChangeState(State.WORK); else Runtime.UpdateFrequency = UpdateFrequency.None;  break;
                    }
                    break;

                case State.WORK:
                    switch (StateTim++) {
                        case  2:
                            DistributeWork();
                            break;
                        case  5:
                            DistributeWork();
                            break;
                        case  8:
                            DistributeWork();
                            break;
                        case 11:
                            DistributeWork();
                            StateTim = 0;
                            break;
                    }
                    break;
            }
        }

        public List<MyProductionItem> GlueQuota(List<MyProductionItem> input) {
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

        public void DivideWork(List<MyProductionItem> quota, List<IMyAssembler> workers) {
            int workersNo = workers.Count;
            quota = GlueQuota(quota);
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

        public MyFixedPoint GetAmount(List<MyProductionItem> list) {
            MyFixedPoint output = 0;
            foreach(MyProductionItem quota in list) {output += quota.Amount;}
            return output;
        }

        public MyFixedPoint GetDifferenceBetween(MyFixedPoint A, MyFixedPoint B) {
            if (A == B) return 0;
            MyFixedPoint 
                min = A > B ? B : A,
                max = A < B ? B : A;

            return max - min;
        }


        public void DistributeWork() {
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
                        amounts.Add(GetAmount(input));
                        workable.Add(ass);
                    }
                    else ass.ClearQueue();
                }
            }
            bool isFine = true;
            MyFixedPoint median = (int)GetAmount(summary) / workable.Count;

            foreach(MyFixedPoint amount in amounts) {
                if (GetDifferenceBetween(amount, median) > 100 && ((double)amount > (double)median * (1 + acceptableDeviation) || (double)amount < (double)median * (1- acceptableDeviation))) isFine = false;
            }

            if (!isFine) DivideWork(summary, workable);
        }

        public bool IsOnThisGrid(IMyCubeBlock block) {
            if (Me.CubeGrid.Equals(block.CubeGrid)) return true;
            return false;
        }

        public void FindAssemblers(){
            List<IMyAssembler> 
                temp    = new List<IMyAssembler>(), 
                output  = new List<IMyAssembler>();

            GridTerminalSystem.GetBlocksOfType(temp);
            foreach (IMyAssembler ass in temp) { if (IsOnThisGrid(ass) && !ass.BlockDefinition.SubtypeName.Contains("SurvivalKit")) output.Add(ass); }

            assemblers = new List<IMyAssembler>();
            assemblers.AddList(output);
        }

        public void Output(object input) {
            string output = input is string ? (string)input : input.ToString();
            IMyTextSurface surf = GridTerminalSystem.GetBlockWithName("OutputTest") as IMyTextSurface;
            if (surf == null) return;
            surf.WriteText(output,false);
        }

        public void Main(string argument, UpdateType updateSource) {
            if((updateSource & UpdateType.Update10)>0) ExecuteStateWork();
            else {
                if (argument.ToLower().Equals("clear")) {
                    DivideWork(new List<MyProductionItem>(), assemblers);
                }
            }
        }
    }
}
