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

namespace IngameScript{
    partial class Program : MyGridProgram{

        public Program(){
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        string Format(double input, int afterPoint = 1)
        {
            string addition = "";

            for (int i = 0; i < afterPoint; i++) addition += "#";

            return string.Format("{0:0." + addition + "}", input);
        }

        string Format(float input, int afterPoint = 1)
        {
            string addition = "";

            for (int i = 0; i < afterPoint; i++) addition += "#";

            return string.Format("{0:0." + addition + "}", input);
        }

        string Format(Vector3D input) {
            return "(" + Format(input.X) + "," + Format(input.Y) + "," + Format(input.Z) + ")";
        }

        Vector3D GetCenterOfMass() {
            List<IMyCubeBlock> cubes = new List<IMyCubeBlock>();
            GridTerminalSystem.GetBlocksOfType(cubes);

            Echo(cubes.Count.ToString());

            double      finalMass = 0d;
            Vector3D    finalVec = new Vector3D(0, 0, 0);
            
            foreach(IMyCubeBlock cube in cubes) {
                finalMass += cube.Mass;
                Vector3D.Add(finalVec,Vector3D.Multiply(cube.GetPosition(),cube.Mass));
            }

            return Vector3D.Multiply(finalVec, (1d / finalMass));
        }

        IMyTextPanel panel;

        public void Main() {
            if (panel == null) {
                panel = GridTerminalSystem.GetBlockWithName("Text Panel") as IMyTextPanel;
            }
            if (panel != null) {
                panel.WriteText(Format(Me.CubeGrid.GetPosition()));
                panel.WriteText("\n"+Format(GetCenterOfMass()),true);
            }
        }
    }
}
