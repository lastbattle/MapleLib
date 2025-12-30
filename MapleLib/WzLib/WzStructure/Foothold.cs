using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib.WzStructure
{
    public struct Foothold
    {
        public int x1, x2, y1, y2;
        public int prev, next;
        public int num, layer;

        public Foothold(int x1, int x2, int y1, int y2, int num, int layer)
        {
            this.x1 = x1;
            this.x2 = x2;
            this.y1 = y1;
            this.y2 = y2;
            next = 0;
            prev = 0;
            this.num = num;
            this.layer = layer;
        }

        public bool IsWall()
        {
            return x1 == x2;
        }
    }
}
