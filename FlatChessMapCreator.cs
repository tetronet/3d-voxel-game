using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaTSUK
{
    public class FlatChessMapCreator : MapCreator
    {
        public override Block GetBlockMaterialByPoint(Vector3 point, long cx, long cy, long cz)
        {
            if (cy > 0)
            {
                return Block.Transparent;
            }
            if ((point.X + point.Z) % 2 == 0)
            {
                return Block.Object_Dark;
            }
            else
            {
                return Block.Object_Light;
            }
        }
    }
}
