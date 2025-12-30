using System.IO;
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib.WzProperties
{
	/// <summary>
	/// A property that has the value of a double
	/// </summary>
	public class WzDoubleProperty : WzImageProperty
	{
		#region Fields
		internal string name;
		internal double val;
		internal WzObject parent;
		//internal WzImage imgParent;
		#endregion

		#region Inherited Members
        public override void SetValue(object value)
        {
            val = System.Convert.ToDouble(value);
        }

        public override WzImageProperty DeepClone()
        {
            WzDoubleProperty clone = new WzDoubleProperty(name, val);
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
		public override WzPropertyType PropertyType { get { return WzPropertyType.Double; } }
		/// <summary>
		/// The name of this property
		/// </summary>
		public override string Name { get { return name; } set { name = value; } }
		public override void WriteValue(MapleLib.WzLib.Util.WzBinaryWriter writer)
		{
			writer.Write((byte)5);
			writer.Write(Value);
		}
		public override void ExportXml(StreamWriter writer, int level)
		{
			writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedValuePair("WzDouble", this.Name, this.Value.ToString()));
		}
		public override void Dispose()
		{
			name = null;
		}
		#endregion

		#region Custom Members
		/// <summary>
		/// The value of this property
		/// </summary>
		public double Value { get { return val; } set { val = (double) value; } }
		/// <summary>
		/// Creates a blank WzDoubleProperty
		/// </summary>
		public WzDoubleProperty() { }
		/// <summary>
		/// Creates a WzDoubleProperty with the specified name
		/// </summary>
		/// <param name="name">The name of the property</param>
		public WzDoubleProperty(string name)
		{
			this.name = name;
		}
		/// <summary>
		/// Creates a WzDoubleProperty with the specified name and value
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public WzDoubleProperty(string name, double value)
		{
			this.name = name;
			this.val = value;
		}
		#endregion

        #region Cast Values
        public override float GetFloat()
        {
            return (float)val;
        }

        public override double GetDouble()
        {
            return val;
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