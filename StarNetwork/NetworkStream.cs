using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MessageStream2;
namespace StarNetwork
{
    public class NetworkStream
    {
        private TcpClient tcpClient;
        private IPEndPoint ipEndpoint;
        private bool isHeader = true;
        private bool connected = true;
        private int receiveLeft = 8;
        private int receivePos = 0;
        private MessageType messageType;
        private byte[] receiveBuffer = new byte[1024];
        private DataHandler handler;
        private Action<NetworkStream> disconnectHandler;
        public NetworkStream(TcpClient tcpClient, DataHandler handler, Action<NetworkStream> disconnectHandler)
        {
            this.tcpClient = tcpClient;
            this.handler = handler;
            this.disconnectHandler = disconnectHandler;
            this.ipEndpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            tcpClient.Client.BeginReceive(receiveBuffer, receivePos, receiveLeft, SocketFlags.None, ProcessData, null);
        }

        public void ProcessData(IAsyncResult ar)
        {
            if (!connected)
            {
                return;
            }
            try
            {
                int bytesReceived = tcpClient.Client.EndReceive(ar);
                receiveLeft -= bytesReceived;
                receivePos += bytesReceived;
                //Console.WriteLine("Received: " + bytesReceived + ", Left: " + receiveLeft);
                if (bytesReceived == 0)
                {
                    Console.WriteLine("Received connection close");
                    Disconnect();
                    return;
                }
                else
                {
                    if (receiveLeft == 0)
                    {
                        if (isHeader)
                        {
                            using (MessageReader mr = new MessageReader(receiveBuffer))
                            {
                                int messageTypeInt = mr.Read<int>();
                                if (messageTypeInt < 0 || messageTypeInt >= Enum.GetValues(typeof(MessageType)).Length)
                                {
                                    Console.WriteLine("Bad type detected: " + messageTypeInt);
                                    Disconnect();
                                    return;
                                }
                                messageType = (MessageType)messageTypeInt;
                                receiveLeft = mr.Read<int>();
                                if (receiveLeft < 0 || receiveLeft > 1024)
                                {
                                    Console.WriteLine("Bad length detected: " + receiveLeft);
                                    Disconnect();
                                    return;
                                }
                            }
                            if (receiveLeft == 0)
                            {
                                //Console.WriteLine("Null message: " + messageType.ToString());
                                handler.HandleMessage(messageType, null);
                                receiveLeft = 8;
                            }
                            else
                            {
                                //Console.WriteLine("Header Received: " + messageType.ToString());
                                isHeader = false;
                            }
                            receivePos = 0;
                        }
                        else
                        {
                            //Console.WriteLine("Message Received: " + messageType.ToString());
                            handler.HandleMessage(messageType, receiveBuffer);
                            receiveLeft = 8;
                            receivePos = 0;
                            isHeader = true;
                        }
                    }
                    tcpClient.Client.BeginReceive(receiveBuffer, receivePos, receiveLeft, SocketFlags.None, ProcessData, null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Receive error from " + ipEndpoint + ": " + e);
                Disconnect();
            }
        }

        public void SendMessage(MessageType mt, byte[] data)
        {
            if (!connected)
            {
                return;
            }
            byte[] headerData = null;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>((int)mt);
                if (data != null && data.Length > 0)
                {
                    mw.Write<int>(data.Length);
                }
                else
                {
                    mw.Write<int>(0);
                }
                headerData = mw.GetMessageBytes();
            }
            byte[] sendData = null;
            if (data != null && data.Length > 0)
            {
                sendData = new byte[headerData.Length + data.Length];
                Array.Copy(headerData, sendData, headerData.Length);
                Array.Copy(data, 0, sendData, headerData.Length, data.Length);
            }
            else
            {
                sendData = headerData;
            }
            try
            {
                tcpClient.Client.Send(sendData);
            }
            catch
            {
                Disconnect();
            }
        }

        public IPEndPoint GetIPEndpoint()
        {
            return ipEndpoint;
        }

        public DataHandler GetHandler()
        {
            return handler;
        }

		private void Disconnect()
		{
			connected = false;
			Console.WriteLine("Disconnected " + tcpClient.Client.RemoteEndPoint);
			if (disconnectHandler != null)
			{
				disconnectHandler(this);
			}
		}
	}
}
