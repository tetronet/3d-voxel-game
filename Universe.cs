using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Schema;

namespace MetaTSUK
{
    public class Universe
    {
        private string DirectoryForSavingChunks = "";
        private MapCreator UsedMapCreator;
        public ConcurrentDictionary<(long, long, long), Chunk> LoadedChunks = new ConcurrentDictionary<(long, long, long), Chunk>();
        public ConcurrentDictionary<long, PlayerPosition> ClientPositions = new ConcurrentDictionary<long, PlayerPosition>();

        
        public Universe(string dir, MapCreator creator)
        {
            DirectoryForSavingChunks = dir;
            UsedMapCreator = creator;
        }
        /// <summary>
        /// Generates chunks, that are needed by the current clients
        /// </summary>
        /// <param name="creator">Map Creator, that will be used to generate chunks</param>
        /// <returns>How many chunks were totally generated</returns>
        /// <summary>
        /// Сколько чанков грузить в каждую сторону от чанка игрока: 3 = куб 7x7x7.
        /// </summary>
        public const int LoadRadius = 3;

        // 1, пока идёт загрузка/выгрузка: таймер зовёт её каждые 100 мс, а один проход может идти дольше
        private int isUpdatingChunks = 0;

        /// <summary>
        /// Загружает (или генерирует) все чанки в кубе (2 * LoadRadius + 1)^3 вокруг каждого игрока,
        /// ближние первыми, и выгружает те, что ушли далеко.
        /// </summary>
        /// <param name="creator">Map Creator, that will be used to generate chunks</param>
        /// <returns>How many chunks were totally generated or loaded</returns>
        public int GenerateNeccesaryChunks()
        {
            if (Interlocked.Exchange(ref isUpdatingChunks, 1) == 1)
            {
                return 0;
            }
            try
            {
                // Чанк -> квадрат расстояния до ближайшего игрока (в чанках), чтобы грузить ближние первыми
                Dictionary<(long, long, long), long> reqiredToLoad = new Dictionary<(long, long, long), long>();
                foreach (PlayerPosition playerPos in ClientPositions.Values)
                {
                    GetPlayerChunk(playerPos, out long pcx, out long pcy, out long pcz);
                    for (long dx = -LoadRadius; dx <= LoadRadius; dx++)
                    {
                        for (long dy = -LoadRadius; dy <= LoadRadius; dy++)
                        {
                            for (long dz = -LoadRadius; dz <= LoadRadius; dz++)
                            {
                                (long, long, long) chunkpos = (pcx + dx, pcy + dy, pcz + dz);
                                if (LoadedChunks.ContainsKey(chunkpos))
                                {
                                    continue;
                                }
                                long dist = dx * dx + dy * dy + dz * dz;
                                if (!reqiredToLoad.TryGetValue(chunkpos, out long old) || dist < old)
                                {
                                    reqiredToLoad[chunkpos] = dist;
                                }
                            }
                        }
                    }
                }

                foreach (KeyValuePair<(long, long, long), long> kv in reqiredToLoad.OrderBy(kv => kv.Value))
                {
                    (long, long, long) chunkpos = kv.Key;
                    LoadedChunks.TryAdd(chunkpos, LoadOrGenerate(chunkpos.Item1, chunkpos.Item2, chunkpos.Item3));
                }

                UnloadNotUsedChunks();
                return reqiredToLoad.Count;
            }
            finally
            {
                Interlocked.Exchange(ref isUpdatingChunks, 0);
            }
        }

        private Chunk LoadOrGenerate(long cx, long cy, long cz)
        {
            string file = DirectoryForSavingChunks + $"chunk.{cx}.{cy}.{cz}.bin";
            if (File.Exists(file))
            {
                (Chunk, long, long, long) chunkandpos = Chunk.Open(File.ReadAllBytes(file), out bool corrupted);
                if (!corrupted)
                {
                    Console.WriteLine($"load chunk at X={cx} Y={cy} Z={cz}");
                    return chunkandpos.Item1;
                }
                Console.WriteLine("CORRUPTED CHUNK - REGENERATE");
            }
            return UsedMapCreator.GenerateMap(cx, cy, cz);
        }

        /// <summary>
        /// Заполняет блоком область dx * dy * dz, начиная с угла (x, y, z).
        /// </summary>
        public void Fill(Block block, long x, long y, long z, long dx, long dy, long dz)
        {
            for (long curX = 0; curX < dx; curX++)
            {
                for (long curY = 0; curY < dy; curY++)
                {
                    for (long curZ = 0; curZ < dz; curZ++)
                    {
                        SetBlock(block, curX + x, curY + y, curZ + z);
                    }
                }
            }
        }
        /// <summary>
        /// Заполняет блоком шар радиуса r с центром в блоке (x, y, z).
        /// </summary>
        public void FillSphere(Block block, long x, long y, long z, long r)
        {
            // Смещения от центра, от -r до r включительно
            for (long dx = -r; dx <= r; dx++)
            {
                for (long dy = -r; dy <= r; dy++)
                {
                    for (long dz = -r; dz <= r; dz++)
                    {
                        // very hard math btw. Формула сферы работает для смещений ОТ ЦЕНТРА:
                        // раньше сюда шли мировые координаты, и расстояние считалось от (0, 0, 0) мира
                        if (dx * dx + dy * dy + dz * dz <= r * r)
                        {
                            SetBlock(block, x + dx, y + dy, z + dz);
                        }
                    }
                }
            }
        }

        // Чанк, в котором стоит игрок. Floor, а не (long): иначе -0.5 попадает в чанк 0, а не -1
        private static void GetPlayerChunk(PlayerPosition playerPos, out long cx, out long cy, out long cz)
        {
            cx = playerPos.CellX >> 6;
            cy = playerPos.CellY >> 6;
            cz = playerPos.CellZ >> 6;
        }
        /// <summary>
        /// Gets block at a given coordinate.
        /// </summary>
        /// <param name="x">X pos</param>
        /// <param name="y">Y pos</param>
        /// <param name="z">Z pos</param>
        /// <returns>Block at coordinate, or <see cref="Block"/>.Transparent, if this loading this block failed</returns>
        public Block GetBlock(long x, long y, long z)
        {
            GetChunkedByBlockPosition(x, y, z, out long cx, out long cy, out long cz, out byte bx, out byte by, out byte bz);
            if (LoadedChunks.TryGetValue((cx, cy, cz), out Chunk chunk))
            {
                return chunk.GetBlock(bx, by, bz);
            }
            return Block.Transparent;
        }
        public void SetBlock(Block block, long x, long y, long z)
        {
            GetChunkedByBlockPosition(x, y, z, out long cx, out long cy, out long cz, out byte bx, out byte by, out byte bz);
            if (LoadedChunks.TryGetValue((cx, cy, cz), out Chunk chunk))
            {
                chunk.SetBlock(block, bx, by, bz);
            }
            else
            {
                Console.WriteLine("failed to set block: this block doesn't exist");
                Console.WriteLine("chunk will be created NOW");
                GetChunkedByBlockPosition(x, y, z, out long cx_, out long cy_, out long cz_, out _, out _, out _);
                if (File.Exists(DirectoryForSavingChunks + $"chunk.{cx_}.{cy_}.{cz_}.bin"))
                {
                    Console.WriteLine("loaded chunk from the persistent storage");
                    (Chunk, long, long, long) chunkandpos = Chunk.Open(File.ReadAllBytes(DirectoryForSavingChunks + $"chunk.{cx_}.{cy_}.{cz_}.bin"), out _);
                    LoadedChunks.TryAdd((chunkandpos.Item2, chunkandpos.Item3, chunkandpos.Item4), chunkandpos.Item1);
                }
                else
                {
                    LoadedChunks.TryAdd((x >> 6, y >> 6, z >> 6), UsedMapCreator.GenerateMap(cx_, cy_, cz_));
                }
                // Раньше блок в только что созданный/загруженный чанк так и не ставился
                if (LoadedChunks.TryGetValue((cx, cy, cz), out chunk))
                {
                    chunk.SetBlock(block, bx, by, bz);
                }
            }
        }
        /// <summary>
        /// Gets Chunk and inner position for the block by a 3-dimentional coordinate
        /// </summary>
        /// <param name="x">X</param>
        /// <param name="y">Y</param>
        /// <param name="z">Z</param>
        /// <returns>Chunk position and Block relative position</returns>
        public void GetChunkedByBlockPosition(long x, long y, long z, out long cx, out long cy, out long cz, out byte bx, out byte by, out byte bz)
        {
            cx = (x >> 6);
            cy = (y >> 6);
            cz = (z >> 6);
            bx = (byte)(x & 63);
            by = (byte)(y & 63);
            bz = (byte)(z & 63);
        }
        /// <summary>
        /// Saves every chunk in to the persistent data storage device.
        /// </summary>
        /// <returns>How many chunks are currently loaded.</returns>
        public int SaveAllChunks()
        {
            foreach (KeyValuePair<(long, long, long), Chunk> kv in LoadedChunks)
            {
                Chunk chunk = kv.Value;
                // Нетронутые чанки не пишем: их можно сгенерировать заново точно такими же.
                // Иначе при кубе 7x7x7 автосохранение писало бы ~170 МБ каждые 20 секунд.
                if (!chunk.IsDirty)
                {
                    continue;
                }
                chunk.Save(DirectoryForSavingChunks + $"chunk.{kv.Key.Item1}.{kv.Key.Item2}.{kv.Key.Item3}.bin", kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
            }
            return LoadedChunks.Count;
        }
        /// <summary>
        /// Forcefully unloads a chunk to the persistent data storage device.
        /// </summary>
        /// <param name="x">X axis of the chunk.</param>
        /// <param name="y">Y axis of the chunk.</param>
        /// <param name="z">Z axis of the chunk.</param>
        /// <returns>How many chunks are currently loaded.</returns>
        public int ForceUnloadAndSave(long x, long y, long z)
        {
            if (LoadedChunks.TryGetValue((x, y, z), out Chunk chunk))
            {
                chunk.Save(DirectoryForSavingChunks + $"chunk.{x}.{y}.{z}.bin", x, y, z);
                LoadedChunks.TryRemove((x, y, z), out _);
            }
            return LoadedChunks.Count;
        }
        public int ForceLoad(long x, long y, long z)
        {
            (Chunk, long, long, long) chunkandpos = Chunk.Open(File.ReadAllBytes(DirectoryForSavingChunks + $"chunk.{x}.{y}.{z}.bin"), out bool corrupted);
            if (corrupted)
            {
                return LoadedChunks.Count;
            }
            LoadedChunks.TryAdd((chunkandpos.Item2, chunkandpos.Item3, chunkandpos.Item4), chunkandpos.Item1);
            return LoadedChunks.Count;
        }
        /// <summary>
        /// Выгружает чанки, которые дальше LoadRadius + 1 от всех игроков (лишний +1 - чтобы чанки
        /// на границе не грузились и не выгружались туда-сюда, когда ходишь по краю). Изменённые сохраняются.
        /// </summary>
        /// <returns>How many chunks were unloaded.</returns>
        public int UnloadNotUsedChunks()
        {
            List<(long, long, long)> unnecesaryChunks = new List<(long, long, long)>();
            foreach (var kv in LoadedChunks)
            {
                bool used = false;

                foreach (PlayerPosition playerPos in ClientPositions.Values)
                {
                    GetPlayerChunk(playerPos, out long cx, out long cy, out long cz);

                    if (Math.Abs(kv.Key.Item1 - cx) <= LoadRadius + 1 &&
                        Math.Abs(kv.Key.Item2 - cy) <= LoadRadius + 1 &&
                        Math.Abs(kv.Key.Item3 - cz) <= LoadRadius + 1)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    unnecesaryChunks.Add(kv.Key);
                }
            }
            foreach ((long, long, long) chunkpos in unnecesaryChunks)
            {
                if (LoadedChunks.TryRemove(chunkpos, out Chunk chunk) && chunk.IsDirty)
                {
                    chunk.Save(DirectoryForSavingChunks + $"chunk.{chunkpos.Item1}.{chunkpos.Item2}.{chunkpos.Item3}.bin", chunkpos.Item1, chunkpos.Item2, chunkpos.Item3);
                }
            }
            return unnecesaryChunks.Count;
        }
        public int LoadUsedChunks()
        {
            List<(long, long, long)> unnecesaryChunks = new List<(long, long, long)>();
            foreach (var kv in LoadedChunks)
            {
                bool used = false;

                foreach (PlayerPosition playerPos in ClientPositions.Values)
                {
                    GetChunkedByBlockPosition(
                        playerPos.CellX,
                        playerPos.CellY,
                        playerPos.CellZ, out long cx, out long cy, out long cz, out _, out _, out _);

                    if (cx == kv.Key.Item1 &&
                        cy == kv.Key.Item2 &&
                        cz == kv.Key.Item3)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    unnecesaryChunks.Add(kv.Key);
                }
            }
            foreach ((long, long, long) chunkpos in unnecesaryChunks)
            {
                (Chunk, long, long, long) chunkandpos = Chunk.Open(File.ReadAllBytes(DirectoryForSavingChunks + $"chunk.{chunkpos.Item1}.{chunkpos.Item2}.{chunkpos.Item3}.bin"), out bool corrupted);
                if (corrupted)
                {
                    continue;
                }
                LoadedChunks.TryAdd((chunkandpos.Item2, chunkandpos.Item3, chunkandpos.Item4), chunkandpos.Item1);
            }
            return unnecesaryChunks.Count;
        }
    }
}
