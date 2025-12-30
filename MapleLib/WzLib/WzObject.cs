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
                WzObject wzObject = this;
                
                if (wzObject is WzFile)
                {
                    return ((WzFile)this)[name];
                } 
                else if (wzObject is WzDirectory)
                {
                    return ((WzDirectory)this)[name];
                }
                else if (wzObject is WzImage)
                {
                    return ((WzImage)this)[name];
                }
                else if (wzObject is WzImageProperty)
                {
                    return ((WzImageProperty)this)[name];
                }
                else
                {
                    throw new NotImplementedException();
                }
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
                if (parent.GetType() == typeof(WzImage))
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
                
                string result = this.Name;
                WzObject currObj = this;
                while (currObj.Parent != null)
                {
                    currObj = currObj.Parent;
                    result = currObj.Name + @"\" + result;
                }
                return result;
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