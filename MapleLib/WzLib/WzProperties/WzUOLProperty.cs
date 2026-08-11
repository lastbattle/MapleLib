//uncomment to enable automatic UOL resolving, comment to disable it
#define UOLRES

using System.IO;
using System.Collections.Generic;
using MapleLib.WzLib.Util;
using System.Drawing;
using System;

namespace MapleLib.WzLib.WzProperties
{
	/// <summary>
	/// A property that's value is a string
	/// </summary>
    public class WzUOLProperty : WzExtended
	{
		#region Fields
		internal string name, val;
		internal WzObject parent;
		//internal WzImage imgParent;
		internal WzObject linkVal;
		#endregion

		#region Inherited Members
        public override void SetValue(object value)
        {
            val = (string)value;
        }

        public override WzImageProperty DeepClone()
        {
            WzUOLProperty clone = new WzUOLProperty(name, val);
            clone.linkVal = null;
            return clone;
        }

		public override object WzValue
        {
            get
            {
#if UOLRES
                return LinkValue;
#else
                return this;
#endif
            }
        }
		/// <summary>
		/// The parent of the object
		/// </summary>
		public override WzObject Parent { get { return parent; } internal set { parent = value; } }

		/*/// <summary>
		/// The image that this property is contained in
		/// </summary>
		public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/

		/// <summary>
		/// The name of the property
		/// </summary>
		public override string Name { get { return name; } set { name = value; } }

#if UOLRES
		public override WzPropertyCollection WzProperties
		{
			get
			{
				return ResolveLinkValue() is WzImageProperty property ? property.WzProperties : null;
			}
		}


        public override WzImageProperty this[string name]
		{
			get
			{

                WzObject target = ResolveLinkValue();
                return target is WzImageProperty property ? property[name] : target is WzImage image ? image[name] : null;
			}
		}

		public override WzImageProperty GetFromPath(string path)
		{
			WzObject target = ResolveLinkValue();
			return target is WzImageProperty property ? property.GetFromPath(path) : target is WzImage image ? image.GetFromPath(path) : null;
		}
#endif

		/// <summary>
		/// The WzPropertyType of the property
		/// </summary>
		public override WzPropertyType PropertyType { get { return WzPropertyType.UOL; } }

		public override void WriteValue(WzBinaryWriter writer)
		{
			writer.WriteStringValue("UOL", WzImage.WzImageHeaderByte_WithoutOffset, WzImage.WzImageHeaderByte_WithOffset);
			writer.Write((byte)0);
			writer.WriteStringValue(Value, 0, 1);
		}

		public override void ExportXml(StreamWriter writer, int level)
		{
			writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedValuePair("WzUOL", this.Name, this.Value));
		}

		/// <summary>
		/// Disposes the object
		/// </summary>
		public override void Dispose()
		{
			name = null;
			val = null;
		}
		#endregion

		#region Custom Members
		/// <summary>
		/// The value of the property
		/// </summary>
		public string Value { get { return val; } set { val = value; } }

#if UOLRES
        public WzObject LinkValue {
            get {
                if (linkVal == null) {
                    if (string.IsNullOrEmpty(val) || parent == null)
                        return null;
                    string[] paths = val.Split('/');

                    if (paths.Length >= 1) {
                        if (paths[0] != "..") {
                            // if it doesnt start with "..", means the first directory is the top directory.
                            linkVal = this.GetTopMostWzImage(); // Npc.wz/9310019.img
                        } else {
                            linkVal = (WzObject)this.parent;
                        }
                        string fullPath = parent.FullPath;
                        foreach (string path in paths) {
                            if (linkVal == null)
                                return null;
                            if (path == "..") {
                                linkVal = linkVal.Parent;
                            }
                            else {
                                if (linkVal is WzImageProperty)
                                    linkVal = ((WzImageProperty)linkVal)[path];
                                else if (linkVal is WzImage image)
                                    linkVal = image[path];
                                else if (linkVal is WzDirectory directory) {
                                    if (path.EndsWith(".img"))
                                        linkVal = directory[path];
                                    else
                                        linkVal = directory[path + ".img"];
                                }
                                else {
                                    MapleLib.Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "UOL got nexon'd at property: " + this.FullPath);
                                    return null;
                                }
                            }
                        }
                    }
                }
                return linkVal;
            }
        }

        private WzObject ResolveLinkValue()
        {
            WzObject current = this;
            HashSet<WzObject> visited = new HashSet<WzObject>();
            while (current is WzUOLProperty uol)
            {
                if (!visited.Add(current))
                    throw new InvalidDataException("Cyclic UOL link detected.");

                current = uol.LinkValue;
                if (current == null)
                    return null;
            }
            return current;
        }
#endif

        /// <summary>
        /// Creates a blank WzUOLProperty
        /// </summary>
        public WzUOLProperty() { }

		/// <summary>
		/// Creates a WzUOLProperty with the specified name
		/// </summary>
		/// <param name="name">The name of the property</param>
		public WzUOLProperty(string name)
		{
			this.name = name;
		}

		/// <summary>
		/// Creates a WzUOLProperty with the specified name and value
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public WzUOLProperty(string name, string value)
		{
			this.name = name;
			this.val = value;
		}
		#endregion

        #region Cast Values
#if UOLRES
        public override int GetInt()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetInt();
        }

        public override short GetShort()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetShort();
        }

        public override long GetLong()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetLong();
        }

        public override float GetFloat()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetFloat();
        }

        public override double GetDouble()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetDouble();
        }

        public override string GetString()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetString();
        }

        public override Point GetPoint()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetPoint();
        }

        public override Bitmap GetBitmap()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetBitmap();
        }

        public override byte[] GetBytes()
        {
            return (ResolveLinkValue() ?? throw new InvalidDataException("UOL link target could not be resolved.")).GetBytes();
        }
#else
        public override string GetString()
        {
            return val;
        }
#endif
        public override string ToString()
        {
            return val;
        }
        #endregion
    }
}
