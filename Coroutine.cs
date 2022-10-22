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
        List<IEnumerator> enumerators = new List<IEnumerator>();


        System.DateTime _lastTime = System.DateTime.UtcNow;
        System.DateTime _nowTime = System.DateTime.UtcNow;

        double deltaTime { get { return (_nowTime - _lastTime).Milliseconds / 1000f; } }

        bool isStart(UpdateType updateSource)
        {
            return (updateSource & (UpdateType.Mod | UpdateType.IGC | UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) != 0;
        }

        // 异步运行多个协程
        void RunCoroutines(string argument, UpdateType updateSource)
        {
            _nowTime = System.DateTime.UtcNow;
            if (isStart(updateSource))
            {
                _lastTime = _nowTime;
            }

            List<IEnumerator> inEnumerators = new List<IEnumerator>(enumerators);
            List<IEnumerator> outEnumerators = new List<IEnumerator>();
            foreach (var enumerator in inEnumerators)
            {
                if (enumerator.MoveNext())
                {
                    outEnumerators.Add(enumerator);
                }
            }
            enumerators = outEnumerators;
            _lastTime = _nowTime;
        }

        // 协程BaseClass用于组织嵌套协程
        public class Coroutine : IEnumerable
        {
            IEnumerator enumerator;
            Program program;
            bool isRunning;
            public bool IsRunning
            {
                get { return isRunning; }
            }
            public Coroutine(Program program, IEnumerator enumerator = null)
            {
                this.program = program;
                this.enumerator = enumerator;
            }

            // 嵌套协程同步运行
            virtual public IEnumerator GetEnumerator()
            {
                isRunning = true;
                while (enumerator != null && enumerator.MoveNext())
                {
                    if (enumerator.Current is Coroutine)
                    {
                        var corutine = enumerator.Current as Coroutine;
                        var enumer = corutine.GetEnumerator();
                        while (enumerator != null && enumer.MoveNext())
                        {
                            yield return enumer.Current;
                        }
                    }
                    else
                    {
                        yield return enumerator.Current;
                    }
                }
                isRunning = false;
            }

            // 用于停止当前协程
            public void Stop()
            {
                this.enumerator = null;
            }
            public static IEnumerator All(params Coroutine[] coroutines)
            {
                List<IEnumerator> inEnumerators = new List<IEnumerator>();
                foreach (var coroutine in coroutines)
                {
                    inEnumerators.Add(coroutine.GetEnumerator());
                }
                while (inEnumerators.Count > 0)
                {
                    List<IEnumerator> outEnumerators = new List<IEnumerator>();
                    foreach (var enumerator in inEnumerators)
                    {
                        if (enumerator.MoveNext())
                        {
                            outEnumerators.Add(enumerator);
                        }
                    }
                    yield return null;
                    inEnumerators = outEnumerators;
                }
            }
        }

        Coroutine startCoroutine(IEnumerator enumerator)
        {
            var corutine = new Coroutine(this, enumerator);
            enumerators.Add(corutine.GetEnumerator());
            return corutine;
        }

        void stopAllCoroutines()
        {
            enumerators.Clear();
        }
    }
}
