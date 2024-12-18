/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib
{
    /// <summary>
    /// Sets the parent node automagically when inserting into the List<WzImageProperty>
    /// </summary>
    public class WzPropertyCollection : List<WzImageProperty>
    {
        private WzObject parent;

        /// <summary>
        /// Constructor without a parent
        /// </summary>
        /*public WzPropertyCollection()
        {
        }*/

        /// <summary>
        /// Constructor with parent
        /// </summary>
        /// <param name="parent"></param>
        public WzPropertyCollection(WzObject parent)
        {
            this.parent = parent;
        }

        public new void Add(WzImageProperty item)
        {
            if (parent != null)
                item.Parent = parent;
            base.Add(item);
        }

        public new void AddRange(IEnumerable<WzImageProperty> collection)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Override other methods as needed, such as Insert, to ensure Parent is set
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public new void Insert(int index, WzImageProperty item)
        {
            if (parent != null)
                item.Parent = parent;
            base.Insert(index, item);
        }
    }
}