using System;

namespace ClashUp.Shared.Simulation
{
    public static class MovementModel
    {
        public const float MoveSpeed = 5f;
        public const float SpawnSpacing = 3f;
        private const float AxisScale = 32767f;

        public static short EncodeAxis(float value)
        {
            return (short)Math.Clamp(value * AxisScale, -AxisScale, AxisScale);
        }

        public static float DecodeAxis(short encoded)
        {
            return encoded / AxisScale;
        }

        public static void Step(ref float x, ref float z, ref float yaw, float moveX, float moveZ, double dt)
        {
            float mag = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
            if (mag > 1f)
            {
                moveX /= mag;
                moveZ /= mag;
            }

            x += moveX * MoveSpeed * (float)dt;
            z += moveZ * MoveSpeed * (float)dt;

            if (mag > 0.001f)
            {
                yaw = MathF.Atan2(moveX, moveZ) * (180f / MathF.PI);
            }
        }
    }
}
