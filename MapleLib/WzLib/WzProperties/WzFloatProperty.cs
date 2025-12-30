using System.IO;
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib.WzProperties
{
	/// <summary>
	/// A property that is stored in the wz file with a byte and possibly followed by a float. If the 
	/// byte is 0, the value is 0, else the value is the float that follows.
	/// </summary>
	public class WzFloatProperty : WzImageProperty
	{
		#region Fields
		internal string name;
		internal float val;
		internal WzObject parent;
		//internal WzImage imgParent;
		#endregion

		#region Inherited Members
        public override void SetValue(object value)
        {
            val = System.Convert.ToSingle(value);
        }

        public override WzImageProperty DeepClone()
        {
            WzFloatProperty clone = new WzFloatProperty(name, val);
            return clone;
        }

		public override object WzValue { get { return Value; } }
		/// <summary>
		/// The parent of the object
		/// </summary>
		public override WzObject Parent { get { return parent; } internal set { parent = value; } }
		/*/// <summary>
		/// The image that this property is contained in
		/// </summary>
		public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
		/// <summary>
		/// The WzPropertyType of the property
		/// </summary>
		public override WzPropertyType PropertyType { get { return WzPropertyType.Float; } }
		/// <summary>
		/// The name of the property
		/// </summary>
		public override string Name { get { return name; } set { name = value; } }
		public override void WriteValue(WzBinaryWriter writer)
		{
			writer.Write((byte)4);
			if (Value == 0f)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.Write((byte)0x80);
				writer.Write(Value);
			}
		}
		public override void ExportXml(StreamWriter writer, int level)
		{
			writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedValuePair("WzByteFloat", this.Name, this.Value.ToString()));
		}
		/// <summary>
		/// Dispose the object
		/// </summary>
		public override void Dispose()
		{
			name = null;
		}
		#endregion

		#region Custom Members
		/// <summary>
		/// The value of the property
		/// </summary>
		public float Value { get { return val; } set { val = (float) value; } }
		/// <summary>
		/// Creates a blank WzByteFloatProperty
		/// </summary>
		public WzFloatProperty() { }
		/// <summary>
		/// Creates a WzByteFloatProperty with the specified name
		/// </summary>
		/// <param name="name">The name of the property</param>
		public WzFloatProperty(string name)
		{
			this.name = name;
		}
		/// <summary>
		/// Creates a WzByteFloatProperty with the specified name and value
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public WzFloatProperty(string name, float value)
		{
			this.name = name;
			this.val = value;
		}
		#endregion

        #region Cast Values
        public override float GetFloat()
        {
            return val;
        }

        public override double GetDouble()
        {
            return (double)val;
        }

        public override int GetInt()
        {
            return (int)val;
        }

        public override short GetShort()
        {
            return (short)val;
        }

        public override long GetLong()
        {
            return (long)val;
        }

        public override string ToString()
        {
            return val.ToString();
        }
        #endregion
    }
}