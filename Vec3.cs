namespace MetaTSUK
{
    public class Vector3
    {
        public double X;
        public double Y;
        public double Z;

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"Vec3:X={X},Y={Y},Z={Z}";
        }
    }

    /// <summary>
    /// Позиция игрока в decimal: 28-29 значащих цифр, так что даже на 1e18 остаётся ~10 знаков после запятой
    /// (double на 1e15 уже шагает по 0.125). Неизменяемая: чтобы сдвинуться, создаётся новая позиция.
    /// Так поток рендера никогда не прочитает "половину" старого и "половину" нового decimal
    /// (decimal 16 байт и читается не атомарно, а ссылка на объект - атомарно).
    /// </summary>
    public sealed class PlayerPosition
    {
        // Блоки адресуются long, дальше мира нет. Держим запас, чтобы x+1 и т.п. не переполнялись.
        public const long CellLimit = 9_000_000_000_000_000_000L;
        public const decimal Limit = CellLimit;

        public readonly decimal X, Y, Z;

        public PlayerPosition(decimal x, decimal y, decimal z)
        {
            X = Clamp(x); Y = Clamp(y); Z = Clamp(z);
        }

        private static decimal Clamp(decimal v) => v > Limit ? Limit : v < -Limit ? -Limit : v;

        /// <summary>Клетка (целые блоки), в которой стоит точка.</summary>
        public long CellX => (long)decimal.Floor(X);
        public long CellY => (long)decimal.Floor(Y);
        public long CellZ => (long)decimal.Floor(Z);

        /// <summary>Положение внутри клетки, от 0 до 1. Маленькое число, поэтому в double оно точное.</summary>
        public double FracX => (double)(X - decimal.Floor(X));
        public double FracY => (double)(Y - decimal.Floor(Y));
        public double FracZ => (double)(Z - decimal.Floor(Z));

        public PlayerPosition Moved(double dx, double dy, double dz)
        {
            return new PlayerPosition(X + (decimal)dx, Y + (decimal)dy, Z + (decimal)dz);
        }

        public override string ToString()
        {
            return $"Pos:X={X},Y={Y},Z={Z}";
        }
    }
}
