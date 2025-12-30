using System.IO;
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib.WzProperties
{
    public class WzLongProperty : WzImageProperty
    {
        #region Fields
        internal string name;
        internal long val;
        internal WzObject parent;
        //internal WzImage imgParent;
        #endregion

        #region Inherited Members
        public override void SetValue(object value)
        {
            val = System.Convert.ToInt64(value);
        }

        public override WzImageProperty DeepClone()
        {
            WzLongProperty clone = new WzLongProperty(name, val);
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
        public override WzPropertyType PropertyType { get { return WzPropertyType.Long; } }
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        public override void WriteValue(MapleLib.WzLib.Util.WzBinaryWriter writer)
        {
            writer.Write((byte)20);
            writer.WriteCompressedLong(Value);
        }
        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedValuePair("WzLong", this.Name, this.Value.ToString()));
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
        public long Value { get { return val; } set { val = (long) value; } }
        /// <summary>
        /// Creates a blank WzCompressedIntProperty
        /// </summary>
        public WzLongProperty() { }
        /// <summary>
        /// Creates a WzCompressedIntProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        public WzLongProperty(string name)
        {
            this.name = name;
        }
        /// <summary>
        /// Creates a WzCompressedIntProperty with the specified name and value
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="value">The value of the property</param>
        public WzLongProperty(string name, long value)
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
            return (double)val;
        }

        public override long GetLong()
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

        public override string ToString()
        {
            return val.ToString();
        }
        #endregion
    }
}