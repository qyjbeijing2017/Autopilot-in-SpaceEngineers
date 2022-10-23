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
    partial class Program
    {
        public class Behavior
        {
            private Program _program;
            public Program program { get { return _program; } }
            private Coroutine coroutine;

            public bool enable
            {
                get { return coroutine.IsRunning; }
                set
                {
                    if (value && !coroutine.IsRunning)
                    {
                        coroutine = this.program.startCoroutine(Exec());
                        Enable(value);
                    }
                    else if (!value && coroutine.IsRunning)
                    {
                        coroutine.Stop();
                        Enable(value);
                    }
                }
            }

            virtual public void Enable(bool val)
            {

            }

            public Behavior(Program program)
            {
                this._program = program;
                coroutine = this.program.startCoroutine(Exec());
            }

            private IEnumerator Exec()
            {
                Start();
                while (true)
                {
                    Update();
                    yield return null;
                }
            }

            protected virtual void Start() { }
            protected virtual void Update() { }

        }
    }
}
