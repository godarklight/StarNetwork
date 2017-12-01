using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MessageStream2;
namespace StarNetwork
{
	public class StarServer
	{
		private TcpListener tcpListener;
		private List<NetworkStream> clients = new List<NetworkStream>();
        private Dictionary<NetworkStream, Guid> clientGuids = new Dictionary<NetworkStream, Guid>();
        private Dictionary<NetworkStream, IPEndPoint> clientUDPEndpoints = new Dictionary<NetworkStream, IPEndPoint>();

		private void AcceptClient(IAsyncResult ar)
		{
			TcpClient tcpClient = tcpListener.EndAcceptTcpClient(ar);
			lock (clients)
			{
				ServerMessageHandling smh = new ServerMessageHandling();
				NetworkStream ns = new NetworkStream(tcpClient, smh, SendDisconnectToClients);
                smh.SetNetworkStream(ns, EndpointCallback);
                using (MessageWriter mw = new MessageWriter())
                {
                    Guid newGuid = Guid.NewGuid();
                    Console.WriteLine("Setting UDP GUID for client to " + newGuid);
                    clientGuids.Add(ns, newGuid);
                    mw.Write<byte[]>(newGuid.ToByteArray());
                    ns.SendMessage(MessageType.GUID, mw.GetMessageBytes());
                }
                /*
                SendJoinToClients(ns, (IPEndPoint)tcpClient.Client.RemoteEndPoint);
				SendClientsToJoin(ns);
                */
                clients.Add(ns);
                Console.WriteLine(tcpClient.Client.RemoteEndPoint.ToString() + " connected!");
			}
            tcpListener.BeginAcceptTcpClient(AcceptClient, null);
		}

        private void EndpointCallback(NetworkStream ns, IPEndPoint endpoint)
        {
            lock (clients)
            {
                clientUDPEndpoints.Add(ns, endpoint);
                SendJoinToClients(ns, endpoint);
                SendClientsToJoin(ns);
            }
        }

		private void SendJoinToClients(NetworkStream ns, IPEndPoint endpoint)
		{
			byte[] data = null;
			using (MessageWriter mw = new MessageWriter())
			{
				mw.Write<string>(endpoint.ToString());
                mw.Write<byte[]>(clientGuids[ns].ToByteArray());
				data = mw.GetMessageBytes();
			}
			foreach (NetworkStream nsother in clients)
			{
                if (nsother != ns && clientUDPEndpoints.ContainsKey(nsother))
                {
                    nsother.SendMessage(MessageType.CONNECT_ENDPOINT, data);
                }
			}
		}

		private void SendClientsToJoin(NetworkStream ns)
		{

			foreach (NetworkStream nsother in clients)
			{
                if (nsother == ns)
                {
                    continue;
                }
                if (!clientUDPEndpoints.ContainsKey(nsother))
                {
                    continue;
                }
				using (MessageWriter mw = new MessageWriter())
				{
                    mw.Write<string>(clientUDPEndpoints[nsother].ToString());
                    mw.Write<byte[]>(clientGuids[nsother].ToByteArray());
					ns.SendMessage(MessageType.CONNECT_ENDPOINT, mw.GetMessageBytes());
				}
			}
		}

		private void SendDisconnectToClients(NetworkStream ns)
        {
            Guid disconnectGuid = clientGuids[ns];
			lock (clients)
			{
				clients.Remove(ns);
                clientGuids.Remove(ns);
			}
			byte[] data = null;
			using (MessageWriter mw = new MessageWriter())
			{
                mw.Write<byte[]>(disconnectGuid.ToByteArray());
				data = mw.GetMessageBytes();
			}
			foreach (NetworkStream nsother in clients)
			{
				nsother.SendMessage(MessageType.DISCONNECT_ENDPOINT, data);
			}
		}

		public void Run()
		{
			tcpListener = new TcpListener(IPAddress.Any, 2076);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(AcceptClient, null);
			while (true)
			{
                //Console.Clear();
                List<NetworkStream> clientcopy = new List<NetworkStream>(clients);
				foreach (NetworkStream ns in clientcopy)
				{
					ns.SendMessage(MessageType.HEARTBEAT, null);
                    //Yes I know this is ugly, but YOLO, it's a proof of concept program
                    ((ServerMessageHandling)ns.GetHandler()).ReportNTP();
				}
				System.Threading.Thread.Sleep(1000);
			}
		}
	}
}
