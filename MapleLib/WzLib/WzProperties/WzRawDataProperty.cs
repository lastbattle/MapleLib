using MapleLib.WzLib.Util;
using System.IO;

namespace MapleLib.WzLib.WzProperties
{
    public class WzRawDataProperty : WzExtended, IPropertyContainer
    {

        #region Fields
        public const string RAW_DATA_HEADER = "RawData";

        internal string _name;
        internal WzObject _parent;
        internal WzBinaryReader _wzReader;

        internal byte _type;
        internal long _rawDataOffset; // 
        internal int _length;
        internal byte[] _bytes;
        internal WzPropertyCollection properties;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        public WzRawDataProperty(string name, WzBinaryReader reader, byte type)
        {
            this._name = name;
            this._wzReader = reader;
            this._type = type;
            this.properties = new WzPropertyCollection(this);
        }

        #region Inherited Members
        public override WzImageProperty DeepClone()
        {
            var clone = new WzRawDataProperty(_name, null, _type);
            foreach (WzImageProperty prop in properties)
            {
                clone.AddProperty(prop.DeepClone());
            }
            clone._length = _length;
            clone._bytes = new byte[_length]; 
            GetBytes(false).CopyTo(clone._bytes, 0);

            return clone;
        }

        public override object WzValue => GetBytes(false);

        public override void SetValue(object value)
        {
        }

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent
        {
            get => _parent;
            internal set => _parent = value;
        }

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Raw;

        public override string Name { get =>  _name; set => this._name = value; }

        public override WzPropertyCollection WzProperties => properties;

        public override void WriteValue(WzBinaryWriter writer)
        {
            var data = GetBytes(false);
            writer.WriteStringValue(RAW_DATA_HEADER, WzImage.WzImageHeaderByte_WithoutOffset,
                WzImage.WzImageHeaderByte_WithOffset);
            writer.Write(_type);
            if (_type == 1)
            {
                if (properties.Count > 0)
                {
                    writer.Write((byte)1);
                    WritePropertyList(writer, properties);
                }
                else
                {
                    writer.Write((byte)0);
                }
            }
            writer.WriteCompressedInt(data.Length);
            writer.Write(data);
        }

        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.Write(XmlUtil.Indentation(level));
            writer.WriteLine(XmlUtil.EmptyNamedTag(RAW_DATA_HEADER, Name));
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            this._name = null;
            this._bytes = null;
            foreach (WzImageProperty prop in properties)
            {
                prop.Dispose();
            }
            properties.Clear();
            properties = null;
        }
        #endregion

        internal void Parse(bool parseNow)
        {
            _length = _wzReader.ReadCompressedInt();
            _rawDataOffset = _wzReader.BaseStream.Position;
            if (parseNow)
                GetBytes(true);
            else
                _wzReader.BaseStream.Position = _rawDataOffset + _length;
        }

        public byte[] GetBytes(bool saveInMemory)
        {
            if (this._bytes != null) // check in-memory
                return this._bytes;

            if (this._wzReader == null)
                return null;

            // read if none
            var currentPos = _wzReader.BaseStream.Position;
            this._wzReader.BaseStream.Position = _rawDataOffset;
            this._bytes = _wzReader.ReadBytes(_length);
            this._wzReader.BaseStream.Position = currentPos;
            if (saveInMemory)
            {
                return this._bytes;
            }
            else
            {
                byte[] ret_bytes = _bytes;
                this._bytes = null;
                return ret_bytes;
            }
        }

        /// <summary>
        /// Adds a property to the property list of this property
        /// </summary>
        /// <param name="prop">The property to add</param>
        public void AddProperty(WzImageProperty prop)
        {
            prop.Parent = this;
            properties.Add(prop);
        }

        public void AddProperties(WzPropertyCollection props)
        {
            foreach (WzImageProperty prop in props)
            {
                AddProperty(prop);
            }
        }

        /// <summary>
        /// Remove a property by its name
        /// </summary>
        /// <param name="name">Name of Property</param>
        public void RemoveProperty(string propertyName)
        {
            WzImageProperty prop = this[propertyName];
            if (prop != null) {
                RemoveProperty(prop);
            }
        }

        /// <summary>
        /// Remove a property
        /// </summary>
        public void RemoveProperty(WzImageProperty prop)
        {
            prop.Parent = null;
            properties.Remove(prop);
        }

        /// <summary>
        /// Clears the list of properties
        /// </summary>
        public void ClearProperties()
        {
            foreach (WzImageProperty prop in properties) prop.Parent = null;
            properties.Clear();
        }
    }
}
