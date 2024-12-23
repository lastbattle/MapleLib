using MapleLib.WzLib.Util;
using System.IO;

namespace MapleLib.WzLib.WzProperties
{
    public class WzRawDataProperty : WzExtended
    {

        #region Fields
        public const string RAW_DATA_HEADER = "RawData";

        internal string _name;
        internal WzObject _parent;
        internal WzBinaryReader _wzReader;

        internal long _offset;
        internal int _length;
        internal byte[] _bytes;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        public WzRawDataProperty(string name, WzBinaryReader reader, bool parseNow)
        {
            this._name = name;
            this._wzReader = reader;

            this._wzReader.BaseStream.Position++;
            this._length = reader.ReadInt32();
            this._offset = reader.BaseStream.Position;
            if (parseNow)
                GetBytes(true);
            else
                this._wzReader.BaseStream.Position += _length;
        }

        /// <summary>
        /// Constructor copy
        /// </summary>
        /// <param name="copy"></param>
        private WzRawDataProperty(WzRawDataProperty copy)
        {
            this._name = copy._name;
            this._bytes = new byte[copy._length];
            copy.GetBytes(false).CopyTo(_bytes, 0);
            this._length = copy._length;
        }

        #region Inherited Members
        public override WzImageProperty DeepClone()
        {
            return new WzRawDataProperty(this);
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

        public override void WriteValue(WzBinaryWriter writer)
        {
            var data = GetBytes(false);
            writer.WriteStringValue(RAW_DATA_HEADER, WzImage.WzImageHeaderByte_WithoutOffset,
                WzImage.WzImageHeaderByte_WithOffset);
            writer.Write((byte)0);
            writer.Write(data.Length);
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
        }
        #endregion

        public byte[] GetBytes(bool saveInMemory)
        {
            if (this._bytes != null) // check in-memory
                return this._bytes;

            if (this._wzReader == null)
                return null;

            // read if none
            var currentPos = _wzReader.BaseStream.Position;
            this._wzReader.BaseStream.Position = _offset;
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
    }
}
