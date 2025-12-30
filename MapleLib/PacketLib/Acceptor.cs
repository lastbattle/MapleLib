using System;
using System.Net;
using System.Net.Sockets;
using MapleLib.MapleCryptoLib;

namespace MapleLib.PacketLib
{

	/// <summary>
	/// A Nework Socket Acceptor (Listener)
	/// </summary>
	public class Acceptor
	{

		/// <summary>
		/// The listener socket
		/// </summary>
		private readonly Socket _listener;

		/// <summary>
		/// Method called when a client is connected
		/// </summary>
		public delegate void ClientConnectedHandler(Session session);

		/// <summary>
		/// Client connected event
		/// </summary>
		public event ClientConnectedHandler OnClientConnected;

		/// <summary>
		/// Creates a new instance of Acceptor
		/// </summary>
		public Acceptor()
		{
			_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		/// <summary>
		/// Starts listening and accepting connections
		/// </summary>
		/// <param name="port">Port to listen to</param>
		public void StartListening(int port)
		{
			_listener.Bind(new IPEndPoint(IPAddress.Any, port));
			_listener.Listen(15);
			_listener.BeginAccept(new AsyncCallback(OnClientConnect), null);
		}

        /// <summary>
        /// Stops listening for connections
        /// </summary>
        public void StopListening()
        {
            _listener.Disconnect(true);
        }

		/// <summary>
		/// Client connected handler
		/// </summary>
		/// <param name="iarl">The IAsyncResult</param>
		private void OnClientConnect(IAsyncResult iar)
		{
			try
			{
				Socket socket = _listener.EndAccept(iar);
				Session session = new Session(socket, SessionType.SERVER_TO_CLIENT);

				if (OnClientConnected != null)
					OnClientConnected(session);

				session.WaitForData();

				_listener.BeginAccept(new AsyncCallback(OnClientConnect), null);
			}
			catch (ObjectDisposedException)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] OnClientConnect: Socket closed.");
				//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] OnClientConnect: Socket closed.");
			}
			catch (Exception se)
			{
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] OnClientConnect: " + se);
				//Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] OnClientConnect: " + se);
			}
		}
	}
}