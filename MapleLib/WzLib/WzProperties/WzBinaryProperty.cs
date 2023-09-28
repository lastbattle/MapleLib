/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System.IO;
using System;
using System.Linq;
using MapleLib.WzLib.Util;
using NAudio.Wave;
using MapleLib.Helpers;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using MapleLib.PacketLib;

namespace MapleLib.WzLib.WzProperties
{
    public enum WzBinaryPropertyType
    {
        Raw, // could be anything.. 
        MP3,
        WAV,
    }

    /// <summary>
    /// A property that contains data for an MP3 or binary file
    /// </summary>
    public class WzBinaryProperty : WzExtended
    {
        #region Constants
        private static readonly byte[] _riff_waveHeader = {  // RIFF`(�WAVEfmt �����"V��D¬����datað_(
            0x52, 0x49, 0x46, 0x46, // 'RIFF'
            0x14, 0x60, 0x28, 0x00, // chunk size
            0x57, 0x41, 0x56, 0x45, // 'WAVE' id
            0x66, 0x6D, 0x74, // 'fmt'
            0x20, 0x10, 0x00, 0x00, // chunk size
            0x00, 0x01, // wFormatTag
            0x00, 0x01, // nChannels
            0x00, 0x22, 0x56, 0x00, 0x00, // nSamplesPerSec
            //0x44, 0xAC, 0x00, 0x00, 0x02, // nAvgBytesPerSec
            //0x00, 0x10, // nBlockAlign
            //0x00, 0x64, // cbSize
            //0x61, 0x74, // wValidBitsPerSample
            //0x61, 0xF0, 0x5F, 0x28 // dwChannelMask
        };

        public static readonly byte[] soundHeader = new byte[] {
            0x02,
            0x83, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x8B, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x00,
            0x01,
            0x81, 0x9F, 0x58, 0x05, 0x56, 0xC3, 0xCE, 0x11, 0xBF, 0x01, 0x00, 0xAA, 0x00, 0x55, 0x59, 0x5A };
        #endregion

        #region Fields
        internal string name;
        internal byte[] fileBytes = null;
        internal WzObject parent;
        internal int len_ms;
        internal byte[] header;
        //internal WzImage imgParent;
        internal WzBinaryReader wzReader;

        /// <summary>
        /// List.wz, header is encrypted
        /// </summary>
        internal bool headerEncrypted = false;

        internal long offs;
        internal int soundDataLen;

        internal WaveFormat wavFormat;
        #endregion

        #region Inherited Members

        public override WzImageProperty DeepClone()
        {
            WzBinaryProperty clone = new WzBinaryProperty(this);
            return clone;
        }

        public override object WzValue { get { return GetBytes(false); } }

        public override void SetValue(object value)
        {
            return;
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
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.Sound; } }
        public override void WriteValue(WzBinaryWriter writer)
        {
            byte[] data = GetBytes(false);
            writer.WriteStringValue("Sound_DX8", WzImage.WzImageHeaderByte_WithoutOffset, WzImage.WzImageHeaderByte_WithOffset);
            writer.Write((byte)0);
            writer.WriteCompressedInt(data.Length);
            writer.WriteCompressedInt(len_ms);
            writer.Write(header);
            writer.Write(data);
        }
        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedTag("WzSound", this.Name));
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            name = null;
            fileBytes = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The data of the mp3 header
        /// </summary>
        public byte[] Header { get { return header; } set { header = value; } }
        /// <summary>
        /// Length of the mp3 file in milliseconds
        /// </summary>
        public int Length { get { return len_ms; } set { len_ms = value; } }
        /// <summary>
        /// Frequency of the mp3 file in Hz
        /// </summary>
        public int Frequency
        {
            get { return wavFormat != null ? wavFormat.SampleRate : 0; }
        }
        public WaveFormat WavFormat
        {
            get { return wavFormat; }
            private set { }
        }
        /// <summary>
        /// BPS of the mp3 file
        /// </summary>
        //public byte BPS { get { return bps; } set { bps = value; } }
        /// <summary>
        /// Creates a WzSoundProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="reader">The wz reader</param>
        /// <param name="parseNow">Indicating whether to parse the property now</param>
        public WzBinaryProperty(string name, WzBinaryReader reader, bool parseNow)
        {
            this.name = name;
            wzReader = reader;
            reader.BaseStream.Position++;

            //note - soundDataLen does NOT include the length of the header.
            soundDataLen = reader.ReadCompressedInt();
            len_ms = reader.ReadCompressedInt();

            long headerOff = reader.BaseStream.Position;
            reader.BaseStream.Position += soundHeader.Length; //skip GUIDs
            int wavFormatLen = reader.ReadByte();
            reader.BaseStream.Position = headerOff;

            byte[] soundHeaderBytes = reader.ReadBytes(soundHeader.Length);
            byte[] unk1 = reader.ReadBytes(1);
            byte[] waveFormatBytes = reader.ReadBytes(wavFormatLen);

            header = soundHeaderBytes.Concat(unk1).Concat(waveFormatBytes).ToArray();

            Debug.WriteLine(HexTool.ByteArrayToString(soundHeaderBytes));
            Debug.WriteLine(HexTool.ByteArrayToString(unk1));
            Debug.WriteLine(HexTool.ByteArrayToString(waveFormatBytes));

            ParseWzSoundPropertyHeader();

            //sound file offs
            this.offs = reader.BaseStream.Position;
            if (parseNow)
                fileBytes = reader.ReadBytes(soundDataLen);
            else
                reader.BaseStream.Position += soundDataLen;
        }

        /// <summary>
        /// Creates a WzSoundProperty with the specified name and data from another WzSoundProperty Object
        /// </summary>
        /// <param name="name"></param>
        /// <param name="wavFormat"></param>
        /// <param name="len_ms"></param>
        /// <param name="soundDataLen"></param>
        /// <param name="headerClone"></param>
        /// <param name="data"></param>
        /// <param name="headerEncrypted"></param>
        public WzBinaryProperty(WzBinaryProperty otherProperty)
        {
            this.name = otherProperty.name;
            this.wavFormat = otherProperty.wavFormat;
            this.len_ms = otherProperty.len_ms;
            this.soundDataLen = otherProperty.soundDataLen;
            this.offs = otherProperty.offs;

            if (otherProperty.header == null) // not initialized yet
            {
                otherProperty.ParseWzSoundPropertyHeader();
            }
            this.header = new byte[otherProperty.header.Length];
            Array.Copy(otherProperty.header, this.header, otherProperty.header.Length);

            if (otherProperty.fileBytes == null)
                this.fileBytes = otherProperty.GetBytes(false);
            else
            {
                this.fileBytes = new byte[otherProperty.fileBytes.Length];
                Array.Copy(otherProperty.fileBytes, fileBytes, otherProperty.fileBytes.Length);
            }
            this.headerEncrypted = otherProperty.headerEncrypted;
        }

        /// <summary>
        /// Creates a WzSoundProperty with the specified name and data
        /// </summary>
        /// <param name="name"></param>
        /// <param name="len_ms"></param>
        /// <param name="headerClone"></param>
        /// <param name="data"></param>
        public WzBinaryProperty(string name, int len_ms, byte[] headerClone, byte[] data)
        {
            this.name = name;
            this.len_ms = len_ms;

            this.header = new byte[headerClone.Length];
            Array.Copy(headerClone, this.header, headerClone.Length);

            this.fileBytes = new byte[data.Length];
            Array.Copy(data, fileBytes, data.Length);

            ParseWzSoundPropertyHeader();
        }

        /// <summary>
        /// Creates a WzSoundProperty with the specified name from a file
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="file">The path to the sound file</param>
        public WzBinaryProperty(string name, string file)
        {
            this.name = name;
            Mp3FileReader reader = new Mp3FileReader(file);
            this.wavFormat = reader.Mp3WaveFormat;
            this.len_ms = (int)((double)reader.Length * 1000d / (double)reader.WaveFormat.AverageBytesPerSecond);
            RebuildHeader();
            reader.Dispose();
            this.fileBytes = File.ReadAllBytes(file);
        }

        private void RebuildHeader()
        {
            using (BinaryWriter bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write(soundHeader);
                byte[] wavHeader = StructToBytes(wavFormat);
                if (headerEncrypted)
                {
                    for (int i = 0; i < wavHeader.Length; i++)
                    {
                        wavHeader[i] ^= this.wzReader.WzKey[i];
                    }
                }
                bw.Write((byte)wavHeader.Length);
                bw.Write(wavHeader, 0, wavHeader.Length);
                header = ((MemoryStream)bw.BaseStream).ToArray();
            }
        }

        private static byte[] StructToBytes<T>(T obj)
        {
            byte[] result = new byte[Marshal.SizeOf(obj)];
            GCHandle handle = GCHandle.Alloc(result, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(obj, handle.AddrOfPinnedObject(), false);
                return result;
            }
            finally
            {
                handle.Free();
            }
        }

        private static T BytesToStruct<T>(byte[] data) where T : new()
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private static T BytesToStructConstructorless<T>(byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                T obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
                Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject(), obj);
                return obj;
            }
            finally
            {
                handle.Free();
            }
        }

        private void ParseWzSoundPropertyHeader()
        {
            byte[] wavHeader = new byte[header.Length - soundHeader.Length - 1];
            Buffer.BlockCopy(header, soundHeader.Length + 1, wavHeader, 0, wavHeader.Length);

            if (wavHeader.Length < Marshal.SizeOf<WaveFormat>())
                return;

            WaveFormat wavFmt = BytesToStruct<WaveFormat>(wavHeader);
            if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
            {
                //try decrypt
                for (int i = 0; i < wavHeader.Length; i++)
                {
                    wavHeader[i] ^= this.wzReader.WzKey[i];
                }
                wavFmt = BytesToStruct<WaveFormat>(wavHeader);

                if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
                {
                    ErrorLogger.Log(ErrorLevel.Critical, "parse sound header failed");
                    return;
                }
                headerEncrypted = true;
            }

            // parse to mp3 header
            if (wavFmt.Encoding == WaveFormatEncoding.MpegLayer3 && wavHeader.Length >= Marshal.SizeOf<Mp3WaveFormat>())
            {
                this.wavFormat = BytesToStructConstructorless<Mp3WaveFormat>(wavHeader);
            }
            else if (wavFmt.Encoding == WaveFormatEncoding.Pcm)
            {
                this.wavFormat = wavFmt;
            }
            else
            {
                ErrorLogger.Log(ErrorLevel.MissingFeature, string.Format("Unknown wave encoding {0}", wavFmt.Encoding.ToString()));
            }
        }
        #endregion

        #region Parsing Methods
        public byte[] GetBytesForWAVPlayback()
        {
            byte[] soundBytes = GetBytes(false);

            byte[] combinedArray = new byte[_riff_waveHeader.Length + header.Length + soundBytes.Length];

            Array.Copy(_riff_waveHeader, 0, combinedArray, 0, _riff_waveHeader.Length);
            Array.Copy(header, 0, combinedArray, _riff_waveHeader.Length, header.Length);
            Array.Copy(soundBytes, 0, combinedArray, _riff_waveHeader.Length + header.Length, soundBytes.Length);

            return combinedArray;
        }

        public byte[] GetBytes(bool saveInMemory)
        {
            if (fileBytes != null)
                return fileBytes;
            else
            {
                if (wzReader == null) 
                    return null;

                byte[] wavHeader = new byte[header.Length - soundHeader.Length - 1];
                Buffer.BlockCopy(header, soundHeader.Length + 1, wavHeader, 0, wavHeader.Length);

                long currentPos = wzReader.BaseStream.Position;
                wzReader.BaseStream.Position = offs;
                fileBytes = wzReader.ReadBytes(soundDataLen);
                wzReader.BaseStream.Position = currentPos;
                if (saveInMemory)
                    return fileBytes;
                else
                {
                    byte[] result = fileBytes;
                    fileBytes = null;
                    return result;
                }
            }
        }

        public void SaveToFile(string file)
        {
            File.WriteAllBytes(file, GetBytes(false));
        }
        #endregion

        #region Cast Values
        public override byte[] GetBytes()
        {
            return GetBytes(false);
        }
        #endregion
    }
}