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
        public class Helicopter : Behavior
        {
            double _timestep;

            IMyShipController _cockpit = null;
            public Helicopter(Program program, double TimeStep = 1.0 / 60.0) : base(program)
            {
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
