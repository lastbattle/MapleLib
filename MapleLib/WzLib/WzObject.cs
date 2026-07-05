using System;
using System.Drawing;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.WzLib
{
	/// <summary>
	/// An abstract class for wz objects
	/// </summary>
	public abstract class WzObject : IDisposable
	{
        private object hcTag = null;
        private object hcTag_spine = null;
        private object msTag = null;
        private object msTag_spine = null;
        private object tag3 = null;

		public abstract void Dispose();

		/// <summary>
		/// The name of the object
		/// </summary>
		public abstract string Name { get; set; }
		/// <summary>
		/// The WzObjectType of the object
		/// </summary>
		public abstract WzObjectType ObjectType { get; }
		/// <summary>
		/// Returns the parent object
		/// </summary>
		public abstract WzObject Parent { get; internal set; }
        /// <summary>
        /// Returns the parent WZ File
        /// </summary>
        public abstract WzFile WzFileParent { get; }

        public WzObject this[string name]
        {
            get
            {
                return this switch
                {
                    WzFile file => file[name],
                    WzDirectory directory => directory[name],
                    WzImage image => image[name],
                    WzImageProperty property => property[name],
                    _ => throw new NotImplementedException()
                };
            }
        }


        /// <summary>
        /// Gets the top most WZObject directory (i.e Map.wz, Skill.wz)
        /// </summary>
        /// <returns></returns>
        public WzObject GetTopMostWzDirectory()
        {
            WzObject parent = this.Parent;
            if (parent == null)
                return this; // this

            while (parent.Parent != null )
            {
                parent = parent.Parent;
            }
            return parent;
        }

        /// <summary>
        /// Gets the top most WzImage from the current directory (usually just 1 directory away from .wz file)
        /// </summary>
        /// <returns></returns>
        public WzObject GetTopMostWzImage() {
            WzObject parent = this.Parent;
            if (parent == null)
                return this; // this

            while (parent.Parent != null) {
                parent = parent.Parent;
                if (parent is WzImage)
                    return parent;
            }
            return parent;
        }

        public string FullPath
        {
            get
            {
                if (this is WzFile file) 
                    return file.WzDirectory.Name;

                WzObject parent = Parent;
                if (parent == null)
                    return Name;
                WzObject grandParent = parent.Parent;
                if (grandParent == null)
                    return $"{parent.Name}\\{Name}";
                WzObject greatGrandParent = grandParent.Parent;
                if (greatGrandParent == null)
                    return $"{grandParent.Name}\\{parent.Name}\\{Name}";
                if (greatGrandParent.Parent == null)
                    return $"{greatGrandParent.Name}\\{grandParent.Name}\\{parent.Name}\\{Name}";

                int length = Name?.Length ?? 0;
                WzObject current = this;
                while (current.Parent != null)
                {
                    current = current.Parent;
                    length += 1 + (current.Name?.Length ?? 0);
                }

                Span<char> destination = length <= 512 ? stackalloc char[length] : new char[length];
                int writeAt = destination.Length;
                current = this;
                while (true)
                {
                    string currentName = current.Name ?? string.Empty;
                    writeAt -= currentName.Length;
                    currentName.AsSpan().CopyTo(destination.Slice(writeAt));
                    current = current.Parent;
                    if (current == null)
                        break;
                    destination[--writeAt] = '\\';
                }
                return new string(destination);
            }
        }

        /// <summary>
        /// Used in HaCreator to save already parsed images
        /// </summary>
        public virtual object HCTag
        {
            get { return hcTag; }
            set { hcTag = value; }
        }


        /// <summary>
        /// Used in HaCreator to save already parsed spine images
        /// </summary>
        public virtual object HCTagSpine
        {
            get { return hcTag_spine; }
            set { hcTag_spine = value; }
        }

        /// <summary>
        /// Used in HaCreator's MapSimulator to save already parsed textures
        /// </summary>
        public virtual object MSTag
        {
            get { return msTag; }
            set { msTag = value; }
        }

        /// <summary>
        /// Used in HaCreator's MapSimulator to save already parsed spine objects
        /// </summary>
        public virtual object MSTagSpine
        {
            get { return msTag_spine; }
            set { msTag_spine = value; }
        }

        /// <summary>
        /// Used in HaRepacker to save WzNodes
        /// </summary>
        public virtual object HRTag
        {
            get { return tag3; }
            set { tag3 = value; }
        }

        public virtual object WzValue { get { return null; } }

        public abstract void Remove();

        //Credits to BluePoop for the idea of using cast overriding
        //2015 - That is the worst idea ever, removed and replaced with Get* methods
        #region Cast Values
        public virtual int GetInt()
        {
            throw new NotImplementedException();
        }

        public virtual short GetShort()
        {
            throw new NotImplementedException();
        }

        public virtual long GetLong()
        {
            throw new NotImplementedException();
        }

        public virtual float GetFloat()
        {
            throw new NotImplementedException();
        }

        public virtual double GetDouble()
        {
            throw new NotImplementedException();
        }

        public virtual string GetString()
        {
            throw new NotImplementedException();
        }

        public virtual Point GetPoint()
        {
            throw new NotImplementedException();
        }

        public virtual Bitmap GetBitmap()
        {
            throw new NotImplementedException();
        }

        public virtual byte[] GetBytes()
        {
            throw new NotImplementedException();
        }
        #endregion

	}
}
