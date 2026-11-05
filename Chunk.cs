using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;

namespace MetaTSUK
{
    public class Chunk
    {
        private Block[,,] Blocks = new Block[64, 64, 64];

        /// <summary>
        /// Чанк менялся после загрузки/генерации и ещё не сохранён. Только такие чанки пишутся на диск.
        /// </summary>
        public volatile bool IsDirty = false;

        public Chunk(bool log)
        {
            if (log)
                Console.WriteLine("chunk created");
        }

        public void SetBlock(Block block, byte x, byte y, byte z)
        {
            Blocks[x, y, z] = block;
            IsDirty = true;
            //Console.WriteLine($"block was set at {x} {y} {z}");
        }
        public Block GetBlock(byte x, byte y, byte z)
        {
            return Blocks[x, y, z];
        }
        public Block[,,] GetArray3D()
        {
            return Blocks;
        }
        public void Fill(Block block, byte x1, byte y1, byte z1, byte x2, byte y2, byte z2)
        {
            for (byte x = x1; x < x2; x++)
            {
                for (byte y = y1; y < y2; y++)
                {
                    for (byte z = z1; z < z2; z++)
                    {
                        Blocks[x, y, z] = block;
                    }
                }
            }
        }
        public bool IsChunkEmpty()
        {
            foreach (Block block in Blocks)
            {
                if (block != Block.Transparent)
                {
                    return false;
                }
            }
            return true;
        }
        public void Save(string filename, long x, long y, long z)
        {
            // Сбрасываем ДО записи: если блок поменяют, пока мы пишем, чанк снова станет dirty
            // и сохранится в следующий раз, а не потеряется
            IsDirty = false;
            byte[] buffer = new byte[Blocks.Length * 2 + 24];
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(0), x);
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(8), y);
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(16), z);
            int idx = 24;
            foreach (Block block in Blocks)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(idx), (ushort)block);
                idx += 2;
            }
            if (buffer == null || filename == null)
            {
                throw new Exception("null here is not allowed");
            }
            File.WriteAllBytes(filename, buffer);
        }
        public static (Chunk, long, long, long) Open(byte[] binary, out bool corrupted)
        {
            Chunk chunk = new Chunk(true);

            if (binary.Length != 524312)
            {
                corrupted = true;
                Console.WriteLine($"CORRUPTED CHUNK!!! LENGTH: {binary.Length}");
                return (null, 0, 0, 0);
            }
            corrupted = false;

            long x = BinaryPrimitives.ReadInt64BigEndian(binary.AsSpan(0));
            long y = BinaryPrimitives.ReadInt64BigEndian(binary.AsSpan(8));
            long z = BinaryPrimitives.ReadInt64BigEndian(binary.AsSpan(16));

            int index = 0;

            for (int i = 24; i < binary.Length; i += 2)
            {
                ushort blocktype = BinaryPrimitives.ReadUInt16BigEndian(binary.AsSpan(i));

                // Save пишет через foreach по Block[,,], а он идёт [x, y, z] с z самым быстрым,
                // поэтому index = x * 4096 + y * 64 + z (раньше тут x и z были перепутаны)
                int bx = index / 4096;
                int by = index / 64 % 64;
                int bz = index % 64;

                chunk.Blocks[bx, by, bz] = (Block)blocktype;

                index++;
            }

            return (chunk, x, y, z);
        }

        public ushort[] ToFlatArray()
        {
            var blocks = GetArray3D();
            var flat = new ushort[64 * 64 * 64];

            // Блоки хранятся в памяти как [x, y, z]
            // Нам нужно переупорядочить в [x, y, z] (порядок x -> y -> z)
            // Для этого копируем построчно

            for (int z = 0; z < 64; z++)
            {
                for (int y = 0; y < 64; y++)
                {
                    // Копируем целую строку (64 блока) за раз
                    int srcIndex = z * 64 * 64 + y * 64; // В 3D массиве память упакована [x, y, z]
                    int dstIndex = (z * 64 + y) * 64;    // В плоском массиве [x + y*64 + z*4096]

                    // Копируем 64 блока (каждый по 2 байта)
                    Buffer.BlockCopy(
                        blocks,
                        srcIndex * sizeof(ushort),
                        flat,
                        dstIndex * sizeof(ushort),
                        64 * sizeof(ushort)
                    );
                }
            }

            return flat;
        }
    }
}
