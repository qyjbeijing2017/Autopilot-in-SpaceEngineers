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

        struct TrustCombine
        {
            public List<IMyThrust> Trusts;
            public float CombineTrusts;
            public Vector3 Direction;

            public float Trust
            {
                get
                {
                    float force = 0;
                    foreach (var trust in Trusts)
                    {
                        force += trust.CurrentThrust;
                    }
                    return force;
                }

                set
                {
                    var max = CombineTrusts;
                    var forcePercentage = value / max;
                    foreach (var trust in Trusts)
                    {
                        trust.ThrustOverridePercentage = forcePercentage;
                    }
                }
            }

            public float TrustPercentage
            {
                set
                {
                    foreach (var trust in Trusts)
                    {
                        trust.ThrustOverridePercentage = value;
                    }
                }
            }

            public static Vector3D TrustMaxDirection(TrustCombine a, TrustCombine b)
            {
                float lengthA = a.CombineTrusts;
                float lengthB = b.CombineTrusts;
                if (lengthA > lengthB)
                {
                    return a.Direction;
                }
                else if (lengthA < lengthB)
                {
                    return b.Direction;
                }
                else
                {
                    return new Vector3D();
                }
            }


        }

        class TrustController
        {
            List<IMyThrust> _trusts = new List<IMyThrust>();
            TrustCombine _trusUp;
            TrustCombine _trustsDown;
            TrustCombine _trustLeft;
            TrustCombine _trustRight;
            TrustCombine _trustForward;
            TrustCombine _trustBackward;
            TrustCombine _trustMax;
            public TrustController(Program program)
            {
                //  get thrusts
                program.GridTerminalSystem.GetBlocksOfType(_trusts);
                if (_trusts.Count <= 0)
                {
                    throw new Exception("Can not found the ship trust");
                }

                _trusUp = forceOnDirection(new Vector3(0, 1, 0));
                _trustsDown = forceOnDirection(new Vector3(0, -1, 0));
                _trustLeft = forceOnDirection(new Vector3(-1, 0, 0));
                _trustRight = forceOnDirection(new Vector3(1, 0, 0));
                _trustForward = forceOnDirection(new Vector3(0, 0, -1));
                _trustBackward = forceOnDirection(new Vector3(0, 0, 1));


                Vector3D forceY = TrustCombine.TrustMaxDirection(_trusUp, _trustsDown);
                Vector3D forceX = TrustCombine.TrustMaxDirection(_trustLeft, _trustRight);
                Vector3D forceZ = TrustCombine.TrustMaxDirection(_trustForward, _trustBackward);
                Vector3D forceMax = Vector3D.Add(forceX, Vector3D.Add(forceY, forceZ));

                _trustMax = forceOnDirection(forceMax);
            }

            TrustCombine forceOnDirection(Vector3 direction)
            {
                var combine = new TrustCombine();
                combine.Direction = direction;
                combine.Direction.Normalize();
                combine.Trusts = new List<IMyThrust>();
                foreach (var trust in _trusts)
                {
                    var forceCoefficient = Vector3.Dot(combine.Direction, trust.GridThrustDirection);
                    if (forceCoefficient > 0)
                    {
                        combine.CombineTrusts += forceCoefficient * trust.MaxThrust;
                        combine.Trusts.Add(trust);
                    }
                }
                return combine;
            }
        }

        public class ShipControl : Behavior
        {



            double _timestep;

            IMyShipController _cockpit = null;
            List<IMyGyro> _gyros = new List<IMyGyro>();
            List<IMyThrust> _trusts = new List<IMyThrust>();
            TrustCombine _trusUp;
            TrustCombine _trustsDown;
            TrustCombine _trustLeft;
            TrustCombine _trustRight;
            TrustCombine _trustForward;
            TrustCombine _trustBackward;

            TrustCombine _trustMax;

            private bool controlGyros
            {
                set
                {
                    foreach (var gyro in _gyros)
                    {
                        gyro.GyroOverride = value;
                    }
                }
            }

            public ShipControl(Program program, double TimeStep = 1.0 / 60.0) : base(program)
            {
                // create pid
                _timestep = TimeStep;

                // get ship controller
                List<IMyShipController> cockpits = new List<IMyShipController>();
                program.GridTerminalSystem.GetBlocksOfType(cockpits);
                foreach (var cockpit in cockpits)
                {
                    if (cockpit.CanControlShip)
                    {
                        _cockpit = cockpit;
                    }
                }
                if (_cockpit == null)
                {
                    throw new Exception("Can not found the ship controller");
                }

                // get the gyros
                program.GridTerminalSystem.GetBlocksOfType(_gyros);
                if (_gyros.Count <= 0)
                {
                    throw new Exception("Can not found the ship gyros");
                }

                //  get thrusts
                program.GridTerminalSystem.GetBlocksOfType(_trusts);
                if (_trusts.Count <= 0)
                {
                    throw new Exception("Can not found the ship trust");
                }

                _trusUp = forceOnDirection(new Vector3(0, 1, 0));
                _trustsDown = forceOnDirection(new Vector3(0, -1, 0));
                _trustLeft = forceOnDirection(new Vector3(-1, 0, 0));
                _trustRight = forceOnDirection(new Vector3(1, 0, 0));
                _trustForward = forceOnDirection(new Vector3(0, 0, -1));
                _trustBackward = forceOnDirection(new Vector3(0, 0, 1));


                Vector3D forceY = TrustCombine.TrustMaxDirection(_trusUp, _trustsDown);
                Vector3D forceX = TrustCombine.TrustMaxDirection(_trustLeft, _trustRight);
                Vector3D forceZ = TrustCombine.TrustMaxDirection(_trustForward, _trustBackward);
                Vector3D forceMax = Vector3D.Add(forceX, Vector3D.Add(forceY, forceZ));

                _trustMax = forceOnDirection(forceMax);
            }

        }
    }
}
