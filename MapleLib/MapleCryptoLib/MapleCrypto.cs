using System;
using System.Buffers.Binary;

namespace MapleLib.MapleCryptoLib
{
	/// <summary>
	/// Class to manage Encryption and IV generation
	/// </summary>
	public class MapleCrypto
	{
		#region Properties
		/// <summary>
		/// (private) IV used in the packet encryption
		/// </summary>
		private byte[] _IV;

		/// <summary>
		/// Version of MapleStory used in encryption
		/// </summary>
		private readonly short _mapleVersion;

		/// <summary>
		/// (public) IV used in the packet encryption
		/// </summary>
		public byte[] IV
		{
			get { return _IV; }
			set { _IV = value; }
		}
		#endregion

		#region Methods
		/// <summary>
		/// Creates a new MapleCrypto class
		/// </summary>
		/// <param name="IV">Intializing Vector</param>
		/// <param name="mapleVersion">Version of MapleStory</param>
		public MapleCrypto(byte[] IV, short mapleVersion)
		{
			this._IV = IV;
			this._mapleVersion = mapleVersion;
		}

		/// <summary>
		/// Updates the current IV
		/// </summary>
		public void UpdateIV()
		{
			_IV = GetNewIV(_IV);
		}

		/// <summary>
		/// Encrypts data with AES and updates the IV
		/// </summary>
		/// <param name="data">The data to crypt</param>
		public void Crypt(byte[] data)
		{
			MapleAESEncryption.AesCrypt(_IV, data, data.Length);
			UpdateIV();
		}

		/// <summary>
		/// Generates a new IV
		/// </summary>
		/// <param name="oldIv">The Old IV used to generate the new IV</param>
		/// <returns>A new IV</returns>
		public static byte[] GetNewIV(byte[] oldIv)
		{
			//byte[] start = CryptoConstants.bDefaultAESKeyValue;
			byte[] start = new byte[4] { 0xf2, 0x53, 0x50, 0xc6 };//TODO: ADD GLOBAL VAR BACK
			for (int i = 0; i < 4; i++)
			{
				Shuffle(oldIv[i], start);
			}
			return start;
		}

		/// <summary>
		/// Shuffle the bytes in the IV
		/// </summary>
		/// <param name="inputByte">Byte of the old IV</param>
		/// <param name="start">The Default AES Key</param>
		/// <returns>The shuffled bytes</returns>
		public static byte[] Shuffle(byte inputByte, byte[] start)
		{
			byte a = start[1];
			byte b = a;
			uint c, d;
			b = MapleCryptoConstants.bShuffle[b];
			b -= inputByte;
			start[0] += b;
			b = start[2];
			b ^= MapleCryptoConstants.bShuffle[inputByte];
			a -= b;
			start[1] = a;
			a = start[3];
			b = a;
			a -= start[0];
			b = MapleCryptoConstants.bShuffle[b];
			b += inputByte;
			b ^= start[2];
			start[2] = b;
			a += MapleCryptoConstants.bShuffle[inputByte];
			start[3] = a;

			c = (uint)(start[0] + start[1] * 0x100 + start[2] * 0x10000 + start[3] * 0x1000000);
			d = c;
			c >>= 0x1D;
			d <<= 0x03;
			c |= d;
			start[0] = (byte)c;
			start[1] = (byte)(c >> 8);
			start[2] = (byte)(c >> 16);
			start[3] = (byte)(c >> 24);

			return start;
		}

		/// <summary>
		/// Get a packet header for a packet being sent to the server
		/// </summary>
		/// <param name="size">Size of the packet</param>
		/// <returns>The packet header</returns>
		public byte[] GetHeaderToClient(int size)
		{
			byte[] header = new byte[4];
			int a = _IV[3] * 0x100 + _IV[2];
			a ^= -(_mapleVersion + 1);
			int b = a ^ size;
			header[0] = (byte)a;
			header[1] = (byte)(a >> 8);
			header[2] = (byte)b;
			header[3] = (byte)(b >> 8);
			return header;
		}

		/// <summary>
		/// Get a packet header for a packet being sent to the client
		/// </summary>
		/// <param name="size">Size of the packet</param>
		/// <returns>The packet header</returns>
		public byte[] GetHeaderToServer(int size)
		{
			byte[] header = new byte[4];
			int a = IV[3] * 0x100 + IV[2];
			a = a ^ (_mapleVersion);
			int b = a ^ size;
			header[0] = Convert.ToByte(a % 0x100);
			header[1] = Convert.ToByte(a / 0x100);
			header[2] = Convert.ToByte(b % 0x100);
			header[3] = Convert.ToByte(b / 0x100);
			return header;
		}

		/// <summary>
		/// Gets the length of a packet from the header
		/// </summary>
		/// <param name="packetHeader">Header of the packet</param>
		/// <returns>The length of the packet</returns>
		public static int GetPacketLength(int packetHeader)
		{
			return (packetHeader & 0xFFFF) ^ (int)((uint)packetHeader >> 16);
		}

		/// <summary>
		/// Gets the length of a packet from the header
		/// </summary>
		/// <param name="packetHeader">Header of the packet</param>
		/// <returns>The length of the packet</returns>
		public static int GetPacketLength(byte[] packetHeader)
		{
			if (packetHeader.Length < 4)
			{
				return -1;
			}
			return BinaryPrimitives.ReadUInt16LittleEndian(packetHeader) ^
				BinaryPrimitives.ReadUInt16LittleEndian(packetHeader.AsSpan(2));

		}

		/// <summary>
		/// Checks to make sure the packet is a valid MapleStory packet
		/// </summary>
		/// <param name="packetHeader">The header of the packet received</param>
		/// <returns>The packet is valid</returns>
		public bool CheckPacketToServer(byte[] packet)
		{
			int a = packet[0] ^ _IV[2];
			int b = _mapleVersion;
			int c = packet[1] ^ _IV[3];
			int d = _mapleVersion >> 8;
			return (a == b && c == d);
		}

		/// <summary>
		/// Multiplies bytes
		/// </summary>
		/// <param name="input">Bytes to multiply</param>
		/// <param name="count">Amount of bytes to repeat</param>
		/// <param name="mult">Times to repeat the packet</param>
		/// <returns>The multiplied bytes</returns>
		public static byte[] MultiplyBytes(byte[] input, int count, int mult)
		{
			byte[] ret = new byte[count * mult];
			if (ret.Length == 0)
			{
				return ret;
			}

			ReadOnlySpan<byte> pattern = input.AsSpan(0, count);
			for (int offset = 0; offset < ret.Length; offset += count)
			{
				pattern.CopyTo(ret.AsSpan(offset, count));
			}
			return ret;
		}

        public static byte[] MultiplyBytes_SIMD(byte[] input, int count, int mult)
        {
            return MultiplyBytes(input, count, mult);
        }
        #endregion

    }
}
