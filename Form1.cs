using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MetaTSUK
{
    public partial class Form1 : Form
    {
        // Readonly params:
        public static readonly double multiplier = 1;
        public static readonly int width = 960, height = 960;
        // Всё остальное (обзор, центр, рука) задано для старого кадра 120x120 и масштабируется от него:
        // 1 "старый" пиксель = uiScale настоящих. Обзор остаётся 120 градусов при любом разрешении.
        public static readonly int uiScale = width / 120;
        public static readonly double pixelsPerDegree = width / 120.0;
        // Угол обзора по горизонтали. 90 - как в большинстве шутеров; больше 110 края сильно растягиваются
        public static readonly double horizontalFovDegrees = 90.0;
        // Сколько единиц плоского экрана (на расстоянии 1 от глаза) приходится на один пиксель
        public static readonly double screenPlaneScale = Math.Tan(horizontalFovDegrees / 2 / 57.2958) / (width / 2.0);
        private readonly MapCreator creator = new FlatChessMapCreator();
        // Writable params:
        private object renderLock = new object();
        // Есть кадр, который ещё не выведен на экран
        private volatile bool newFrameReady = false;
        // 1, пока tick выводит кадр: чтобы задачи вывода не копились, если вывод медленнее таймера
        private int isPresenting = 0;
        public static double renderDistance = 64;
        public static double quality = 0.05f;
        public static bool ee = true;
        public static double fov = 100;
        public static bool fovcorrection = false;
        // Куда в кадре смотрит камера (там лаймовая точка), относительно центра кадра.
        // Раньше было (-15, -15) * uiScale, теперь ровно центр: кадр в fullscreen рисуется по центру экрана.
        public static readonly int screenCenterLeft = 0, screenCenterTop = 0;
        // Какой блок ставится на правую кнопку мыши
        private Block blockToPlace = Block.Object_Solid;
        // Temp properties:
        private double cameraAngleLeftRight = 0;
        private double cameraAngleTopBottom = 0;
        private PlayerPosition cameraPos = new PlayerPosition(0, 65, 0);
        private decimal performance = 0;
        private decimal renderperformance = 0;
        private long fd = 0;
        private bool gpurender = false;
        private Stopwatch gameLoadTimestamp = Stopwatch.StartNew();
        private bool fastMovement = false;
        private bool fullscreenMode = false;
        // Размер панели, запоминается в потоке окна: tick рисует из фонового потока
        private Size panelSize;
        // После изменения размера один раз залить всё чёрным, чтобы по краям не осталось мусора
        private volatile bool clearScreen = false;

        // Max for first value is 60.
        // Max for second value is 20.

        private Random random = new Random();

        private Bitmap game = new Bitmap(width, height);
        //private Bitmap playerHandCopy;
        private Graphics graphics;
        private bool isClicking = false;
        private bool isMagicing = false;
        private bool isMagicing2 = false;
        private bool mouseHolding = true;
        private int handAnimationIndex = 0;
        private Point[] HandAnimation = new Point[] { new Point(0, 0), new Point(2, 2), new Point(4, 0), new Point(2, 2) };
        private Stopwatch handAnimationCalmer = Stopwatch.StartNew();
        private bool isBlockPreviewLarge = false;
        private volatile ScreenEffects currentScreenEffect = ScreenEffects.None;
        // До какого момента (по screenEffectClock, в мс) показывать текущий эффект
        private long screenEffectUntil = 0;
        private readonly Stopwatch screenEffectClock = Stopwatch.StartNew();

        public Form1()
        {
            InitializeComponent();
            graphics = panel1.CreateGraphics();
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            panelSize = panel1.ClientSize;
            // Поля вокруг кадра в fullscreen чёрные
            panel1.BackColor = Color.Black;
            panel1.Resize += panel1_Resize;
            TextureStorage.Init();
            //playerHandCopy = new Bitmap(TextureStorage.GetTexture("ruca1.png"));
            
            if (!File.Exists("clientfd.txt"))
            {
                fd = (long)random.Next() * random.Next();
                File.WriteAllText("clientfd.txt", fd.ToString());
            }
            else
            {
                if (!long.TryParse(File.ReadAllText("clientfd.txt"), out fd))
                {
                    MessageBox.Show("Meta Tsu-k 3D had a fatal error: failed to parse Client Fd, must check clientfd.txt: it can contain a valid 64-bit integer number from -9223372036854775808 to 9223372036854775807, and must not contain any additional characters except digits and may start from a single \"-\" symbol for negative numbers.");
                    Application.Exit();
                }
            }
            if (File.Exists("./gameworld/local.txt"))
            {
                ReadLocalTxt();
            }
            if (!Program.game_world.ClientPositions.TryAdd(fd, cameraPos))
            {
                MessageBox.Show("Meta Tsu-k 3D had a fatal error: failed to add a key for current client.");
                Application.Exit();
            }
            Task.Run(() =>
            {
                Console.WriteLine("Generated or loaded " + Program.game_world.GenerateNeccesaryChunks() + " chunks");
            });
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        long timeTicksOnBegin = DateTime.Now.Ticks;
                        await fillGame();
                        long timeTicksOnEnd = DateTime.Now.Ticks;
                        renderperformance = 1m / ((timeTicksOnEnd - timeTicksOnBegin) / (decimal)TimeSpan.TicksPerSecond);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            });
            Console.WriteLine(Program.game_world);
            //Console.WriteLine(Program.game_world.Count);
        }

        /*public void CharArrayWith2IndexToString(char[,] array, out string lines) {
            lines = "";
            
            for (int top = 0; top < height; top++) {            
                for (int left = 0; left < width; left++)
                {                  
                    try
                    {
                        lines += array[top, left];
                    }
                    catch (Exception ex)
                    {
                         ex.GetType().ToString();
                    }        
                }
                lines += '\n';          
            } 
        }*/

        public Bitmap ArrayViewToBitmapMarshal(ArrayView<double> pixels, int width, int height)
        {
            double[] pixelData = pixels.GetAsArray();

            // Создаем массив байт в правильном порядке (BGR)
            byte[] byteData = new byte[width * height * 3];

            for (int i = 0; i < pixelData.Length; i += 3)
            {
                byteData[i + 0] = (byte)(pixelData[i + 2] * 255); // B
                byteData[i + 1] = (byte)(pixelData[i + 1] * 255); // G
                byteData[i + 2] = (byte)(pixelData[i + 0] * 255); // R
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                // Копируем подготовленный массив в Bitmap
                Marshal.Copy(byteData, 0, data.Scan0, byteData.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private async Task fillGame()
        {
            if (!gpurender)
            {
                try
                {
                    // Создаем временный массив для результатов
                    int[] pixels = new int[height * width];

                    // Поворот камеры считаем один раз на кадр
                    double yawRad = cameraAngleLeftRight / multiplier / 57.2958;
                    double pitchRad = -cameraAngleTopBottom / multiplier / 57.2958;
                    double cosYaw = Math.Cos(yawRad), sinYaw = Math.Sin(yawRad);
                    double cosPitch = Math.Cos(pitchRad), sinPitch = Math.Sin(pitchRad);

                    // Одна позиция на весь кадр: все лучи стартуют из одной и той же точки
                    PlayerPosition framePos = cameraPos;

                    ParallelLoopResult plr = Parallel.For(-height / 2, height / 2, delegate (int top)
                    {
                        Ray ray = DvcSel.CreateRay(gpurender, 0, 0, framePos, renderDistance, quality, ee);
                        for (int left = -width / 2; left < width / 2; left++)
                        {
                            // Коррекция сферических искажений (если вдруг захочется)
                            double correctedLeft = left / multiplier;
                            double correctedTop = (-top + 15) / multiplier;

                            // Нормализация и коррекция "рыбьего глаза"
                            double r = Math.Sqrt(correctedLeft * correctedLeft + correctedTop * correctedTop);
                            double fov = 1.0; // Можно регулировать
                            double scale = Math.Tan(r * fov) / (r * fov);
                            correctedLeft *= scale;
                            correctedTop *= scale;
                            if (fovcorrection)
                            {
                                ray.SetPhiAndTheta(correctedLeft, correctedTop);
                            }
                            else
                            {
                                // Угол пикселя задаём относительно камеры, а потом поворачиваем камерой.
                                // Раньше углы камеры просто прибавлялись к theta/phi, и у полюсов всё ехало.
                                // Отступ считаем от визуального центра, чтобы он совпадал с направлением камеры.
                                // Перспектива "камеры-обскуры": пиксель лежит на плоском экране перед глазом,
                                // а не на сфере. Раньше угол рос линейно с пикселем (1 градус = N пикселей),
                                // это и есть рыбий глаз: прямые стены гнулись дугой к краям.
                                double sx = (left - screenCenterLeft) * screenPlaneScale / multiplier;
                                double sy = -(top - screenCenterTop) * screenPlaneScale / multiplier;
                                double localPhi = Math.Atan2(sx, 1.0) * 57.2958;
                                double localTheta = Math.Atan2(sy, Math.Sqrt(1.0 + sx * sx)) * 57.2958;
                                CameraToWorldAngles(localPhi, localTheta, cosYaw, sinYaw, cosPitch, sinPitch, out double worldPhi, out double worldTheta);
                                ray.SetPhiAndTheta(worldPhi, worldTheta);
                            }

                            ray.Raycast(renderDistance, out Ray.RaycastHit hit);
                            int clr = GetPixelColor(hit);
                            // Лаймовая точка размером с один "старый" пиксель, центр точки - ровно направление камеры
                            if (top >= screenCenterTop - uiScale / 2 && top < screenCenterTop + uiScale / 2 &&
                                left >= screenCenterLeft - uiScale / 2 && left < screenCenterLeft + uiScale / 2)
                            {
                                clr = Color.Lime.ToArgb();
                            }
                            pixels[(top + height / 2) * width + (left + width / 2)] = clr;
                        }
                    });

                    while (!plr.IsCompleted)
                    {

                    }
                    // LockBits, копирование, UnlockBits и рука - всё под ОДНИМ захватом lock.
                    // Раньше UnlockBits шёл отдельно в finally, и между ними tick успевал схватить
                    // ещё заблокированный кадр: "Область растрового изображения уже заблокирована".
                    lock (renderLock)
                    {
                        BitmapData data = game.LockBits(
                            new Rectangle(0, 0, game.Width, game.Height),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppArgb);
                        try
                        {
                            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                        }
                        finally
                        {
                            game.UnlockBits(data);
                        }

                        using (Graphics g = Graphics.FromImage(game))
                        {
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            // Без этого при увеличении пиксели руки съезжают на полпикселя
                            g.PixelOffsetMode = PixelOffsetMode.Half;
                            if (isMagicing)
                            {
                                DrawHand(g, "ruca1enchants.png", isMagicing2 ? 10 : 0);
                            }
                            else
                            {
                                DrawHand(g, "ruca1.png", isClicking ? 10 : 0);
                            }
                            DrawScreenEffect(g);
                            g.DrawImage(TextureStorage.GetTexture("block_" + blockToPlace.ToString() + ".png"), 25, 25, isBlockPreviewLarge ? 128 : 32, isBlockPreviewLarge ? 128 : 32);
                        }
                        newFrameReady = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            /*else
            {
                ArrayView<double> output = new ArrayView<double>();
                var wrld = WorldGPUConverter.WorldToFlatArray(Program.game_world, 0, 0, 0);
                var buffer = Accelerator.Current.Allocate1D<ushort>(wrld.Item1.LongLength);
                ArrayView<ushort> view = buffer.View;
                RaytracingKernel.Trace(new Index2D(width, height), new ArrayView<int>(buffer, 0, wrld.Item1.LongLength), wrld.Item2, wrld.Item3, wrld.Item4, output, (float)cameraPos.X, (float)cameraPos.Y, (float)cameraPos.Z, cameraAngleLeftRight / 57.2957f, 3.75f, renderDistance, 1f);
                game = ArrayViewToBitmapMarshal(output, game.Width, game.Height);
            }*/
            Thread.Sleep(10);
        }

        // Рука нарисована под кадр 120x120 (96x96 со сдвигами анимации), увеличиваем её в uiScale раз
        private void DrawHand(Graphics g, string texture, int yOffset)
        {
            Point p = HandAnimation[handAnimationIndex];
            g.DrawImage(TextureStorage.GetTexture(texture), p.X * uiScale, (p.Y + yOffset) * uiScale, 120 * uiScale, 120 * uiScale);
        }

        /// <summary>
        /// Показывает эффект screff_&lt;имя&gt;.png поверх кадра на duration миллисекунд.
        /// Ничего не ждёт: рендер сам рисует эффект в каждом кадре, пока время не выйдет.
        /// </summary>
        private void ApplyScreenEffects(ScreenEffects effect, int duration)
        {
            // Interlocked: long в 32-битном процессе пишется в два приёма, а читает его поток рендера
            Interlocked.Exchange(ref screenEffectUntil, screenEffectClock.ElapsedMilliseconds + duration);
            currentScreenEffect = effect;
        }

        // Вызывается из рендера под renderLock, поверх руки
        private void DrawScreenEffect(Graphics g)
        {
            ScreenEffects effect = currentScreenEffect;
            if (effect == ScreenEffects.None)
            {
                return;
            }
            if (screenEffectClock.ElapsedMilliseconds >= Interlocked.Read(ref screenEffectUntil))
            {
                currentScreenEffect = ScreenEffects.None;
                return;
            }
            Bitmap texture = TextureStorage.GetTexture($"screff_{effect}.png".ToLower());
            if (texture == null)
            {
                // Без этой проверки DrawImage(null) кидал бы исключение и терялся весь кадр
                return;
            }
            // Эффект нарисован под кадр 120x120, как рука: растягиваем на весь кадр.
            // Белое в картинке прозрачное, так что сквозь него видно игру.
            g.DrawImage(texture, 0, 0, width, height);
        }

        /// <summary>
        /// Цвет пикселя по попаданию луча: текстура блока (если есть block_&lt;имя&gt;.png) или его цвет,
        /// плюс затенение граней, чтобы блоки выглядели объёмными.
        /// </summary>
        // Цвет края мира: луч дошёл до ±9e18. Магента - классический цвет "тут ничего нет", с небом не спутать.
        private static readonly int worldEdgeColor = Color.Magenta.ToArgb();

        private int GetPixelColor(Ray.RaycastHit hit)
        {
            if (hit.OutOfWorld)
            {
                return worldEdgeColor;
            }
            int baseColor = GetColorFromMaterial(hit.Block).ToArgb();
            if (hit.Block == Block.Transparent || hit.FaceAxis < 0)
            {
                return baseColor;
            }

            int clr = baseColor;
            TextureStorage.BlockTexture texture = TextureStorage.GetBlockTexture(hit.Block);
            if (texture != null)
            {
                int texel = texture.Sample(hit.U, hit.V);
                // Прозрачные (белые в Paint) пиксели текстуры показывают обычный цвет блока
                if (((uint)texel >> 24) != 0)
                {
                    clr = texel;
                }
            }

            int shade; // из 256
            switch (hit.FaceAxis)
            {
                case 1: shade = hit.IsTopFace ? 256 : 128; break; // верх светлый, низ тёмный
                case 0: shade = 205; break;                       // бока по X
                default: shade = 166; break;                      // бока по Z
            }
            int r = ((clr >> 16) & 255) * shade >> 8;
            int g = ((clr >> 8) & 255) * shade >> 8;
            int b = (clr & 255) * shade >> 8;
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }

        private Color GetColorFromMaterial(Block color)
        {
            switch (color)
            {
                case Block.Transparent:
                    return Color.White;
                case Block.Object_Dark:
                    return Color.DarkBlue;
                case Block.Object_Light:
                    return Color.Gray;
                case Block.Object_Translucent:
                    return Color.LightGray;
                case Block.Object_Solid:
                    return Color.DimGray;
                case Block.Object_Copybook:
                    return Color.LimeGreen;
                case Block.Ground:
                    return Color.Brown;
                case Block.Opaque:
                    return Color.LightYellow;
                default:
                    //Console.WriteLine("unknown block");
                    return Color.FromArgb(
                        255,
                        random.Next(256),
                        random.Next(256),
                        random.Next(256)
                    );
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            tick.Start();
            chunkGen.Start();
            rawMouseInput = RawMouse.Register(Handle);
            Console.WriteLine(rawMouseInput ? "raw mouse input enabled" : "raw mouse input failed, using cursor position");
            FitWindowToFrame();
        }

        // Форма в дизайнере сделана при 120 DPI (AutoScaleDimensions 8x16), а игра видит 96 DPI,
        // и WinForms при запуске ужимает её до ~706x742 - кадр 960x960 туда не влезал и обрезался.
        // Поэтому размер окна задаём сами: ровно под кадр, но не больше рабочей области экрана.
        private void FitWindowToFrame()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int borderWidth = Width - ClientSize.Width;
            int borderHeight = Height - ClientSize.Height;
            ClientSize = new Size(
                Math.Min(width, area.Width - borderWidth),
                Math.Min(height, area.Height - borderHeight));
            // CenterScreen уже отработал для старого размера, центрируем заново
            Location = new Point(
                area.X + (area.Width - Width) / 2,
                area.Y + (area.Height - Height) / 2);
        }

        // Камера крутится от RAW ввода (WndProc), а не от MouseMove
        private bool rawMouseInput = false;
        // Градусов поворота на одну единицу сдвига мыши (как раньше /10 на пиксель)
        private double mouseSensitivity = 0.2;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == RawMouse.WM_INPUT && rawMouseInput &&
                RawMouse.TryRead(m.LParam, out int dx, out int dy, out bool absolute))
            {
                if (absolute)
                {
                    // Удалённый рабочий стол или планшет: сдвигов нет, возвращаемся к старому способу
                    rawMouseInput = false;
                    Console.WriteLine("mouse sends absolute coordinates, raw mouse input disabled");
                }
                else if (mouseHolding)
                {
                    RotateCamera(dx, dy);
                }
            }
            // Для WM_INPUT base обязательно: DefWindowProc освобождает данные сообщения
            base.WndProc(ref m);
        }

        private void RotateCamera(double dx, double dy)
        {
            cameraAngleLeftRight += dx * mouseSensitivity;
            cameraAngleTopBottom += dy * mouseSensitivity;
            // Переход через 180 без потери остатка (раньше просто прыгало на -180)
            if (cameraAngleLeftRight > 180)
            {
                cameraAngleLeftRight -= 360;
            }
            if (cameraAngleLeftRight < -180)
            {
                cameraAngleLeftRight += 360;
            }
            // clamp Top..Bottom camera rotation to prevent fuck ups
            if (cameraAngleTopBottom < -85)
            {
                cameraAngleTopBottom = -85;
            }
            if (cameraAngleTopBottom > 85)
            {
                cameraAngleTopBottom = 85;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // by the keycode
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    new Settings(fd, cameraPos.X, cameraPos.Y, cameraPos.Z, (int)(quality * 100), delegate ()
                    {
                        Console.WriteLine("CALLED");
                        isMagicing = false;
                        mouseHolding = true;
                    }, renderDistance).Show();
                    isMagicing = true;
                    mouseHolding = false;
                    Task.Run(async delegate ()
                    {
                        isMagicing2 = true;
                        await Task.Delay(250);
                        isMagicing2 = false;
                    });
                    break;
                case Keys.W:
                case Keys.Up:
                    TryWalk(1);
                    if (handAnimationCalmer.ElapsedMilliseconds > 100)
                    {
                        if (handAnimationIndex + 1 == 4)
                        {
                            handAnimationIndex = 0;
                        }
                        else
                        {
                            handAnimationIndex++;
                        }
                        handAnimationCalmer.Restart();
                    }
                    break;
                case Keys.S:
                case Keys.Down:
                    TryWalk(-1);
                    break;
                case Keys.ShiftKey:
                    if (Program.game_world.GetBlock(cameraPos.CellX, cameraPos.CellY - 2, cameraPos.CellZ) == Block.Transparent)
                    {
                        cameraPos = cameraPos.Moved(0, fastMovement ? -1 : -0.1, 0);
                    }
                    break;
                case Keys.Space:
                    cameraPos = cameraPos.Moved(0, fastMovement ? 1 : 0.1, 0);
                    break;
                case Keys.P:
                    Save();
                    new AutoClosingWindow("Success", "Saved game successfully", 1000).Show();
                    break;
                case Keys.Delete:
                    Save();
                    mouseHolding = false;
                    Task.Run(delegate ()
                    {
                        if (MessageBox.Show("Do you want to exit the game?", "Meta Tsu-k 3D", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                        {
                            Application.Exit();
                        }
                        else
                        {
                            mouseHolding = true;
                        }
                    });
                    break;
                case Keys.F1:
                    Console.WriteLine("Dark block selected");
                    blockToPlace = Block.Object_Dark;
                    EnlargeBlockPreview();
                    break;
                case Keys.F2:
                    Console.WriteLine("Light block selected");
                    blockToPlace = Block.Object_Light;
                    EnlargeBlockPreview();
                    break;
                case Keys.F3:
                    Console.WriteLine("Translucent block selected");
                    blockToPlace = Block.Object_Translucent;
                    EnlargeBlockPreview();
                    break;
                case Keys.F4:
                    Console.WriteLine("Solid block selected");
                    blockToPlace = Block.Object_Solid;
                    EnlargeBlockPreview();
                    break;
                case Keys.F5:
                    Console.WriteLine("Copybook block selected");
                    blockToPlace = Block.Object_Copybook;
                    EnlargeBlockPreview();
                    break;
                case Keys.F6:
                    Console.WriteLine("Debug block selected");
                    blockToPlace = Block.Object_Debug;
                    EnlargeBlockPreview();
                    break;
                case Keys.F7:
                    Console.WriteLine("Ground block selected");
                    blockToPlace = Block.Ground;
                    EnlargeBlockPreview();
                    break;
                case Keys.F8:
                    Console.WriteLine("Opaque block selected");
                    blockToPlace = Block.Opaque;
                    EnlargeBlockPreview();
                    break;
                case Keys.F9:
                    Console.WriteLine("Detonator block selected");
                    blockToPlace = Block.Detonator;
                    EnlargeBlockPreview();
                    break;
                case Keys.F10:
                    Console.WriteLine("Nuke block selected");
                    blockToPlace = Block.Nuke;
                    EnlargeBlockPreview();
                    break;
                case Keys.F11:
                    Console.WriteLine("FULLSCREEN MODE SWITCHED");
                    fullscreenMode = !fullscreenMode;
                    DoFullscreenStuff();
                    break;
                case Keys.D1:
                    Console.WriteLine("Computer block selected");
                    blockToPlace = Block.Computer;
                    EnlargeBlockPreview();
                    break;
                case Keys.Control:
                case Keys.ControlKey:
                case Keys.RControlKey:
                case Keys.LControlKey:
                    Console.WriteLine("Switch fast movement");
                    fastMovement = !fastMovement;
                    break;
            }
            // update client position
            //cameraPos = Program.game_world.ClientPositions[fd];
            Program.game_world.ClientPositions[fd] = cameraPos;
        }
        // Шаг вперёд (direction = 1) или назад (-1) по взгляду, если ноги и голова пройдут.
        // Шаг маленький и считается в double, а прибавляется к decimal - на любых координатах без потерь.
        private void TryWalk(int direction)
        {
            double step = direction * (fastMovement ? 1 : 0.1);
            PlayerPosition next = cameraPos.Moved(Math.Cos(cameraAngleLeftRight / 57.2958) * step, 0, Math.Sin(cameraAngleLeftRight / 57.2958) * step);
            if (Program.game_world.GetBlock(next.CellX, next.CellY - 1, next.CellZ) == Block.Transparent &&
                Program.game_world.GetBlock(next.CellX, next.CellY, next.CellZ) == Block.Transparent)
            {
                cameraPos = next;
            }
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseHolding)
            {
                // Запасной способ, если RAW ввод не работает: сдвиг курсора от центра
                if (!rawMouseInput)
                {
                    RotateCamera(e.X - panel1.Width / 2, e.Y - panel1.Height / 2);
                }
                // Курсор всё равно держим в центре, чтобы он не уходил из окна и клики не улетали мимо.
                // На RAW ввод это не влияет: перенос курсора программой не даёт WM_INPUT.
                Cursor.Position = PointToScreen(new Point(panel1.Width / 2, panel1.Height / 2));
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        // Переводит угол луча относительно камеры (в градусах) в мировые phi/theta (тоже в градусах).
        // Сначала наклон (pitch) вокруг правой оси камеры, потом поворот (yaw) вокруг вертикали.
        private static void CameraToWorldAngles(double localPhiDeg, double localThetaDeg, double cosYaw, double sinYaw, double cosPitch, double sinPitch, out double phiDeg, out double thetaDeg)
        {
            double lp = localPhiDeg / 57.2958;
            double lt = localThetaDeg / 57.2958;
            // Направление в системе камеры: X - вперёд, Y - вверх, Z - вправо
            double x = Math.Cos(lt) * Math.Cos(lp);
            double y = Math.Sin(lt);
            double z = Math.Cos(lt) * Math.Sin(lp);

            // Pitch
            double x1 = x * cosPitch - y * sinPitch;
            double y1 = x * sinPitch + y * cosPitch;

            // Yaw
            double x2 = x1 * cosYaw - z * sinYaw;
            double z2 = x1 * sinYaw + z * cosYaw;

            thetaDeg = Math.Asin(Math.Max(-1, Math.Min(1, y1))) * 57.2958;
            phiDeg = Math.Atan2(z2, x2) * 57.2958;
        }

        // MouseDown, а не MouseClick: MouseClick не приходит на второе нажатие быстрого двойного клика
        // (WinForms шлёт вместо него MouseDoubleClick), и каждый второй быстрый клик терялся
        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            // Лаймовая точка теперь ровно по направлению камеры
            Ray ray = DvcSel.CreateRay(gpurender, cameraAngleLeftRight / multiplier, -cameraAngleTopBottom / multiplier, cameraPos, 10, 0.1f, ee);
            // seeThrough: false - стекло ловится целиком, иначе клик в прозрачную часть пролетал бы насквозь
            // и ломал блок за стеклом
            if (!ray.Raycast(10, out Ray.RaycastHit hit, seeThrough: false))
            {
                Console.WriteLine("bro u are clicking nothing");
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (!hit.HasPrev)
                {
                    return;
                }
                // Не ставим блок туда, где стоит игрок (ноги и голова, как в коллизии)
                long playerX = cameraPos.CellX;
                long playerY = cameraPos.CellY;
                long playerZ = cameraPos.CellZ;
                if (hit.PrevX == playerX && hit.PrevZ == playerZ && (hit.PrevY == playerY || hit.PrevY == playerY - 1))
                {
                    Console.WriteLine("bro u are placing a block into yourself");
                    return;
                }
                Console.WriteLine($"block placed at coordinates {hit.PrevX} {hit.PrevY} {hit.PrevZ}");
                Program.game_world.SetBlock(blockToPlace, hit.PrevX, hit.PrevY, hit.PrevZ);
            }
            else
            {
                Console.WriteLine($"block destroyed at coordinates {hit.X} {hit.Y} {hit.Z}");
                ProcessBreakingSpecialBlocks(hit.X, hit.Y, hit.Z);
                Program.game_world.SetBlock(Block.Transparent, hit.X, hit.Y, hit.Z);
            }

            Task.Run(async delegate ()
            {
                isClicking = true;
                await Task.Delay(250);
                isClicking = false;
            });
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void chunkGen_Tick(object sender, EventArgs e)
        {
            Task.Run(delegate ()
            {
                try
                {
                    Program.game_world.GenerateNeccesaryChunks();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"failed to generate chunks because of a following expection: {ex}");
                }
            });
        }

        private void autosave_Tick(object sender, EventArgs e)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Program.game_world.SaveAllChunks();
            Console.WriteLine($"Autosave: saved all chunks in {stopwatch.ElapsedMilliseconds}ms");
        }

        private void tick_Tick(object sender, EventArgs e)
        {
            Program.game_world.ClientPositions.TryGetValue(fd, out cameraPos);
            Text = $"Meta Tsu-k 3D (X:{cameraPos.X:f3},Y:{cameraPos.Y:f3},Z:{cameraPos.Z:f3},CALR:{cameraAngleLeftRight:f1},CATB:{cameraAngleTopBottom:f1},FPS:{performance:f2}/{renderperformance:f2})";

            // Прошлый вывод ещё не закончился - пропускаем тик, а не ставим в очередь ещё одну задачу
            if (Interlocked.Exchange(ref isPresenting, 1) == 1)
            {
                return;
            }

            Task.Run(delegate ()
            {
                lock (renderLock)
                {
                    try
                    {
                        long timeTicksOnBegin = DateTime.Now.Ticks;
                        /*using (Bitmap bmap = new Bitmap(960, 960))
                        {
                            using (Graphics tmpGraphics = Graphics.FromImage(bmap))
                            {
                                tmpGraphics.DrawImage(new Bitmap(game), new Rectangle(0, 0, 960, 960));
                                tmpGraphics.DrawImage(playerHandCopy, 0, 0);
                            }
                            graphics.DrawImage(bmap, 0, 0);
                        }
                        using (Bitmap tempBitmap = new Bitmap(960, 960))
                        {
                            using (Graphics tempGraphics = Graphics.FromImage(tempBitmap))
                            {
                                tempGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                                double alpha = 1.0f;
                                if (gameLoadTimestamp.ElapsedMilliseconds >= 2500)
                                {
                                    // Плавно уменьшаем прозрачность заставки за 0.5 секунды
                                    float fadeTime = (gameLoadTimestamp.ElapsedMilliseconds - 2500) / 500.0f;
                                    alpha = Math.Max(0.0f, 1.0f - fadeTime);
                                }

                                // Рисуем 3D-сцену (она всегда рендерится, даже под заставкой)

                                graphics.DrawImage(game, new Rectangle(0, 0, 960, 960));

                                // Сверху рисуем заставку с прозрачностью (если ещё не убрали)
                                if (alpha > 0)
                                {
                                    //graphics.DrawImage(ApplyOpacityFilter(TextureStorage.GetTexture("magic_murder_3d.png"), alpha), new Rectangle(0, 0, 960, 960));
                                }
                            }
                            graphics.DrawImage(tempBitmap, 0, 0);
                        }*/
                        if (clearScreen)
                        {
                            clearScreen = false;
                            graphics.Clear(Color.Black);
                        }
                        if (gameLoadTimestamp.ElapsedMilliseconds < 2500)
                        {
                            // Заставка во весь кадр (720x750 было подогнано под ужатое окно)
                            graphics.DrawImage(TextureStorage.GetTexture("magic_murder_3d.png"), CenteredRect(width, height));
                        }
                        else if (newFrameReady)
                        {
                            // Мы и так под renderLock, а рендер трогает game только под ним же,
                            // так что копия кадра (3.7 МБ на каждый тик) не нужна. И выводим только новый кадр,
                            // а не один и тот же по 3 раза.
                            newFrameReady = false;
                            graphics.DrawImage(game, CenteredRect(width, height));

                            long timeTicksOnEnd = DateTime.Now.Ticks;
                            performance = 1m / ((timeTicksOnEnd - timeTicksOnBegin) / (decimal)TimeSpan.TicksPerSecond);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"tick exception: {ex}");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isPresenting, 0);
                    }
                }
            });
        }

        private void Save()
        {
            Program.game_world.SaveAllChunks();

            // Координаты - decimal, его ToString пишет все цифры, и он читается обратно точно.
            // Углы - double, для них G17 (обычный ToString даёт только 15 цифр).
            File.WriteAllText("./gameworld/local.txt", string.Join(" ",
                cameraPos.X.ToString(CultureInfo.InvariantCulture),
                cameraPos.Y.ToString(CultureInfo.InvariantCulture),
                cameraPos.Z.ToString(CultureInfo.InvariantCulture),
                cameraAngleLeftRight.ToString("G17", CultureInfo.InvariantCulture),
                cameraAngleTopBottom.ToString("G17", CultureInfo.InvariantCulture)));
        }
        private static decimal ParseCoordinate(string s)
        {
            s = s.Replace(',', '.');
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d))
            {
                return d;
            }
            // Совсем огромное число из старого double-сохранения: зажмём до края мира
            return double.Parse(s, CultureInfo.InvariantCulture) > 0 ? PlayerPosition.Limit : -PlayerPosition.Limit;
        }

        private void ReadLocalTxt()
        {
            string[] data = File.ReadAllText("./gameworld/local.txt").Split(' ');
            // NumberStyles.Float - чтобы читались и старые сохранения в виде 1.0000000000000001E+15
            cameraPos = new PlayerPosition(
                ParseCoordinate(data[0]),
                ParseCoordinate(data[1]),
                ParseCoordinate(data[2]));
            cameraAngleLeftRight = double.Parse(data[3].Replace(',', '.'), CultureInfo.InvariantCulture);
            cameraAngleTopBottom = double.Parse(data[4].Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W || e.KeyCode == Keys.Up)
            {
                handAnimationIndex = 0;
            }
        }
        /// <summary>
        /// Makes the block preview bigger for 1 second so the player will see the preview better.
        /// </summary>
        private void EnlargeBlockPreview()
        {
            Task.Run(delegate ()
            {
                isBlockPreviewLarge = true;
                Thread.Sleep(1000);
                isBlockPreviewLarge = false;
            });
        }

        // Прямоугольник размером w x h по центру панели. Если панель меньше (окно 942x913),
        // края кадра обрезаются поровну со всех сторон, а не только справа и снизу.
        private Rectangle CenteredRect(int w, int h)
        {
            Size size = panelSize;
            // Если панель меньше кадра (маленький экран), уменьшаем кадр целиком, а не обрезаем края:
            // иначе за краем окажутся рука и превью блока. Больше 1:1 не увеличиваем.
            double scale = Math.Min(1.0, Math.Min((double)size.Width / w, (double)size.Height / h));
            int sw = (int)(w * scale);
            int sh = (int)(h * scale);
            return new Rectangle((size.Width - sw) / 2, (size.Height - sh) / 2, sw, sh);
        }

        private void panel1_Resize(object sender, EventArgs e)
        {
            lock (renderLock)
            {
                // Graphics из CreateGraphics запоминает размер панели на момент создания и дальше
                // обрезает всё, что за ним. После разворота на весь экран его надо создать заново.
                graphics.Dispose();
                graphics = panel1.CreateGraphics();
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                panelSize = panel1.ClientSize;
                clearScreen = true;
                // Перерисовать кадр на новом месте, не дожидаясь следующего
                newFrameReady = true;
            }
        }

        private void DoFullscreenStuff()
        {
            if (fullscreenMode)
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                WindowState = FormWindowState.Normal;
            }
        }
        private void ProcessBreakingSpecialBlocks(long ax, long ay, long az)
        {
            switch (Program.game_world.GetBlock(ax, ay, az))
            {
                case Block.Detonator:
                    ApplyScreenEffects(ScreenEffects.Kaboom, 500);
                    Program.game_world.FillSphere(Block.Transparent, ax, ay, az, 7);
                    break;
                case Block.Nuke:
                    ApplyScreenEffects(ScreenEffects.NuclearApocalypse, 1500);
                    Program.game_world.FillSphere(Block.Transparent, ax, ay, az, 100);
                    break;
            }
        }
        private Bitmap ApplyOpacityFilter(Bitmap b, double a)
        {
            BitmapData bitmapData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] data = new byte[bitmapData.Width * bitmapData.Height * bitmapData.Stride];
            Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
            b.UnlockBits(bitmapData);
            for (int i = 0; i < data.Length; i++)
            {
                if ((i & 3) == 0)
                {
                    data[i] = (byte)(data[i] * a);
                }
            }
            Bitmap result = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
            BitmapData resultBitmapData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(data, 0, resultBitmapData.Scan0, data.Length);
            result.UnlockBits(resultBitmapData);
            return result;
        }
    }
}