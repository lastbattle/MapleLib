using System.Net;
using System.Net.Sockets;

namespace MapleLib.PacketLib
{
	/// <summary>
	/// Socket class to connect to a listener
	/// </summary>
	public class Connector
	{

		/// <summary>
		/// The connecting socket
		/// </summary>
		private readonly Socket _socket;

		/// <summary>
		/// Method called when the client connects
		/// </summary>
		public delegate void ClientConnectedHandler(Session session);

		/// <summary>
		/// Client connected event
		/// </summary>
		public event ClientConnectedHandler OnClientConnected;

		/// <summary>
		/// Creates a new instance of Acceptor
		/// </summary>
		public Connector()
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		/// <summary>
		/// Connects to a listener
		/// </summary>
		/// <param name="ep">IPEndPoint of listener</param>
		/// <returns>Session connecting to</returns>
		public Session Connect(IPEndPoint ep)
		{
			_socket.Connect(ep);
			return CreateSession();
		}

		/// <summary>
		/// Connects to a listener
		/// </summary>
		/// <param name="ip">IPAdress of listener</param>
		/// <param name="port">Port of listener</param>
		/// <returns>Session connecting to</returns>
		public Session Connect(IPAddress ip, int port)
		{
			_socket.Connect(ip, port);
			return CreateSession();
		}

		/// <summary>
		/// Connects to a listener
		/// </summary>
		/// <param name="ip">IPAdress's of listener</param>
		/// <param name="port">Port of listener</param>
		/// <returns>Session connecting to</returns>
		public Session Connect(IPAddress[] ip, int port)
		{
			_socket.Connect(ip, port);
			return CreateSession();
		}

		/// <summary>
		/// Connects to a listener
		/// </summary>
		/// <param name="ip">IPAdress of listener</param>
		/// <param name="port">Port of listener</param>
		/// <returns>Session connecting to</returns>
		public Session Connect(string ip, int port)
		{
			_socket.Connect(ip, port);
			return CreateSession();
		}

		/// <summary>
		/// Creates the session after connecting
		/// </summary>
		/// <returns>Session created with listener</returns>
		private Session CreateSession()
		{
			Session session = new Session(_socket, SessionType.CLIENT_TO_SERVER);

			if (OnClientConnected != null)
				OnClientConnected(session);

			session.WaitForDataNoEncryption();

			return session;
		}
	}
}