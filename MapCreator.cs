using System;
using System.Collections.Generic;
using System.Numerics;

namespace MetaTSUK
{
    public abstract class MapCreator
    {
        private int seed = 0;
        private float smoothness = 1.0f;
        private int max_height = 0;
        private int min_height = 0;
        private Random random = new Random();

        public Chunk GenerateMap(long x, long y, long z)
        {
            Console.WriteLine("generate world");
            Chunk chunk = new Chunk(true);
            for (int i = 0; i < 64; i++) {
                for (int j = 0; j < 64; j++) {
                    for (int k = 0; k < 64; k++) {
                        /*if (!Program.game_world.ContainsKey(i))
                        {
                            Program.game_world.Add(i, new Dictionary<int, Dictionary<int, Block>>());
                        }
                        if (!Program.game_world[i].ContainsKey(j))
                        {
                            Program.game_world[i].Add(j, new Dictionary<int, Block>());
                        }*/
                        chunk.SetBlock(GetBlockMaterialByPoint(new Vector3(i, j, k), x, y, z), (byte)i, (byte)j, (byte)k);
                    }
                }
            }
            // Свежесгенерированный чанк сохранять незачем: его можно сгенерировать заново
            chunk.IsDirty = false;
            return chunk;
        }

        public abstract Block GetBlockMaterialByPoint(Vector3 vector, long cx, long cy, long cz);
    }
}
