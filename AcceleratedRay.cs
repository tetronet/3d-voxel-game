using System;

namespace MetaTSUK
{
    public class AcceleratedRay : Ray
    {
        private float phiRad = 0;
        private float thetaRad = 0;
        private readonly float start_x = 0;
        private readonly float start_y = 0;
        private readonly float start_z = 0;

        private float renderDistance = 128f;
        private float quality = 1.5f;
        private static bool enableEarth = true;

        private float dirX;
        private float dirY;
        private float dirZ;

        //private static Chunk cache;
        private long cx;
        private long cy;
        private long cz;
        private GPUMath Math = Program.GPUMath;

        public AcceleratedRay(float phiDegree, float thetaDegree, float x, float y, float z, float renderDistance, float quality, bool ee) : base()
        {
            phiRad = phiDegree / 57.2958f;
            thetaRad = thetaDegree / 57.2958f;
            start_x = x;
            start_y = y;
            start_z = z;
            this.renderDistance = renderDistance;
            this.quality = quality;
            enableEarth = ee;
            dirX = Math.Cos(thetaRad) * Math.Cos(phiRad);
            dirY = Math.Sin(thetaRad);
            dirZ = Math.Cos(thetaRad) * Math.Sin(phiRad);
        }

        public new Block getPointColorHitByRay()
        {
            if (quality <= 0)
                throw new Exception("quality must be > 0");

            float i = 0;
            do
            {
                i += quality;
                Block color = getPointColorAtLength(i);
                if (color != Block.Transparent)
                {
                    return color;
                }
            }
            while (i <= renderDistance);
            return Block.Transparent;
        }

        public new (float, float, float) getPointHitByRay()
        {
            float i = 0;
            do
            {
                i += quality;
                (float, float, float) pos = GetPointPositionOnRayByLenghtOfCameraToPoint(i);
                if (getPointColor(pos) != Block.Transparent)
                {
                    return pos;
                }
            }
            while (i <= renderDistance);
            return (float.NaN, float.NaN, float.NaN);
        }

        public new void SetPhiAndTheta(float phiDegree, float thetaDegree)
        {
            phiRad = phiDegree / 57.2958f;
            thetaRad = thetaDegree / 57.2958f;
            dirX = Math.Cos(thetaRad) * Math.Cos(phiRad);
            dirY = Math.Sin(thetaRad);
            dirZ = Math.Cos(thetaRad) * Math.Sin(phiRad);
        }

        private Block getPointColorAtLength(float lenghtOutOfCamera)
        {
            (float, float, float) point = GetPointPositionOnRayByLenghtOfCameraToPoint(lenghtOutOfCamera);
            return getPointColor(point);
        }

        private (float, float, float) GetPointPositionOnRayByLenghtOfCameraToPoint(float lenghtOutOfCamera)
        {
            float x = (float)(start_x + lenghtOutOfCamera * dirX);
            float y = (float)(start_y + lenghtOutOfCamera * dirY);
            float z = (float)(start_z + lenghtOutOfCamera * dirZ);
            return (x, y, z);
        }

        public new Block getPointColor((float, float, float) point)
        {
            /*if ((long)(point.Item1 / 64) == cx &&
                (long)(point.Item2 / 64) == cy &&
                (long)(point.Item3 / 64) == cz &&
                cache != null)
            {
                return cache.GetBlock((byte)((byte)point.Item1 & 63), (byte)((byte)point.Item1 & 63), (byte)((byte)point.Item1 & 63));
            }
            if (cache == null)
            {
                Console.WriteLine("cached chunk is null: current chunk will be cached");
                if (Program.game_world.LoadedChunks.TryGetValue(((long)(point.Item1 / 64), (long)(point.Item2 / 64), (long)(point.Item3 / 64)), out cache))
                {
                    Console.WriteLine($"successfully cached 1 chunk at {((long)(point.Item1 / 64), (long)(point.Item2 / 64), (long)(point.Item3 / 64))}");
                }
            }*/
            return Program.game_world.GetBlock((int)point.Item1, (int)point.Item2, (int)point.Item3);
        }
    }
}
