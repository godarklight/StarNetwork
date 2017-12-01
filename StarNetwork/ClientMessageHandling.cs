using System;
using System.Net;
using MessageStream2;
namespace StarNetwork
{
    public class ClientMessageHandling : DataHandler
    {
        private NetworkStream ns;
        private long rtt;
        private long clockDiff;
        private Action<IPEndPoint, Guid> addClient;
        private Action<Guid> removeClient;
        private Action<Guid> setUDP;

        public ClientMessageHandling(Action<IPEndPoint, Guid> addClient, Action<Guid> removeClient, Action<Guid> setUDP)
        {
            this.addClient = addClient;
            this.removeClient = removeClient;
            this.setUDP = setUDP;
        }

        public void SetNetworkStream(NetworkStream ns)
        {
            this.ns = ns;
        }
        public void HandleMessage(MessageType mt, byte[] data)
        {
            switch (mt)
            {
                case MessageType.HEARTBEAT:
                    HandleHeartbeat();
                    break;
                case MessageType.GUID:
                    HandleUDPAddress(data);
                    break;
                case MessageType.CONNECT_ENDPOINT:
                    HandleConnectEndpoint(data);
                    break;
                case MessageType.DISCONNECT_ENDPOINT:
                    HandleDisconnectEndpoint(data);
                    break;
                case MessageType.NTP_MESSAGE:
                    HandleNTP(data);
                    break;
            }
        }
        private void HandleHeartbeat()
        {
            
        }

        private void HandleUDPAddress(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                Guid guid = new Guid(mr.Read<byte[]>());
                setUDP(guid);
            }
        }

        private void HandleNTP(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                long sendTime = mr.Read<long>();
                long serverTime = mr.Read<long>();
                long receiveTime = DateTime.UtcNow.Ticks;
                rtt = receiveTime - sendTime;
                clockDiff = serverTime - (sendTime + (rtt / 2));
                using (MessageWriter mw = new MessageWriter())
                {
                    mw.Write<long>(sendTime);
                    mw.Write<long>(serverTime);
                    mw.Write<long>(receiveTime);
                    ns.SendMessage(MessageType.NTP_COMPLETE, mw.GetMessageBytes());
                }
            }
        }

        private void HandleConnectEndpoint(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                string ipEndPoint = mr.Read<string>();
                Guid udpAddress = new Guid(mr.Read<byte[]>());
                string address = ipEndPoint.Substring(0, ipEndPoint.LastIndexOf(":", StringComparison.InvariantCultureIgnoreCase));
                string port = ipEndPoint.Substring(ipEndPoint.LastIndexOf(":", StringComparison.InvariantCultureIgnoreCase) + 1);
                Console.WriteLine(address + "  ----   " + port);
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(address), Int32.Parse(port));
                addClient(endpoint, udpAddress);
            }
        }

        private void HandleDisconnectEndpoint(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                Guid udpAddress = new Guid(mr.Read<byte[]>());
                removeClient(udpAddress);
            }
        }

        public void ReportNTP()
        {
            double rttMs = Math.Round(rtt / (double)TimeSpan.TicksPerMillisecond, 3);
            double clockDiffS = Math.Round(clockDiff / (double)TimeSpan.TicksPerSecond, 3);
            Console.WriteLine("Server - RTT: " + rttMs + "ms, Diff: " + clockDiffS + "s.");
        }
    }
}
