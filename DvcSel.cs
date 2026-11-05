namespace MetaTSUK
{
    /// <summary>
    /// Device selector to switch from GPU to CPU render easily.
    /// </summary>
    public class DvcSel
    {
        /// <summary>
        /// Create a new Ray using GPU or CPU device.
        /// </summary>
        /// <param name="isGpuAccelerated">Device (true = GPU, false = CPU)</param>
        /// <param name="phi">Phi Angle for this Ray</param>
        /// <param name="th">Theta Angle for this Ray</param>
        /// <param name="x">X starting point for this Ray</param>
        /// <param name="y">Y starting point for this Ray</param>
        /// <param name="z">Z starting point for this Ray</param>
        /// <param name="rd">Render Distance for this Ray</param>
        /// <param name="q">Quality for this Ray</param>
        /// <param name="ee">Enable Earth for this Ray</param>
        /// <returns></returns>
        public static Ray CreateRay(bool isGpuAccelerated, double phi, double th, double x, double y, double z, double rd, double q, bool ee)
        {
            if (isGpuAccelerated)
            {
                return new AcceleratedRay((float)phi, (float)th, (float)x, (float)y, (float)z, (float)rd, (float)q, ee);
            }
            else
            {
                return new Ray(phi, th, x, y, z, rd, q, ee);
            }
        }

        /// <summary>
        /// То же, но старт берётся из точной позиции игрока: CPU-луч идёт от клетки камеры без потерь точности.
        /// </summary>
        public static Ray CreateRay(bool isGpuAccelerated, double phi, double th, PlayerPosition pos, double rd, double q, bool ee)
        {
            if (isGpuAccelerated)
            {
                return new AcceleratedRay((float)phi, (float)th, (float)pos.X, (float)pos.Y, (float)pos.Z, (float)rd, (float)q, ee);
            }
            return new Ray(phi, th, pos, rd, q, ee);
        }
    }
}
