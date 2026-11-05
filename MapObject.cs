using System.Numerics;

namespace MetaTSUK
{
    public abstract class MapObject
    {
        Block color;

        abstract protected bool isPointInObject(Vector3 point);

        public Block GetPointColor(Vector3 point)
        {
            return isPointInObject(point)
                ? color
                : Block.Transparent;
        }
    }
}
