using MapleLib.WzLib.Util;
using System.IO;

namespace MapleLib.WzLib.WzProperties
{
    public class WzVideoProperty : WzExtended, IPropertyContainer
    {

        #region Fields
        public const string CANVAS_VIDEO_HEADER = "Canvas#Video";

        internal string name;
        internal WzObject parent;
        internal WzBinaryReader wzReader;
        internal WzPropertyCollection properties;

        internal long _offset;
        internal int _length;
        internal int type;
        internal byte[] _bytes;

        private static void EnsurePayloadAvailable(WzBinaryReader reader, int length, string description)
        {
            if (length < 0 || length > MemoryLimits.MAX_WZ_PAYLOAD_BYTES || !reader.BaseStream.CanSeek)
                throw new InvalidDataException($"Invalid {description} length: {length}.");

            long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining < 0 || length > remaining)
                throw new InvalidDataException($"{description} exceeds the containing stream: {length} bytes.");
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        public WzVideoProperty(string name, WzBinaryReader reader)
        {
            this.name = name;
            this.wzReader = reader;
            this.properties = new WzPropertyCollection(this);
        }

        /// <summary>
        /// Constructor copy
        /// </summary>
        /// <param name="copy"></param>
        private WzVideoProperty(WzVideoProperty copy)
        {
            this.name = copy.name;
            byte[] sourceBytes = copy.GetBytes(false);
            this._bytes = sourceBytes == null ? null : (byte[])sourceBytes.Clone();
            this._length = copy._length;
            this.type = copy.type;
            this.properties = new WzPropertyCollection(this);
            foreach (WzImageProperty property in copy.properties)
                AddProperty(property.DeepClone());
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
        /// Remove a property
        /// </summary>
        /// <param name="name">Name of Property</param>
        public void RemoveProperty(WzImageProperty prop)
        {
            prop.Parent = null;
            properties.Remove(prop);
        }

        /// <summary>
        /// Remove a property by its name
        /// </summary>
        /// <param name="name">Name of Property</param>
        public void RemoveProperty(string propertyName)
        {
            WzImageProperty prop = this[propertyName];
            if (prop != null)
            {
                RemoveProperty(prop);
            }
        }

        /// <summary>
        /// Clears the list of properties
        /// </summary>
        public void ClearProperties()
        {
            foreach (WzImageProperty prop in properties) prop.Parent = null;
            properties.Clear();
        }

        #region Inherited Members
        public override WzImageProperty DeepClone()
        {
            WzVideoProperty clone = new WzVideoProperty(this);
            return clone;
        }

        internal void Parse(bool parseNow)
        {
            type = wzReader.ReadByte();
            _length = wzReader.ReadCompressedInt();
            EnsurePayloadAvailable(wzReader, _length, "Video data");
            _offset = wzReader.BaseStream.Position;
            if (parseNow)
                GetBytes(true);
            else
                wzReader.BaseStream.Position = checked(_offset + _length);
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
            get => parent;
            internal set => parent = value;
        }

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Raw;
        /// <summary>
        /// The properties contained in this property
        /// </summary>
        public override WzPropertyCollection WzProperties
        {
            get
            {
                return properties;
            }
        }
        public override string Name { get => name; set => this.name = value; }

        public override void WriteValue(WzBinaryWriter writer)
        {
            var data = GetBytes(false);
            writer.WriteStringValue(CANVAS_VIDEO_HEADER, WzImage.WzImageHeaderByte_WithoutOffset, WzImage.WzImageHeaderByte_WithOffset);
            writer.Write((byte)0);
            if (properties.Count > 0)
            {
                writer.Write((byte)1);
                WzImageProperty.WritePropertyList(writer, properties);
            }
            else
            {
                writer.Write((byte)0);
            }
            writer.Write((byte)type);
            writer.WriteCompressedInt(data.Length);
            writer.Write(data);
        }

        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag(CANVAS_VIDEO_HEADER, this.Name, false, false));
            WzImageProperty.DumpPropertyList(writer, level, this.WzProperties);
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag(CANVAS_VIDEO_HEADER));
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            this.name = null;
            this._bytes = null;
            foreach (WzImageProperty prop in properties)
            {
                prop.Dispose();
            }
            properties.Clear();
            properties = null;
        }
        #endregion

        public byte[] GetBytes(bool saveInMemory)
        {
            if (this._bytes != null) // check in-memory
                return this._bytes;

            if (this.wzReader == null)
                return null;

            // read if none; the reader is shared by all lazy properties in an
            // image, so serialize cursor movement and restore it atomically.
            lock (wzReader)
            {
                var currentPos = wzReader.BaseStream.Position;
                try
                {
                    if (_offset < 0 || _offset > wzReader.BaseStream.Length)
                        throw new InvalidDataException("Video data offset is outside the containing stream.");

                    this.wzReader.BaseStream.Position = _offset;
                    EnsurePayloadAvailable(wzReader, _length, "Video data");
                    this._bytes = wzReader.ReadBytes(_length);
                    if (this._bytes.Length != _length)
                        throw new InvalidDataException("Video data is truncated.");
                }
                finally
                {
                    this.wzReader.BaseStream.Position = currentPos;
                }
            }
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
    }
}
