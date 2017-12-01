using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MessageStream2;
namespace StarNetwork
{
    public class StarClient
    {
        Guid udpAddress;
        bool connected = true;
        NetworkStream ns = null;
        List<UDPSibling> udpSiblings = new List<UDPSibling>();
        UdpClient udpClient;
        public void Run()
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            udpClient.BeginReceive(ReceiveUDPMessage, null);
            Console.WriteLine("Using UDP port " + udpClient.Client.LocalEndPoint);
            IPAddress[] addresses = Dns.GetHostAddresses("godarklight.info.tm");
            IPAddress address = IPAddress.None;
            foreach (IPAddress possibleAddress in addresses)
            {
                if (possibleAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    address = possibleAddress;
                }
            }
            if (address == IPAddress.None)
            {
                Console.WriteLine("Unable to lookup godarklight.info.tm");
                return;
            }
            TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                tcpClient.Connect(new IPEndPoint(address, 2076));
            }
            catch
            {
                Console.WriteLine("Unable to connect to godarklight.info.tm");
                return;
            }
            ClientMessageHandling cmh = new ClientMessageHandling(AddClient, RemoveClient, SetUDPAddress);
            ns = new NetworkStream(tcpClient, cmh, Disconnect);
            cmh.SetNetworkStream(ns);
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>(((IPEndPoint)udpClient.Client.LocalEndPoint).Port);
                ns.SendMessage(MessageType.SEND_UDP_ENDPOINT, mw.GetMessageBytes());
            }
            while (connected)
            {
                ns.SendMessage(MessageType.HEARTBEAT, null);
                SendNTP();
                cmh.ReportNTP();
                if (udpAddress != Guid.Empty)
                {
                    List<UDPSibling> udpSiblingsCopy = new List<UDPSibling>(udpSiblings);
                    foreach (UDPSibling udps in udpSiblingsCopy)
                    {
                        if (udps.isConnected == false)
                        {
                            Console.WriteLine("Sending discover to " + udps.guid);
                            SendUDPDiscover(udps, false);
                        }
                        else
                        {
                            Console.WriteLine("Sending NTP to " + udps.guid);
                            SendUDPNTP(udps);
                            udps.ReportNTP();
                        }
                    }
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        //This really isn't at all thought out, but I'm going to leave it now because.
        private void SendUDPMessage(UDPSibling udps, MessageType mt, byte[] data)
        {
            byte[] sendData = null;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<byte[]>(udpAddress.ToByteArray());
                mw.Write<int>((int)mt);
                if (data == null || data.Length == 0)
                {
                    mw.Write<int>(0);
                }
                else
                {
                    mw.Write<int>(data.Length);
                    mw.Write<byte[]>(data);
                }
                sendData = mw.GetMessageBytes();
            }
            udpClient.Send(sendData, sendData.Length, udps.endpoint);
        }

        private void ReceiveUDPMessage(IAsyncResult ar)
        {
            IPEndPoint receiveEndpoint = null;
            try
            {
                byte[] udpMessage = udpClient.EndReceive(ar, ref receiveEndpoint);
                //Console.WriteLine("UDP size: " + udpMessage.Length);
                using (MessageReader mr = new MessageReader(udpMessage))
                {
                    Guid fromGuid = new Guid(mr.Read<byte[]>());
                    MessageType messageType = (MessageType)mr.Read<int>();
                    int dataLength = mr.Read<int>();
                    if (dataLength > 0)
                    {
                        byte[] messageData = mr.Read<byte[]>();
                        //Console.WriteLine("Message length: " + messageData.Length);
                        HandleUDPMessage(fromGuid, receiveEndpoint, messageType, messageData);
                    }
                    else
                    {
                        HandleUDPMessage(fromGuid, receiveEndpoint, messageType, null);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("UDP Error from " + (IPEndPoint)receiveEndpoint + ", exception: " + e);
            }
            udpClient.BeginReceive(ReceiveUDPMessage, null);
        }

        private void HandleUDPMessage(Guid fromGuid, IPEndPoint endpoint, MessageType messageType, byte[] data)
        {
            UDPSibling udps = null;
            foreach (UDPSibling udpsother in udpSiblings)
            {
                if (udpsother.guid == fromGuid)
                {
                    udps = udpsother;
                    break;
                }
            }
            if (udps == null)
            {
                Console.WriteLine("Received UDP message from unknown client");
                return;
            }
            switch (messageType)
            {
                case MessageType.DISCOVER:
                    HandleUDPDiscover(udps, endpoint, data);
                    break;
                case MessageType.NTP_MESSAGE:
                    HandleUDPNTP(udps, data);
                    break;
                case MessageType.NTP_COMPLETE:
                    HandleUDPNTPComplete(udps, data);
                    break;
            }
        }

        private void HandleUDPDiscover(UDPSibling udps, IPEndPoint endpoint, byte[] data)
        {
            udps.isConnected = true;
            udps.endpoint = endpoint;
            using (MessageReader mr = new MessageReader(data))
            {
                bool isResponse = mr.Read<bool>();
                if (!isResponse)
                {
                    SendUDPDiscover(udps, true);
                }
            }
        }

        private void HandleUDPNTP(UDPSibling udps, byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                long sendTime = mr.Read<long>();
                long peerTime = DateTime.UtcNow.Ticks;
                using (MessageWriter mw = new MessageWriter())
                {
                    mw.Write<long>(sendTime);
                    mw.Write<long>(peerTime);
                    SendUDPMessage(udps, MessageType.NTP_COMPLETE, mw.GetMessageBytes());
                }
            }
        }

        private void HandleUDPNTPComplete(UDPSibling udps, byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                long sendTime = mr.Read<long>();
                long peerTime = mr.Read<long>();
                long receiveTime = DateTime.UtcNow.Ticks;
                udps.rtt = receiveTime - sendTime;
                udps.clockDiff = (sendTime + (udps.rtt / 2)) - peerTime;
            }
        }

        private void SendUDPDiscover(UDPSibling udps, bool isResponse)
        {
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<bool>(isResponse);
                SendUDPMessage(udps, MessageType.DISCOVER, mw.GetMessageBytes());
            }
        }

        private void SendUDPNTP(UDPSibling udps)
        {
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<long>(DateTime.UtcNow.Ticks);
                SendUDPMessage(udps, MessageType.NTP_MESSAGE, mw.GetMessageBytes());
            }
        }

        private void SetUDPAddress(Guid udpAddress)
        {
            Console.WriteLine("Set UDP GUID to " + udpAddress);
            this.udpAddress = udpAddress;
        }

        private void SendNTP()
        {
            using (MessageWriter mw = new MessageWriter())
            {
                long timeNow = DateTime.UtcNow.Ticks;
                mw.Write<long>(timeNow);
                ns.SendMessage(MessageType.NTP_MESSAGE, mw.GetMessageBytes());
            }
        }

        private void AddClient(IPEndPoint endpoint, Guid udpAddress)
        {
            UDPSibling udps = new UDPSibling();
            udps.guid = udpAddress;
            udps.endpoint = endpoint;
            lock (udpSiblings)
            {
                udpSiblings.Add(udps);
            }
        }


        private void RemoveClient(Guid udpAddress)
        {
            UDPSibling udps = null;
            lock (udpSiblings)
            {
                foreach (UDPSibling udpsother in udpSiblings)
                {
                    if (udpsother.guid == udpAddress)
                    {
                        udps = udpsother;
                        break;
                    }
                }
                udpSiblings.Remove(udps);
            }
        }

        private void Disconnect(NetworkStream ns)
        {
            connected = false;
        }
    }
}
