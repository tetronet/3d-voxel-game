using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaTSUK
{
    public class WorldGPUConverter
    {
        private const int chunkSize = 64;

        public static (ushort[] blocks, int width, int height, int depth) WorldToFlatArray(
            Universe world,
            long centerX, long centerY, long centerZ,
            int viewRadiusInChunks = 8) // Радиус видимости в чанках
        {
            // Собираем все чанки вокруг камеры
            var chunks = new List<(long cx, long cy, long cz, Chunk chunk)>();

            for (long cx = centerX - viewRadiusInChunks; cx <= centerX + viewRadiusInChunks; cx++)
            {
                for (long cy = centerY - viewRadiusInChunks; cy <= centerY + viewRadiusInChunks; cy++)
                {
                    for (long cz = centerZ - viewRadiusInChunks; cz <= centerZ + viewRadiusInChunks; cz++)
                    {
                        if (world.LoadedChunks.TryGetValue((cx, cy, cz), out var chunk))
                        {
                            chunks.Add((cx, cy, cz, chunk));
                        }
                    }
                }
            }

            if (chunks.Count == 0)
                return (Array.Empty<ushort>(), 0, 0, 0);

            // Вычисляем реальные границы мира
            long minX = chunks.Min(c => c.cx);
            long maxX = chunks.Max(c => c.cx);
            long minY = chunks.Min(c => c.cy);
            long maxY = chunks.Max(c => c.cy);
            long minZ = chunks.Min(c => c.cz);
            long maxZ = chunks.Max(c => c.cz);

            int width = (int)(maxX - minX + 1) * chunkSize;
            int height = (int)(maxY - minY + 1) * chunkSize;
            int depth = (int)(maxZ - minZ + 1) * chunkSize;

            var flat = new ushort[width * height * depth];

            // Заполняем каждый чанк
            foreach (var (cx, cy, cz, chunk) in chunks)
            {
                int offsetX = (int)(cx - minX) * chunkSize;
                int offsetY = (int)(cy - minY) * chunkSize;
                int offsetZ = (int)(cz - minZ) * chunkSize;

                var blocks = chunk.GetArray3D();

                // Копируем чанк в плоский массив
                for (int z = 0; z < chunkSize; z++)
                {
                    for (int y = 0; y < chunkSize; y++)
                    {
                        int srcIndex = z * chunkSize * chunkSize + y * chunkSize;
                        int dstIndex = (offsetZ + z) * width * height + (offsetY + y) * width + offsetX;

                        Buffer.BlockCopy(
                            blocks,
                            srcIndex * sizeof(ushort),
                            flat,
                            dstIndex * sizeof(ushort),
                            chunkSize * sizeof(ushort)
                        );
                    }
                }
            }

            return (flat, width, height, depth);
        }
    }
}
