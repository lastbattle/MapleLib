using System;
using System.IO;
using System.Text;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to handle writing packets
	/// </summary>
	public class PacketWriter : AbstractPacket, IDisposable
	{
		/// <summary>
		/// The main writer tool
		/// </summary>
		private readonly BinaryWriter _binWriter;

		/// <summary>
		/// Amount of data writen in the writer
		/// </summary>
		public short Length
		{
			get { return (short)_buffer.Length; }
		}

		public Stream BaseStream
		{
			get { return _buffer; }
		}

		/// <summary>
		/// Creates a new instance of PacketWriter
		/// </summary>
		public PacketWriter()
			: this(0)
		{
		}

		/// <summary>
		/// Creates a new instance of PacketWriter
		/// </summary>
		/// <param name="size">Starting size of the buffer</param>
		public PacketWriter(int size)
		{
			_buffer = new MemoryStream(size);
			_binWriter = new BinaryWriter(_buffer, Encoding.ASCII);
		}

		public PacketWriter(byte[] data)
		{
			_buffer = new MemoryStream(data);
			_binWriter = new BinaryWriter(_buffer, Encoding.ASCII);
		}

		public PacketWriter(Stream stream)
			: this(stream, Encoding.ASCII, false)
		{
		}

		public PacketWriter(Stream stream, Encoding encoding)
			: this(stream, encoding, false)
		{
		}

		public PacketWriter(Stream stream, Encoding encoding, bool leaveOpen)
		{
			_buffer = stream as MemoryStream ?? new MemoryStream();
			_binWriter = new BinaryWriter(_buffer, encoding ?? Encoding.ASCII, leaveOpen);
		}

		/// <summary>
		/// Restart writing from the point specified. This will overwrite data in the packet.
		/// </summary>
		/// <param name="length">The point of the packet to start writing from.</param>
		public void Reset(int length)
		{
			_buffer.Seek(length, SeekOrigin.Begin);
		}

		/// <summary>
		/// Writes a byte to the stream
		/// </summary>
		/// <param name="@byte">The byte to write</param>
		public void WriteByte(int @byte)
		{
			_binWriter.Write((byte)@byte);
		}

		public void Write(byte @byte)
		{
			WriteByte(@byte);
		}

		public void Write(sbyte @byte)
		{
			_binWriter.Write(@byte);
		}

		/// <summary>
		/// Writes a byte array to the stream
		/// </summary>
		/// <param name="@bytes">The byte array to write</param>
		public void WriteBytes(byte[] @bytes)
		{
			_binWriter.Write(@bytes);
		}

		public void Write(byte[] @bytes)
		{
			WriteBytes(@bytes);
		}

		public void Write(byte[] @bytes, int index, int count)
		{
			_binWriter.Write(@bytes, index, count);
		}

		/// <summary>
		/// Writes a boolean to the stream
		/// </summary>
		/// <param name="@bool">The boolean to write</param>
		public void WriteBool(bool @bool)
		{
			_binWriter.Write(@bool);
		}

		public void Write(bool @bool)
		{
			WriteBool(@bool);
		}

		/// <summary>
		/// Writes a short to the stream
		/// </summary>
		/// <param name="@short">The short to write</param>
		public void WriteShort(int @short)
		{
			_binWriter.Write((short)@short);
		}

		public void Write(short @short)
		{
			WriteShort(@short);
		}

		public void Write(ushort @short)
		{
			_binWriter.Write(@short);
		}

		/// <summary>
		/// Writes an int to the stream
		/// </summary>
		/// <param name="@int">The int to write</param>
		public void WriteInt(int @int)
		{
			_binWriter.Write(@int);
		}

		public void Write(int @int)
		{
			WriteInt(@int);
		}

		public void Write(uint @int)
		{
			_binWriter.Write(@int);
		}

		/// <summary>
		/// Writes a long to the stream
		/// </summary>
		/// <param name="@long">The long to write</param>
		public void WriteLong(long @long)
		{
			_binWriter.Write(@long);
		}

		public void Write(long @long)
		{
			WriteLong(@long);
		}

		public void Write(ulong @long)
		{
			_binWriter.Write(@long);
		}

		/// <summary>
		/// Writes a string to the stream
		/// </summary>
		/// <param name="@string">The string to write</param>
		public void WriteString(String @string)
		{
			_binWriter.Write(@string.ToCharArray());
		}

		public void Write(string @string)
		{
			WriteString(@string);
		}

		/// <summary>
		/// Writes a string prefixed with a [short] length before it, to the stream
		/// </summary>
		/// <param name="@string">The string to write</param>
		public void WriteMapleString(String @string)
		{
			WriteShort((short)@string.Length);
			WriteString(@string);
		}

		/// <summary>
		/// Writes a hex-string to the stream
		/// </summary>
		/// <param name="@string">The hex-string to write</param>
		public void WriteHexString(String hexString)
		{
			WriteBytes(HexEncoding.GetBytes(hexString));
		}

		/// <summary>
		/// Sets a byte in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@byte">The byte to set</param>
		public void SetByte(long index, int @byte)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteByte((byte)@byte);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a byte array in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@bytes">The bytes to set</param>
		public void SetBytes(long index, byte[] @bytes)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteBytes(@bytes);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a bool in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@bool">The bool to set</param>
		public void SetBool(long index, bool @bool)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteBool(@bool);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a short in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@short">The short to set</param>
		public void SetShort(long index, int @short)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteShort((short)@short);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets an int in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@int">The int to set</param>
		public void SetInt(long index, int @int)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteInt(@int);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a long in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@long">The long to set</param>
		public void SetLong(long index, long @long)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteLong(@long);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a long in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@string">The long to set</param>
		public void SetString(long index, string @string)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteString(@string);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a string prefixed with a [short] length before it, in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@string">The string to set</param>
		public void SetMapleString(long index, string @string)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteMapleString(@string);
			_buffer.Position = oldIndex;
		}

		/// <summary>
		/// Sets a hex-string in the stream
		/// </summary>
		/// <param name="index">The index of the stream to set data at</param>
		/// <param name="@string">The hex-string to set</param>
		public void SetHexString(long index, string @string)
		{
			long oldIndex = _buffer.Position;
			_buffer.Position = index;
			WriteHexString(@string);
			_buffer.Position = oldIndex;
		}

		public void Flush()
		{
			_binWriter.Flush();
		}

		public void Dispose()
		{
			_binWriter.Dispose();
		}

	}
}
