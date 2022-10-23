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
        public class Autopilot : Behavior
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

            Coroutine coroutine;
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

            public Autopilot(Program program, double TimeStep = 1.0 / 60.0) : base(program)
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

            private TrustCombine forceOnDirection(Vector3 direction)
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

            IEnumerator RotateUntilArrive(Matrix3x3 taget)
            {
                var pid = new PID(1.0, 0.0, 0.0, _timestep);
                float speed = 0.0f;
                var eular = new Vector3();
                var worldMatInvert = new Matrix3x3();
                var rotationCurrent = new Matrix3x3();
                do
                {
                    var worldMat = _cockpit.WorldMatrix.Rotation;
                    Matrix3x3.Invert(ref worldMat, out worldMatInvert);
                    Matrix3x3.Multiply(ref taget, ref worldMatInvert, out rotationCurrent);
                    Matrix3x3.GetEulerAnglesXYZ(ref rotationCurrent, out eular);
                    float error = eular.Normalize();
                    speed = (float)pid.Control(error);
                    SetAngleSpeed(eular * speed);
                    yield return null;

                } while (speed > 0.005);
            }

            IEnumerator LookAtUntilAttive(Vector3 fromOC, Vector3 tagetWC)
            {
                var pid = new PID(1.0, 0.0, 0.0, _timestep);
                var rotation = new Matrix3x3();
                var eular = new Vector3();
                float speed = 100000;

                while (speed > 0.005)
                {
                    var lookat = MatrixD.CreateLookAt(Vector3D.Zero, _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
                    var tagetOC3D = Vector3D.Transform(tagetWC, lookat);
                    var tagetOC3 = new Vector3(tagetOC3D.X, tagetOC3D.Y, tagetOC3D.Z);
                    Matrix3x3.CreateRotationFromTwoVectors(ref fromOC, ref tagetOC3, out rotation);
                    Matrix3x3.GetEulerAnglesXYZ(ref rotation, out eular);

                    float error = eular.Normalize();
                    speed = (float)pid.Control(error);
                    SetAngleSpeed(eular * speed);
                    yield return null;
                }
                controlGyros = false;
            }

            Coroutine LookAt(Vector3 fromOC, Vector3 tagetWC)
            {
                Stop();
                coroutine = program.startCoroutine(LookAtUntilAttive(fromOC, tagetWC));
                return this.coroutine;
            }

            IEnumerator FastUntilAttive(Vector3D target)
            {
                var pid = new PID(0.4, 0.0, 0.0, _timestep);

                var distance = new Vector3D();
                var rotation = new Matrix3x3();
                var eular = new Vector3();
                var forceDir = -_trustMax.Direction;
                do
                {
                    var targetPos = _cockpit.WorldMatrix.Translation;
                    Vector3D.Subtract(ref target, ref targetPos, out distance);
                    var velocities = _cockpit.GetShipVelocities();
                    var speed = pid.Control(distance.Length());
                    var a = (Vector3D.Normalize(distance) * speed - velocities.LinearVelocity) / _timestep;

                    // 限定a值在发动机力所能及的范围内，避免过度转向的问题
                    var gravityForce = _cockpit.GetNaturalGravity() * _cockpit.CalculateShipMass().TotalMass;
                    var exceptedA = a.Normalize();
                    var gForce = gravityForce.Normalize();
                    var sin = Vector3D.Cross(gravityForce, a).Length();
                    var cos = Vector3D.Dot(gravityForce, a);
                    var maxForce = gForce * cos + Math.Sqrt(Math.Pow(_trustMax.CombineTrusts, 2) - Math.Pow(gForce * sin, 2));
                    var aMax = maxForce / _cockpit.CalculateShipMass().TotalMass;
                    a = Math.Min(aMax, exceptedA) * a;

                    var forceWC = (a - _cockpit.GetNaturalGravity()) * _cockpit.CalculateShipMass().TotalMass;
                    var world2Ship = MatrixD.CreateLookAt(Vector3D.Zero, _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
                    var forceSC = Vector3D.Transform(forceWC, world2Ship);

                    SetForce(forceSC);

                    var lookat = MatrixD.CreateLookAt(Vector3D.Zero, _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
                    var tagetOC3D = Vector3D.Transform(forceWC, lookat);
                    var tagetOC3 = new Vector3(tagetOC3D.X, tagetOC3D.Y, tagetOC3D.Z);
                    Matrix3x3.CreateRotationFromTwoVectors(ref forceDir, ref tagetOC3, out rotation);
                    Matrix3x3.GetEulerAnglesXYZ(ref rotation, out eular);

                    //float error = eular.Normalize();
                    //angleSpeed = (float)pid.Control(error);
                    SetAngleSpeed(eular);
                    yield return null;
                }
                while (distance.Length() > 1 || _cockpit.GetShipVelocities().LinearVelocity.Length() > 0.05);
                SetForce(new Vector3D(0, 0, 0));
                controlGyros = false;
            }

            public Coroutine FastTravelTo(Vector3D target)
            {
                Stop();
                coroutine = program.startCoroutine(FastUntilAttive(target));
                return this.coroutine;
            }

            private double Fly(ref Vector3D target, PID pid)
            {
                var positionError = target - _cockpit.WorldMatrix.Translation;
                var velocities = _cockpit.GetShipVelocities();
                var nextVecities = new Vector3D(pid.Control(positionError.X), pid.Control(positionError.Y), pid.Control(positionError.Z)) - velocities.LinearVelocity;
                var forceWC = nextVecities / _timestep - _cockpit.GetNaturalGravity() * _cockpit.CalculateShipMass().TotalMass;
                var world2Ship = MatrixD.CreateLookAt(Vector3D.Zero, _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
                var forceSC = Vector3D.Transform(forceWC, world2Ship);
                SetForce(forceSC);
                return nextVecities.Length();
            }

            void SetForce(Vector3D forceSC)
            {
                if (forceSC.Length() <= 0)
                {
                    _trusUp.TrustPercentage = 0;
                    _trustsDown.TrustPercentage = 0;
                    _trustLeft.TrustPercentage = 0;
                    _trustRight.TrustPercentage = 0;
                    _trustForward.TrustPercentage = 0;
                    _trustBackward.TrustPercentage = 0;
                }
                _trusUp.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trusUp.Direction));
                _trustsDown.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trustsDown.Direction));
                _trustLeft.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trustLeft.Direction));
                _trustRight.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trustRight.Direction));
                _trustForward.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trustForward.Direction));
                _trustBackward.Trust = (float)Math.Max(0, Vector3D.Dot(-forceSC, _trustBackward.Direction));
            }

            void SetAngleSpeed(Vector3 eularSC)
            {
                if (eularSC.Length() <= 0)
                {
                    controlGyros = false;
                    return;
                }
                controlGyros = true;
                foreach (var gyro in _gyros)
                {
                    gyro.GyroOverride = true;
                    gyro.Pitch = eularSC.X;
                    gyro.Yaw = eularSC.Y;
                    gyro.Roll = eularSC.Z;
                }
            }

            public Coroutine Living()
            {
                this.coroutine = program.startCoroutine(LookAtUntilAttive(Vector3.Up, -_cockpit.GetNaturalGravity()));
                return this.coroutine;
            }

            public Matrix3x3 LookAt2Target(Vector3 fromOC, Vector3 tagetWC)
            {
                var fromWC3D = Vector3D.Rotate(fromOC, _cockpit.WorldMatrix);
                var fromWC = new Vector3((float)fromWC3D.X, (float)fromWC3D.Y, (float)fromWC3D.Z);
                var rotateMatrix = new Matrix3x3();
                Matrix3x3.CreateRotationFromTwoVectors(ref fromWC, ref tagetWC, out rotateMatrix);
                var tagetRotation = new Matrix3x3();
                var currentRotation = _cockpit.WorldMatrix.Rotation;
                Matrix3x3.Multiply(ref rotateMatrix, ref currentRotation, out tagetRotation);
                return tagetRotation;
            }

            public bool isRunning { get { return coroutine != null && coroutine.IsRunning; } }
            public void Stop()
            {
                if (isRunning)
                    coroutine.Stop();
                SetForce(new Vector3D(0, 0, 0));
                controlGyros = false;
            }

            PID _pidRotation;
            PID _pidControl;
            PID _pidYContril;
            protected override void Start()
            {
                _pidRotation = new PID(1.0, 0.0, 0.0, _timestep);
                _pidControl = new PID(0.4, 0, 0.0, _timestep);
                _pidYContril = new PID(1.0, 0.0, 0.0, _timestep);
            }

            protected override void Update()
            {

                var worldUp = -_cockpit.GetNaturalGravity();
                worldUp.Normalize();
                var worldLeft = Vector3D.Cross(worldUp, _cockpit.WorldMatrix.Forward);
                var worldForward = Vector3D.Cross(worldLeft, worldUp);
                var world2Local = MatrixD.CreateLookAt(Vector3D.Zero, worldForward, worldUp);
                var local2World = MatrixD.Invert(world2Local);

                var moveInd = _cockpit.MoveIndicator;

                if (moveInd.Length() <= 0.03)
                {
                    moveInd = -Vector3.Transform(_cockpit.GetShipVelocities().LinearVelocity, world2Local);
                    moveInd.Y = 0;
                    moveInd = moveInd * (float)_pidControl.Control(moveInd.Length()) / 20.0f;
                }

                moveInd.Y = 2;
                moveInd.Normalize();


                var speedY = _cockpit.GetShipVelocities().LinearVelocity.Dot(worldUp);

                //// 转到飞船坐标
                var world2Ship = MatrixD.CreateLookAt(new Vector3(), _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
                var ship2World = MatrixD.Invert(world2Ship);

                var speedYError = _cockpit.MoveIndicator.Y * 100 - speedY;

                var aY = _pidYContril.Control(speedYError) / _timestep;

                var s = Vector3.Transform(_trustMax.Direction, ship2World).Dot(-worldUp);

                var force = (_cockpit.GetNaturalGravity().Length() + aY) * _cockpit.CalculateShipMass().TotalMass / s;

                _trustMax.Trust = (float)force;




                if (_cockpit.RotationIndicator.Length() >= 0.03)
                {
                    SetAngleSpeed(Vector3D.Zero);
                    return;
                }
                //var targetUp = -_cockpit.GetNaturalGravity();
                var targetUp = Vector3D.Transform(moveInd, local2World);
                if (targetUp.Length() <= 0.003)
                {
                    SetAngleSpeed(Vector3D.Zero);
                    return;
                }
                targetUp.Normalize();



                Vector3D gravityShip = Vector3D.TransformNormal(targetUp, world2Ship);

                // 获得目标向量
                var target = new Vector3(gravityShip.X, gravityShip.Y, gravityShip.Z);
                var up = -_trustMax.Direction;
                // 获得旋转矩阵
                var rotationMat = new Matrix3x3();
                Matrix3x3.CreateRotationFromTwoVectors(ref up, ref target, out rotationMat);
                // 获得欧拉角变化值
                var eular = new Vector3();
                Matrix3x3.GetEulerAnglesXYZ(ref rotationMat, out eular);

                // 获得需要旋转的角度, 并且重置方向
                float error = eular.Normalize();

                float speed = (float)_pidRotation.Control(error);

                if (speed <= 0.005)
                {
                    SetAngleSpeed(Vector3D.Zero);
                    return;
                }


                var currentEular = new Vector3(eular.X * speed, eular.Y * speed, eular.Z * speed);


                SetAngleSpeed(currentEular);

            }
        }
    }
}
