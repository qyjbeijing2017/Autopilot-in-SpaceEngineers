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

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        Autopilot autopilot;

        public Program()
        {
            autopilot = new Autopilot(this, 1.0 / 6);
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (isStart(updateSource))
            {
                Echo("AutoPilot");
                autopilot.Stop();
                string[] target = argument.Split(':');
                if (target.Length < 5)
                {
                    Echo("AutoPilot Stop!!!");
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    return;
                }
                double x, y, z;
                double.TryParse(target[2], out x);
                double.TryParse(target[3], out y);
                double.TryParse(target[4], out z);
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                autopilot.FastTravelTo(new Vector3D(x,y,z));
            }
            RunCoroutines(argument, updateSource);
        }
    }
}
