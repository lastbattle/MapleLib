using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.Helpers
{
    public class StringUtility
    {
        public static string CapitalizeFirstCharacter(string x)
        {
            if (x.Length > 0 && char.IsLower(x[0]))
            {
                return string.Create(x.Length, x, static (destination, source) =>
                {
                    destination[0] = char.ToUpper(source[0]);
                    source.AsSpan(1).CopyTo(destination[1..]);
                });
            }
            return x;
        }
    }
}
