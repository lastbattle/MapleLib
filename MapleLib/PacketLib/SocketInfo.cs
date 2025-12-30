using System;
using System.Net.Sockets;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to manage Socket and data to receive
	/// </summary>
	public class SocketInfo
	{
		/// <summary>
		/// Creates a new instance of a SocketInfo
		/// </summary>
		/// <param name="socket">Socket connection of the session</param>
		/// <param name="headerLength">Length of the main packet's header (Usually 4)</param>
		public SocketInfo(Socket socket, short headerLength) : this (socket, headerLength, false) {
		}

		public SocketInfo(Socket socket, short headerLength, bool noEncryption)
		{
			Socket = socket;
			State = StateEnum.Header;
			NoEncryption = noEncryption;
			DataBuffer = new byte[headerLength];
			Index = 0;
		}

		/// <summary>
		/// The SocketInfo's socket
		/// </summary>
		public readonly Socket Socket;

		public bool NoEncryption;

		/// <summary>
		/// The Session's state of what data to receive
		/// </summary>
		public StateEnum State;

		/// <summary>
		/// The buffer of data to recieve
		/// </summary>
		public byte[] DataBuffer;

		/// <summary>
		/// The index of the current data
		/// </summary>
		public int Index;

		/// <summary>
		/// The SocketInfo's state of data
		/// </summary>
		public enum StateEnum { Header, Content }
	}
}