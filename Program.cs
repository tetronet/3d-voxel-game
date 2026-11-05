using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MetaTSUK
{
    internal static class Program
    {
        //public static Dictionary<int, Dictionary<int, Dictionary<int, Block>>> game_world = new Dictionary<int, Dictionary<int, Dictionary<int, Block>>>();
        public static Universe game_world = new Universe("./gameworld/", new FlatChessMapCreator());
        public static GPUMath GPUMath = new GPUMath();
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
