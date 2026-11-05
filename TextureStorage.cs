using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace MetaTSUK
{
    public class TextureStorage
    {
        private static ConcurrentDictionary<string, Bitmap> InternalStorage = new ConcurrentDictionary<string, Bitmap>();
        public static void Init()
        {
            IEnumerable<string> files = Directory.EnumerateFiles("./textures/");
            foreach (string file in files)
            {
                try
                {
                    Bitmap original = new Bitmap(file);

                    // Создаем НОВЫЙ Bitmap с альфа-каналом
                    Bitmap bmap = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

                    // Рисуем оригинал поверх нового (с прозрачным фоном)
                    using (Graphics g = Graphics.FromImage(bmap))
                    {
                        g.Clear(Color.Transparent); // Заливаем прозрачным
                        // Размер задаём явно: DrawImage(original, 0, 0) подгоняет картинку под DPI,
                        // а Paint сохраняет PNG с DPI экрана (120 при масштабе 125%), и картинка ужималась до 80%.
                        // Копируем пиксель в пиксель, без сглаживания.
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height));
                    }
                    Console.WriteLine(bmap.PixelFormat);
                    for (int y = 0; y < bmap.Height; y++)
                    {
                        for (int x = 0; x < bmap.Width; x++)
                        {
                            if (bmap.GetPixel(x, y).ToArgb() == Color.White.ToArgb())
                            {
                                bmap.SetPixel(x, y, Color.Transparent);
                            }
                        }
                    }
                    string name = file.Split('/', '\\').Last();
                    InternalStorage.TryAdd(name, bmap);

                    // block_<имя блока>.png, например block_Object_Dark.png
                    if (name.StartsWith("block_") && name.EndsWith(".png") &&
                        Enum.TryParse(name.Substring(6, name.Length - 10), out Block block))
                    {
                        BlockTexture texture = new BlockTexture(bmap);
                        SetBlockTexture(block, texture);
                        Console.WriteLine($"loaded block texture for {block} ({bmap.Width}x{bmap.Height}{(texture.HasHoles ? ", see-through" : "")})");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load texture {file}: expection occured: {e}");
                }
            }
        }
        private static ConcurrentDictionary<Block, BlockTexture> BlockTextures = new ConcurrentDictionary<Block, BlockTexture>();

        /// <summary>
        /// Те же текстуры в массиве по номеру блока: луч смотрит сюда на каждом попадании, словарь для этого медленный.
        /// null - у блока нет текстуры.
        /// </summary>
        public static readonly BlockTexture[] TextureById = new BlockTexture[65536];
        /// <summary>true - у текстуры блока есть прозрачные пиксели, луч может пройти его насквозь.</summary>
        public static readonly bool[] HasHolesById = new bool[65536];

        /// <summary>Ставит (или убирает, если null) текстуру блока сразу во все таблицы.</summary>
        public static void SetBlockTexture(Block block, BlockTexture texture)
        {
            if (texture != null) BlockTextures[block] = texture;
            else BlockTextures.TryRemove(block, out _);
            TextureById[(ushort)block] = texture;
            HasHolesById[(ushort)block] = texture != null && texture.HasHoles;
        }

        /// <summary>
        /// Текстура блока, скопированная в обычный массив: GetPixel для рендера слишком медленный.
        /// </summary>
        public class BlockTexture
        {
            public readonly int Width, Height;
            // ARGB, построчно сверху вниз
            public readonly int[] Pixels;
            /// <summary>
            /// Есть хоть один прозрачный пиксель (в Paint - чисто белый, 255 255 255). Сквозь такие пиксели луч
            /// проходит дальше, как сквозь воздух. У блоков без дыр текстуру при обходе даже не смотрим.
            /// </summary>
            public readonly bool HasHoles;

            public BlockTexture(Bitmap bmap)
            {
                Width = bmap.Width;
                Height = bmap.Height;
                Pixels = new int[Width * Height];
                BitmapData data = bmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, Pixels, y * Width, Width);
                    }
                }
                finally
                {
                    bmap.UnlockBits(data);
                }
                foreach (int p in Pixels)
                {
                    if (IsHole(p))
                    {
                        HasHoles = true;
                        break;
                    }
                }
            }

            /// <summary>Пиксель полностью прозрачный.</summary>
            public static bool IsHole(int argb) => ((uint)argb >> 24) == 0;

            /// <summary>Пиксель по координатам на грани от 0 до 1.</summary>
            public int Sample(double u, double v)
            {
                int px = (int)(u * Width);
                int py = (int)(v * Height);
                if (px >= Width) px = Width - 1;
                if (py >= Height) py = Height - 1;
                return Pixels[py * Width + px];
            }
        }

        /// <summary>
        /// Текстура блока или null, если для него нет файла block_&lt;имя&gt;.png.
        /// </summary>
        public static BlockTexture GetBlockTexture(Block block)
        {
            BlockTextures.TryGetValue(block, out BlockTexture texture);
            return texture;
        }

        public static Bitmap GetTexture(string textureName)
        {
            if (InternalStorage.TryGetValue(textureName, out Bitmap value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }
    }
}
