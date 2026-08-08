using System;
using System.IO;
using System.Net.Sockets;
using MapleLib.MapleCryptoLib;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Class to a network session socket
	/// </summary>
	public class Session
	{

		/// <summary>
		/// The Session's socket
		/// </summary>
		private readonly Socket _socket;

		private SessionType _type;

		/// <summary>
		/// The Recieved packet crypto manager
		/// </summary>
		private MapleCrypto _RIV;

		/// <summary>
		/// The Sent packet crypto manager
		/// </summary>
		private MapleCrypto _SIV;
		private readonly object _sendLock = new object();

        /// <summary>
        /// Method to handle packets received
        /// </summary>
        public delegate void PacketReceivedHandler(PacketReader packet, bool mIsInit);

        /// <summary>
		/// Packet received event
		/// </summary>
		public event PacketReceivedHandler OnPacketReceived;

		/// <summary>
		/// Method to handle client disconnected
		/// </summary>
		public delegate void ClientDisconnectedHandler(Session session);

		/// <summary>
		/// Client disconnected event
		/// </summary>
		public event ClientDisconnectedHandler OnClientDisconnected;

		public delegate void InitPacketReceived(short version, byte serverIdentifier);
		public event InitPacketReceived OnInitPacketReceived;

		/// <summary>
		/// The Recieved packet crypto manager
		/// </summary>
		public MapleCrypto RIV
		{
			get { return _RIV; }
			set { _RIV = value; }
		}

		/// <summary>
		/// The Sent packet crypto manager
		/// </summary>
		public MapleCrypto SIV
		{
			get
			{
				lock (_sendLock)
					return _SIV;
			}
			set
			{
				lock (_sendLock)
					_SIV = value;
			}
		}

		/// <summary>
		/// The Session's socket
		/// </summary>
		public Socket Socket
		{
			get { return _socket; }
		}

		public SessionType Type
		{
			get { return _type; }
		}
		/// <summary>
		/// Creates a new instance of a Session
		/// </summary>
		/// <param name="socket">Socket connection of the session</param>

		public Session(Socket socket, SessionType type)
		{
			ArgumentNullException.ThrowIfNull(socket);
			_socket = socket;
			_type = type;
		}

		/// <summary>
		/// Waits for more data to arrive
		/// </summary>
		public void WaitForData()
		{
			WaitForData(new SocketInfo(_socket, 4));
		}

		public void WaitForDataNoEncryption()
		{
			WaitForData(new SocketInfo(_socket, 2, true));
		}

		/// <summary>
		/// Waits for more data to arrive
		/// </summary>
		/// <param name="socketInfo">Info about data to be received</param>
		private void WaitForData(SocketInfo socketInfo)
		{
			try
			{
				_socket.BeginReceive(socketInfo.DataBuffer,
					socketInfo.Index,
					socketInfo.DataBuffer.Length - socketInfo.Index,
					SocketFlags.None,
					new AsyncCallback(OnDataReceived),
					socketInfo);
			}
			catch (Exception se)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.WaitForData: " + se);
				//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.WaitForData: " + se);
			}
		}

		/// <summary>
		/// Data received event handler
		/// </summary>
		/// <param name="iar">IAsyncResult of the data received event</param>
		private void OnDataReceived(IAsyncResult iar)
		{
			SocketInfo socketInfo = (SocketInfo)iar.AsyncState;
			try
			{
				int received = socketInfo.Socket.EndReceive(iar);
				if (received == 0)
				{
					if (OnClientDisconnected != null)
					{
						OnClientDisconnected(this);
					}
					return;
				}

				socketInfo.Index += received;

				if (socketInfo.Index == socketInfo.DataBuffer.Length)
				{
					switch (socketInfo.State)
					{
						case SocketInfo.StateEnum.Header:
							if (socketInfo.NoEncryption)
							{
								PacketReader headerReader = new PacketReader(socketInfo.DataBuffer);
								int packetHeader = headerReader.ReadUShort();
								ValidatePacketLength(packetHeader);
								socketInfo.State = SocketInfo.StateEnum.Content;
								socketInfo.DataBuffer = new byte[packetHeader];
								socketInfo.Index = 0;
								WaitForData(socketInfo);
							}
							else
							{
								PacketReader headerReader = new PacketReader(socketInfo.DataBuffer);
								byte[] packetHeaderB = headerReader.ToArray();
								int packetHeader = headerReader.ReadInt();
								int packetLength = MapleCrypto.GetPacketLength(packetHeader);
								ValidatePacketLength(packetLength);
								if (_type == SessionType.SERVER_TO_CLIENT && _RIV != null && !_RIV.CheckPacketToServer(packetHeaderB))
								{
									Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Packet check failed. Disconnecting client.");
									throw new InvalidDataException("Packet header validation failed.");
								}
								socketInfo.State = SocketInfo.StateEnum.Content;
								socketInfo.DataBuffer = new byte[packetLength];
								socketInfo.Index = 0;
								WaitForData(socketInfo);
							}
							break;
						case SocketInfo.StateEnum.Content:
							byte[] data = socketInfo.DataBuffer;
							if (socketInfo.NoEncryption)
							{
								socketInfo.NoEncryption = false;
								PacketReader reader = new PacketReader(data);
								short version = reader.ReadShort();
								string unknown = reader.ReadMapleString();
								byte[] sendIv = reader.ReadBytes(4);
								byte[] receiveIv = reader.ReadBytes(4);
								lock (_sendLock)
								{
									_SIV = new MapleCrypto(sendIv, version);
									_RIV = new MapleCrypto(receiveIv, version);
								}
								byte serverType = reader.ReadByte();
								if (_type == SessionType.CLIENT_TO_SERVER)
								{
									OnInitPacketReceived?.Invoke(version, serverType);
								}
								OnPacketReceived?.Invoke(new PacketReader(data), true);
								WaitForData();
							}
							else
							{
								_RIV.Crypt(data);
								MapleCustomEncryption.Decrypt(data);
								if (data.Length != 0 && OnPacketReceived != null)
								{
									OnPacketReceived(new PacketReader(data), false);
								}
								WaitForData();
							}
							break;
					}
				}
				else
				{
					Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Warning] Not enough data");
					WaitForData(socketInfo);
				}
			}
			catch (ObjectDisposedException)
			{
				Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: Socket has been closed");
			}
			catch (SocketException se)
			{
				if (se.ErrorCode != 10054)
				{
					Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + se);
				}
			}
			catch (Exception e)
			{
				Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Session.OnDataReceived: " + e);
			}
		}

        public void SendInitialPacket(int pVersion, string pPatchLoc, byte[] pRIV, byte[] pSIV, byte pServerType)
        {
            if (pVersion < short.MinValue || pVersion > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pVersion));
            ArgumentNullException.ThrowIfNull(pPatchLoc);
            ValidateIv(pRIV, nameof(pRIV));
            ValidateIv(pSIV, nameof(pSIV));

            using PacketWriter writer = new PacketWriter();
            writer.WriteShort(pPatchLoc.Length == 0 ? 0x0D : 0x0E);
            writer.WriteShort(pVersion);
            writer.WriteMapleString(pPatchLoc);
            writer.WriteBytes(pRIV);
            writer.WriteBytes(pSIV);
            writer.WriteByte(pServerType);
            SendRawPacket(writer);
        }

		/// <summary>
		/// Encrypts the packet then send it to the client.
		/// </summary>
		/// <param name="packet">The PacketWrtier object to be sent.</param>
		public void SendPacket(PacketWriter packet)
		{
			ArgumentNullException.ThrowIfNull(packet);
			SendPacket(packet.ToArray());
		}

		/// <summary>
		/// Encrypts the packet then send it to the client.
		/// </summary>
		/// <param name="input">The byte array to be sent.</param>
		public void SendPacket(byte[] input)
		{
			ArgumentNullException.ThrowIfNull(input);
			if (input.Length > ushort.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(input), "Maple packet payload cannot exceed the 16-bit header length.");

			lock (_sendLock)
			{
				if (_SIV == null)
					throw new InvalidOperationException("The send cipher has not been initialized.");

				byte[] cryptData = (byte[])input.Clone();
				byte[] sendData = new byte[checked(cryptData.Length + 4)];
				byte[] header = _type == SessionType.SERVER_TO_CLIENT ? _SIV.GetHeaderToClient(cryptData.Length) : _SIV.GetHeaderToServer(cryptData.Length);

				MapleCustomEncryption.Encrypt(cryptData);
				_SIV.Crypt(cryptData);

				System.Buffer.BlockCopy(header, 0, sendData, 0, 4);
				System.Buffer.BlockCopy(cryptData, 0, sendData, 4, cryptData.Length);
				SendRawPacket(sendData);
			}
		}

        /// <summary>
        /// Sends a raw packet to the client
        /// </summary>
        /// <param name="pPacket">The PacketWriter</param>
        public void SendRawPacket(PacketWriter pPacket)
        {
            SendRawPacket(pPacket.ToArray());
        }

		/// <summary>
		/// Sends a raw buffer to the client.
		/// </summary>
		/// <param name="buffer">The buffer to be sent.</param>
		public void SendRawPacket(byte[] buffer)
		{
			ArgumentNullException.ThrowIfNull(buffer);
			lock (_sendLock)
			{
				int sent = 0;
				while (sent < buffer.Length)
				{
					int current = _socket.Send(buffer, sent, buffer.Length - sent, SocketFlags.None);
					if (current <= 0)
						throw new IOException("The socket closed before the complete packet was sent.");
					sent += current;
				}
			}
		}

		private static void ValidateIv(byte[] iv, string parameterName)
		{
			ArgumentNullException.ThrowIfNull(iv, parameterName);
			if (iv.Length != 4)
				throw new ArgumentException("Maple handshake IVs must contain exactly four bytes.", parameterName);
		}

		private static void ValidatePacketLength(int length)
		{
			if (length <= 0 || length > MemoryLimits.MAX_PACKET_BYTES)
				throw new InvalidDataException($"Invalid packet body length: {length}.");
		}

	}
}
