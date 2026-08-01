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

namespace MapleLib.WzLib
{
    /// <summary>
    /// Ordered property storage with automatic parent assignment and a
    /// case-insensitive name index for the common child lookup path.
    /// </summary>
    /// <remarks>
    /// The class intentionally remains a <see cref="List{T}"/> subclass for
    /// source compatibility.  Mutating through the concrete collection keeps
    /// the index and parent links synchronized; callers should use the
    /// collection's methods rather than casting it to the base List type.
    /// </remarks>
    public class WzPropertyCollection : List<WzImageProperty>, IList<WzImageProperty>
    {
        private readonly WzObject parent;
        private readonly Dictionary<string, NameIndexEntry> nameIndex =
            new(StringComparer.OrdinalIgnoreCase);
        private NameIndexEntry? nullNameEntry;

        private struct NameIndexEntry
        {
            internal WzImageProperty First;
            internal int DuplicateCount;

            internal NameIndexEntry(WzImageProperty first)
            {
                First = first;
            }
        }

        public WzPropertyCollection(WzObject parent)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Finds the first property with the requested name, preserving the
        /// original list-order behavior when duplicate names are present.
        /// </summary>
        public WzImageProperty this[string name]
        {
            get { return FindByName(name); }
        }

        /// <summary>
        /// Case-insensitive lookup used by WzImage and property-container
        /// indexers.  A linear fallback repairs the index if a caller changed
        /// a property's public Name after insertion.
        /// </summary>
        public WzImageProperty FindByName(string name)
        {
            if (name == null)
                return nullNameEntry?.First;

            if (nameIndex.TryGetValue(name, out NameIndexEntry entry))
            {
                WzImageProperty indexed = entry.First;
                if (string.Equals(indexed?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return indexed;

                RebuildIndex();
                return nameIndex.TryGetValue(name, out entry) ? entry.First : null;
            }

            // Name is mutable on every WzImageProperty.  The collection has
            // no setter callback, so only a miss needs this compatibility
            // fallback; stable hot-path hits remain dictionary probes.
            for (int i = 0; i < Count; i++)
            {
                WzImageProperty property = base[i];
                if (string.Equals(property?.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    RebuildIndex();
                    return property;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first property using an explicit comparison.  This is
        /// used by path APIs whose historical behavior is case-sensitive.
        /// </summary>
        public WzImageProperty Find(string name, StringComparison comparison)
        {
            if (comparison == StringComparison.OrdinalIgnoreCase)
                return FindByName(name);

            for (int i = 0; i < Count; i++)
            {
                WzImageProperty property = base[i];
                if (string.Equals(property?.Name, name, comparison))
                    return property;
            }

            return null;
        }

        public new void Add(WzImageProperty item)
        {
            if (parent != null && item != null)
                item.Parent = parent;

            base.Add(item);
            AddToIndex(item);
        }

        public new void AddRange(IEnumerable<WzImageProperty> collection)
        {
            foreach (WzImageProperty item in collection)
                Add(item);
        }

        public new void Insert(int index, WzImageProperty item)
        {
            if (parent != null && item != null)
                item.Parent = parent;

            base.Insert(index, item);
            RebuildIndex();
        }

        public new void InsertRange(int index, IEnumerable<WzImageProperty> collection)
        {
            foreach (WzImageProperty item in collection)
                Insert(index++, item);
        }

        public new WzImageProperty this[int index]
        {
            get { return base[index]; }
            set
            {
                WzImageProperty previous = base[index];
                if (previous != null)
                    previous.Parent = null;
                if (parent != null && value != null)
                    value.Parent = parent;

                base[index] = value;
                RebuildIndex();
            }
        }

        public new bool Remove(WzImageProperty item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        public new void RemoveAt(int index)
        {
            WzImageProperty item = base[index];
            if (item != null)
                item.Parent = null;

            base.RemoveAt(index);
            RemoveFromIndex(item);
        }

        public new void RemoveRange(int index, int count)
        {
            for (int i = index; i < index + count; i++)
            {
                WzImageProperty item = base[i];
                if (item != null)
                    item.Parent = null;
            }

            base.RemoveRange(index, count);
            RebuildIndex();
        }

        public new int RemoveAll(Predicate<WzImageProperty> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            int removed = 0;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (match(base[i]))
                {
                    WzImageProperty item = base[i];
                    if (item != null)
                        item.Parent = null;
                    base.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
                RebuildIndex();
            return removed;
        }

        public new void Clear()
        {
            for (int i = 0; i < Count; i++)
            {
                WzImageProperty item = base[i];
                if (item != null)
                    item.Parent = null;
            }

            base.Clear();
            nameIndex.Clear();
            nullNameEntry = null;
        }

        public new void Reverse()
        {
            base.Reverse();
            RebuildIndex();
        }

        public new void Reverse(int index, int count)
        {
            base.Reverse(index, count);
            RebuildIndex();
        }

        public new void Sort()
        {
            base.Sort();
            RebuildIndex();
        }

        public new void Sort(IComparer<WzImageProperty> comparer)
        {
            base.Sort(comparer);
            RebuildIndex();
        }

        public new void Sort(Comparison<WzImageProperty> comparison)
        {
            base.Sort(comparison);
            RebuildIndex();
        }

        public new void Sort(int index, int count, IComparer<WzImageProperty> comparer)
        {
            base.Sort(index, count, comparer);
            RebuildIndex();
        }

        private void AddToIndex(WzImageProperty item)
        {
            if (item == null)
                return;

            string name = item.Name;
            if (name == null)
            {
                if (nullNameEntry == null)
                    nullNameEntry = new NameIndexEntry(item);
                else
                {
                    NameIndexEntry nullEntry = nullNameEntry.Value;
                    nullEntry.DuplicateCount++;
                    nullNameEntry = nullEntry;
                }
                return;
            }

            if (nameIndex.TryGetValue(name, out NameIndexEntry entry))
            {
                entry.DuplicateCount++;
                nameIndex[name] = entry;
            }
            else
                nameIndex.Add(name, new NameIndexEntry(item));
        }

        private void RebuildIndex()
        {
            nameIndex.Clear();
            nullNameEntry = null;
            for (int i = 0; i < Count; i++)
                AddToIndex(base[i]);
        }

        private void RemoveFromIndex(WzImageProperty item)
        {
            RemoveFromIndex(item, indexWasRebuilt: false);
        }

        private void RemoveFromIndex(WzImageProperty item, bool indexWasRebuilt)
        {
            if (item == null)
                return;

            string name = item.Name;
            if (name == null)
            {
                if (!nullNameEntry.HasValue)
                    return;

                NameIndexEntry nullEntry = nullNameEntry.Value;

                if (!ReferenceEquals(nullEntry.First, item))
                {
                    if (nullEntry.DuplicateCount > 0)
                    {
                        nullEntry.DuplicateCount--;
                        nullNameEntry = nullEntry;
                        return;
                    }

                    // The public Name setter can invalidate the index. A
                    // rebuild is only needed on this uncommon stale path.
                    if (indexWasRebuilt || IndexOf(item) < 0)
                        return;

                    RebuildIndex();
                    RemoveFromIndex(item, indexWasRebuilt: true);
                    return;
                }

                if (nullEntry.DuplicateCount == 0)
                {
                    nullNameEntry = null;
                    return;
                }

                nullEntry.DuplicateCount--;
                nullEntry.First = FindFirstByName(null);
                nullNameEntry = nullEntry;
                return;
            }

            if (!nameIndex.TryGetValue(name, out NameIndexEntry entry))
            {
                // The public Name setter can invalidate the index. A
                // rebuild is only needed on this uncommon stale path.
                if (indexWasRebuilt || IndexOf(item) < 0)
                    return;

                RebuildIndex();
                RemoveFromIndex(item, indexWasRebuilt: true);
                return;
            }

            if (!ReferenceEquals(entry.First, item))
            {
                if (entry.DuplicateCount > 0)
                {
                    entry.DuplicateCount--;
                    nameIndex[name] = entry;
                    return;
                }

                if (indexWasRebuilt || IndexOf(item) < 0)
                    return;

                RebuildIndex();
                RemoveFromIndex(item, indexWasRebuilt: true);
                return;
            }

            if (entry.DuplicateCount == 0)
            {
                nameIndex.Remove(name);
                return;
            }

            entry.DuplicateCount--;
            entry.First = FindFirstByName(name);
            nameIndex[name] = entry;
        }

        private WzImageProperty FindFirstByName(string name)
        {
            for (int i = 0; i < Count; i++)
            {
                WzImageProperty property = base[i];
                if (string.Equals(property?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property;
            }

            return null;
        }

        // Re-implement the mutable generic interfaces as well as the concrete
        // methods.  This keeps parent/index state correct when a caller holds
        // the collection through IList/ICollection instead of its concrete
        // WzPropertyCollection type.  A cast to the base List type cannot be
        // intercepted by a List subclass and remains unsupported by design.
        void ICollection<WzImageProperty>.Add(WzImageProperty item) => Add(item);
        void ICollection<WzImageProperty>.Clear() => Clear();
        bool ICollection<WzImageProperty>.Remove(WzImageProperty item) => Remove(item);
        void IList<WzImageProperty>.Insert(int index, WzImageProperty item) => Insert(index, item);
        void IList<WzImageProperty>.RemoveAt(int index) => RemoveAt(index);
        WzImageProperty IList<WzImageProperty>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }
    }
}
