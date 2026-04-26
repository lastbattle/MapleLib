using System;
using System.IO;
using System.Text;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to handle reading data from a packet
	/// </summary>
	public class PacketReader : AbstractPacket, IDisposable
	{
		/// <summary>
		/// The main reader tool
		/// </summary>
		private readonly BinaryReader _binReader;

		/// <summary>
		/// Amount of data left in the reader
		/// </summary>
		public short Length
		{
			get { return (short)_buffer.Length; }
		}

		public int Position
		{
			get { return (int)_buffer.Position; }
			set { _buffer.Position = value; }
		}

		public Stream BaseStream
		{
			get { return _buffer; }
		}

		public int Remaining
		{
			get { return (int)(_buffer.Length - _buffer.Position); }
		}

		/// <summary>
		/// Creates a new instance of PacketReader
		/// </summary>
		/// <param name="arrayOfBytes">Starting byte array</param>
		public PacketReader(byte[] arrayOfBytes)
		{
			_buffer = new MemoryStream(arrayOfBytes, false);
			_binReader = new BinaryReader(_buffer, Encoding.ASCII);
		}

		public PacketReader(Stream stream)
			: this(stream, Encoding.ASCII, false)
		{
		}

		public PacketReader(Stream stream, Encoding encoding)
			: this(stream, encoding, false)
		{
		}

		public PacketReader(Stream stream, Encoding encoding, bool leaveOpen)
		{
			_buffer = stream as MemoryStream ?? new MemoryStream();
			if (stream is not MemoryStream && stream != null)
			{
				stream.CopyTo(_buffer);
				_buffer.Position = 0;
			}

			_binReader = new BinaryReader(_buffer, encoding ?? Encoding.ASCII, leaveOpen);
		}

		/// <summary>
		/// Restart reading from the point specified.
		/// </summary>
		/// <param name="length">The point of the packet to start reading from.</param>
		public void Reset(int length)
		{
			_buffer.Seek(length, SeekOrigin.Begin);
		}

		public void Skip(int length)
		{
			_buffer.Position += length;
		}

		/// <summary>
		/// Reads an unsigned byte from the stream
		/// </summary>
		/// <returns> an unsigned byte from the stream</returns>
		public byte ReadByte()
		{
			return _binReader.ReadByte();
		}

		public sbyte ReadSByte()
		{
			return _binReader.ReadSByte();
		}

		/// <summary>
		/// Reads a byte array from the stream
		/// </summary>
		/// <param name="length">Amount of bytes</param>
		/// <returns>A byte array</returns>
		public byte[] ReadBytes(int count)
		{
			return _binReader.ReadBytes(count);
		}

		/// <summary>
		/// Reads a bool from the stream
		/// </summary>
		/// <returns>A bool</returns>
		public bool ReadBool()
		{
			return _binReader.ReadBoolean();
		}

		public bool ReadBoolean()
		{
			return ReadBool();
		}

		/// <summary>
		/// Reads a signed short from the stream
		/// </summary>
		/// <returns>A signed short</returns>
		public short ReadShort()
		{
			return _binReader.ReadInt16();
		}

		public short ReadInt16()
		{
			return ReadShort();
		}

		public ushort ReadUShort()
		{
			return _binReader.ReadUInt16();
		}

		public ushort ReadUInt16()
		{
			return ReadUShort();
		}

		/// <summary>
		/// Reads a signed int from the stream
		/// </summary>
		/// <returns>A signed int</returns>
		public int ReadInt()
		{
			return _binReader.ReadInt32();
		}

		public int ReadInt32()
		{
			return ReadInt();
		}

		public uint ReadUInt()
		{
			return _binReader.ReadUInt32();
		}

		public uint ReadUInt32()
		{
			return ReadUInt();
		}

		/// <summary>
		/// Reads a signed long from the stream
		/// </summary>
		/// <returns>A signed long</returns>
		public long ReadLong()
		{
			return _binReader.ReadInt64();
		}

		public long ReadInt64()
		{
			return ReadLong();
		}

		public ulong ReadULong()
		{
			return _binReader.ReadUInt64();
		}

		public ulong ReadUInt64()
		{
			return ReadULong();
		}

		public double ReadDouble()
		{
			return _binReader.ReadDouble();
		}

		/// <summary>
		/// Reads an ASCII string from the stream
		/// </summary>
		/// <param name="length">Amount of bytes</param>
		/// <returns>An ASCII string</returns>
		public string ReadString(int length)
		{
			return Encoding.ASCII.GetString(ReadBytes(length));
		}

		/// <summary>
		/// Reads a maple string from the stream
		/// </summary>
		/// <returns>A maple string</returns>
		public string ReadMapleString()
		{
			return ReadString(ReadShort());
		}

		public void Dispose()
		{
			_binReader.Dispose();
		}
	}
}
