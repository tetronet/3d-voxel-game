using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace MetaTSUK
{
    public class Ray
    {
        private double phiRad = 0;
        private double thetaRad = 0;
        // Старт луча относительно камеры: целая клетка отдельно, положение внутри неё отдельно.
        // Весь обход идёт от этой клетки, большие числа в double вообще не попадают.
        private readonly long startCellX, startCellY, startCellZ;
        private readonly double startFracX, startFracY, startFracZ;

        private double renderDistance = 128f;
        private double quality = 1.5f;
        private static bool enableEarth = true;

        private double dirX;
        private double dirY;
        private double dirZ;

        //private static Chunk cache;
        private long cx;
        private long cy;
        private long cz;

        public Ray(double phiDegree, double thetaDegree, double x, double y, double z, double renderDistance, double quality, bool ee)
        {
            startCellX = (long)Math.Floor(x); startFracX = x - startCellX;
            startCellY = (long)Math.Floor(y); startFracY = y - startCellY;
            startCellZ = (long)Math.Floor(z); startFracZ = z - startCellZ;
            this.renderDistance = renderDistance;
            this.quality = quality;
            enableEarth = ee;
            SetPhiAndTheta(phiDegree, thetaDegree);
        }

        /// <summary>Точный старт: из decimal-позиции игрока, без потерь на любых координатах.</summary>
        public Ray(double phiDegree, double thetaDegree, PlayerPosition start, double renderDistance, double quality, bool ee)
        {
            startCellX = start.CellX; startFracX = start.FracX;
            startCellY = start.CellY; startFracY = start.FracY;
            startCellZ = start.CellZ; startFracZ = start.FracZ;
            this.renderDistance = renderDistance;
            this.quality = quality;
            enableEarth = ee;
            SetPhiAndTheta(phiDegree, thetaDegree);
        }

        public Ray()
        {

        }

        public Block getPointColorHitByRay()
        {
            Raycast(renderDistance, out RaycastHit hit);
            return hit.Block;
        }

        /// <summary>
        /// Результат <see cref="Raycast"/>.
        /// </summary>
        public struct RaycastHit
        {
            /// <summary>Блок, в который попал луч (Transparent, если не попал).</summary>
            public Block Block;
            /// <summary>Клетка блока, в который попал луч. Её ломать.</summary>
            public long X, Y, Z;
            /// <summary>Клетка прямо перед блоком, соседняя по грани. Сюда ставить.</summary>
            public long PrevX, PrevY, PrevZ;
            /// <summary>Расстояние от камеры до грани блока (для будущего буфера глубины).</summary>
            public double Distance;
            /// <summary>false, если камера сама внутри блока: тогда клетки "перед" нет.</summary>
            public bool HasPrev;
            /// <summary>Ось грани, в которую попал луч: 0 = X (бок), 1 = Y (верх/низ), 2 = Z (бок), -1 = камера внутри блока.</summary>
            public int FaceAxis;
            /// <summary>true, если это верхняя грань (луч шёл вниз). Имеет смысл только при FaceAxis == 1.</summary>
            public bool IsTopFace;
            /// <summary>Точка попадания на грани, от 0 до 1. V = 0 сверху картинки.</summary>
            public double U, V;
            /// <summary>true, если луч ушёл за край мира (±9e18) и был уничтожен. Пиксель рисуется цветом края мира.</summary>
            public bool OutOfWorld;
        }

        /// <summary>
        /// Обход по вокселям (DDA, Amanatides & Woo): луч идёт ровно от грани к грани клеток,
        /// так что ни один блок не пропускается, а предыдущая клетка всегда соседняя по грани.
        /// </summary>
        /// <param name="seeThrough">
        /// true (для рендера): сквозь прозрачные пиксели текстуры луч летит дальше.
        /// false (для кликов): блок ловится целиком, даже если попал в дырку, как хитбокс.
        /// </param>
        /// <returns>true, если луч попал в блок не дальше maxDistance</returns>
        public bool Raycast(double maxDistance, out RaycastHit hit, bool seeThrough = true)
        {
            passedBlock = Block.Transparent;
            bool[] holes = TextureStorage.HasHolesById;
            hit = new RaycastHit { Block = Block.Transparent, Distance = double.PositiveInfinity };

            long x = startCellX, y = startCellY, z = startCellZ;
            double fracX = startFracX, fracY = startFracY, fracZ = startFracZ;

            int stepX = Math.Sign(dirX), stepY = Math.Sign(dirY), stepZ = Math.Sign(dirZ);
            // На сколько надо пройти по лучу, чтобы сдвинуться на одну клетку по оси
            double deltaX = stepX != 0 ? Math.Abs(1 / dirX) : double.PositiveInfinity;
            double deltaY = stepY != 0 ? Math.Abs(1 / dirY) : double.PositiveInfinity;
            double deltaZ = stepZ != 0 ? Math.Abs(1 / dirZ) : double.PositiveInfinity;
            // Расстояние по лучу до первой границы клетки по каждой оси
            double maxX = stepX > 0 ? (1 - fracX) * deltaX : stepX < 0 ? fracX * deltaX : double.PositiveInfinity;
            double maxY = stepY > 0 ? (1 - fracY) * deltaY : stepY < 0 ? fracY * deltaY : double.PositiveInfinity;
            double maxZ = stepZ > 0 ? (1 - fracZ) * deltaZ : stepZ < 0 ? fracZ * deltaZ : double.PositiveInfinity;

            // Кэш последнего чанка, чтобы не лезть в словарь на каждом шаге
            Chunk chunk = null;
            long ccx = long.MinValue, ccy = long.MinValue, ccz = long.MinValue;

            // Точку попадания считаем как смещение от клетки старта: оно маленькое и точное на любых координатах
            long startX = x, startY = y, startZ = z;

            long prevX = x, prevY = y, prevZ = z;
            double t = 0;
            bool first = true;
            int lastAxis = -1;
            Block block;
            while (t <= maxDistance)
            {
                long cx = x >> 6, cy = y >> 6, cz = z >> 6;
                if (cx != ccx || cy != ccy || cz != ccz)
                {
                    Program.game_world.LoadedChunks.TryGetValue((cx, cy, cz), out chunk);
                    ccx = cx; ccy = cy; ccz = cz;
                }
                block = chunk != null ? chunk.GetBlock((byte)(x & 63), (byte)(y & 63), (byte)(z & 63)) : Block.Transparent;
                // Шаги по воздуху (их большинство) новую логику не трогают вообще,
                // текстура проверяется только когда луч упёрся в блок с дырами
                if (block != Block.Transparent)
                {
                    if (!seeThrough || !holes[(ushort)block] || !PassesThrough(block, first, lastAxis, t, x, y, z, prevX, prevY, prevZ))
                    {
                        goto Hit;
                    }
                }

                prevX = x; prevY = y; prevZ = z;
                first = false;
                // Край мира: луч, вышедший за ±9e18, уничтожается. Проверяем только ось, по которой шагнули,
                // это одно сравнение на шаг. Заодно long никогда не переполнится и не перескочит на другой конец.
                long moved;
                if (maxX < maxY && maxX < maxZ)
                {
                    x += stepX; t = maxX; maxX += deltaX; lastAxis = 0; moved = x;
                }
                else if (maxY < maxZ)
                {
                    y += stepY; t = maxY; maxY += deltaY; lastAxis = 1; moved = y;
                }
                else
                {
                    z += stepZ; t = maxZ; maxZ += deltaZ; lastAxis = 2; moved = z;
                }
                if ((moved > PlayerPosition.CellLimit || moved < -PlayerPosition.CellLimit) && t <= maxDistance)
                {
                    hit.OutOfWorld = true;
                    hit.Distance = t;
                    return false;
                }
            }
            return false;

        // Заполнение результата вынесено из цикла: тело цикла меньше, и JIT лучше держит переменные в регистрах
        Hit:
            hit.Block = block;
            hit.X = x; hit.Y = y; hit.Z = z;
            hit.PrevX = prevX; hit.PrevY = prevY; hit.PrevZ = prevZ;
            hit.HasPrev = !first;
            hit.Distance = t;
            hit.FaceAxis = lastAxis;
            hit.IsTopFace = lastAxis == 1 && stepY < 0;

            // Точка попадания внутри клетки блока, каждая координата от 0 до 1
            double lx = Frac01(fracX + t * dirX - (x - startX));
            double ly = Frac01(fracY + t * dirY - (y - startY));
            double lz = Frac01(fracZ + t * dirZ - (z - startZ));
            FaceUV(lastAxis, lx, ly, lz, out hit.U, out hit.V);
            return true;
        }

        // Последняя клетка, которую луч прошёл насквозь (стекло). Поля, а не локальные переменные,
        // чтобы не нагружать горячий цикл: один Ray принадлежит одной строке кадра и одному потоку.
        private Block passedBlock;
        private long passedX, passedY, passedZ;

        /// <summary>
        /// true, если луч пролетает этот блок насквозь: попал в прозрачный пиксель текстуры,
        /// или это стекло вплотную к такому же стеклу (общую грань не рисуем, иначе стеклянная стена была бы в сетке рамок).
        /// </summary>
        private bool PassesThrough(Block block, bool first, int lastAxis, double t, long x, long y, long z, long prevX, long prevY, long prevZ)
        {
            TextureStorage.BlockTexture tex = TextureStorage.TextureById[(ushort)block];
            if (tex == null || !tex.HasHoles)
            {
                return false;
            }
            bool through;
            if (first || (block == passedBlock && prevX == passedX && prevY == passedY && prevZ == passedZ))
            {
                // Камера внутри стекла, или прошлая клетка - такое же стекло, пройденное насквозь
                through = true;
            }
            else
            {
                double lx = Frac01(startFracX + t * dirX - (x - startCellX));
                double ly = Frac01(startFracY + t * dirY - (y - startCellY));
                double lz = Frac01(startFracZ + t * dirZ - (z - startCellZ));
                FaceUV(lastAxis, lx, ly, lz, out double u, out double v);
                through = TextureStorage.BlockTexture.IsHole(tex.Sample(u, v));
            }
            if (through)
            {
                passedBlock = block;
                passedX = x; passedY = y; passedZ = z;
            }
            return through;
        }

        // Точка внутри клетки -> координаты на грани. V = 0 сверху картинки.
        private static void FaceUV(int axis, double lx, double ly, double lz, out double u, out double v)
        {
            switch (axis)
            {
                case 0: u = lz; v = 1 - ly; break;
                case 1: u = lx; v = lz; break;
                case 2: u = lx; v = 1 - ly; break;
                default: u = 0; v = 0; break;
            }
        }

        // Зажимает в [0, 1): на самой грани из-за погрешности может выйти -0.0000001 или 1.0000001
        private static double Frac01(double v)
        {
            if (v < 0) return 0;
            if (v >= 1) return 0.99999999;
            return v;
        }

        public void SetPhiAndTheta(double phiDegree, double thetaDegree)
        {
            phiRad = phiDegree / 57.2958f;
            thetaRad = thetaDegree / 57.2958f;
            dirX = Math.Cos(thetaRad) * Math.Cos(phiRad);
            dirY = Math.Sin(thetaRad);
            dirZ = Math.Cos(thetaRad) * Math.Sin(phiRad);
        }

        public Block getPointColor(double x, double y, double z)
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
            return Program.game_world.GetBlock((long)x, (long)y, (long)z);
        }
    }
}
