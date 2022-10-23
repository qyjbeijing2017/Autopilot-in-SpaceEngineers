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
        public class Liviling : Behavior
        {
            PID _pid;
            IMyShipController _cockpit = null;
            List<IMyGyro> gyros = new List<IMyGyro>();

            private bool control
            {
                set
                {
                    foreach (var gyro in gyros)
                    {
                        gyro.GyroOverride = value;
                    }
                }
            }

            public override void Enable(bool val)
            {
                if (!val)
                {
                    control = false;
                }
            }

            public Liviling(Program program, double TimeStep = 1.0 / 60.0) : base(program)
            {
                _pid = new PID(2.0, 0.0, 0.0, TimeStep);
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
                    program.Echo("Can not found the ship controller");
                }

                program.GridTerminalSystem.GetBlocksOfType(gyros);

                if (gyros.Count <= 0)
                {
                    program.Echo("Can not found the ship gyros");
                }
            }

            protected override void Update()
            {
                if (Math.Abs(_cockpit.RollIndicator) >= 0.03 || _cockpit.RotationIndicator.Length() >= 0.03)
                {
                    control = false;
                    return;
                }
                var gravityd = _cockpit.GetNaturalGravity();
                if (gravityd.Length() <= 0.003)
                {
                    control = false;
                    return;
                }

                // 转到飞船坐标
                var world2Ship = MatrixD.CreateLookAt(new Vector3(), _cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);

                Vector3D gravityShip = Vector3D.TransformNormal(gravityd, world2Ship);

                // 获得目标向量
                var target = -new Vector3(gravityShip.X, gravityShip.Y, gravityShip.Z);
                var up = new Vector3(0.0, 1.0, 0.0);
                // 获得旋转矩阵
                var rotationMat = new Matrix3x3();
                Matrix3x3.CreateRotationFromTwoVectors(ref up, ref target, out rotationMat);
                // 获得欧拉角变化值
                var eular = new Vector3();
                Matrix3x3.GetEulerAnglesXYZ(ref rotationMat, out eular);

                // 获得需要旋转的角度, 并且重置方向
                float error = eular.Normalize();

                float speed = (float)_pid.Control(error);

                if (speed <= 0.005)
                {
                    control = false;
                    return;
                }

                var currentEular = new Vector3(eular.X * speed, eular.Y * speed, eular.Z * speed);

                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = true;
                    gyro.Pitch = currentEular.X;
                    gyro.Yaw = currentEular.Y;
                    gyro.Roll = currentEular.Z;
                }
            }
        }
    }
}
