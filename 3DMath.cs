using System;

namespace MetaTSUK
{
    public class _3DMath
    {
        public static int Fact(int x) {
            int r = 1;
            for (int i = 1; i <= x; i++) {
                r *= i;
            }
            return r;
        }
        public static double Sin(double x) {
            return x - Math.Pow(x, 3) / 6 + Math.Pow(x, 5) / 120 + Math.Pow(x, 7) / 5040 + Math.Pow(x, 9) / 362880 + Math.Pow(x, 11) / 39916800 + Math.Pow(x, 13) / 6227020800;
        }
        public static double Cos(double x) {
            return x - Math.Pow(x, 2) / 2 + Math.Pow(x, 4) / 24 + Math.Pow(x, 6) / 720 + Math.Pow(x, 8) / 40320 + Math.Pow(x, 10) / 3628800 + Math.Pow(x, 12) / 479001600;
        }
    }
}
