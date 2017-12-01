using System;
using System.Net;
using MessageStream2;
namespace StarNetwork
{
	public class ServerMessageHandling : DataHandler
	{
        private long lastReceiveTime;
        private long clockDiff;
        private long rtt; //Round trip time
		private NetworkStream ns;
        private Action<NetworkStream, IPEndPoint> endpointCallback;
        public void SetNetworkStream(NetworkStream ns, Action<NetworkStream, IPEndPoint> endpointCallback)
		{
			this.ns = ns;
            this.endpointCallback = endpointCallback;
		}
		public void HandleMessage(MessageType mt, byte[] data)
		{
            lastReceiveTime = DateTime.UtcNow.Ticks;
			switch(mt)
			{
				case MessageType.HEARTBEAT:
					HandleHeartbeat();
					break;
                case MessageType.NTP_MESSAGE:
                    HandleNTP(data);
                    break;
                case MessageType.NTP_COMPLETE:
                    HandleNTPComplete(data);
                    break;
                case MessageType.SEND_UDP_ENDPOINT:
                    HandleUDPEndpoint(data);
                    break;
			}
		}

		private void HandleHeartbeat()
		{
			//Do nothing
		}

        private void HandleNTP(byte[] data)
        {
            using (MessageWriter mw = new MessageWriter())
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    long sendTime = mr.Read<long>();
                    long serverTime = DateTime.UtcNow.Ticks;
                    mw.Write<long>(sendTime);
                    mw.Write<long>(serverTime);
                    byte[] sendData = mw.GetMessageBytes();
                    ns.SendMessage(MessageType.NTP_MESSAGE, sendData);
                }
            }
        }

        private void HandleNTPComplete(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                long sendTime = mr.Read<long>();
                long serverTime = mr.Read<long>();
                long receiveTime = mr.Read<long>();
                rtt = receiveTime - sendTime;
                clockDiff = serverTime - (sendTime + (rtt / 2));
            }
        }

        private void HandleUDPEndpoint(byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                int port = mr.Read<int>();
                IPEndPoint ipEndpoint = new IPEndPoint(ns.GetIPEndpoint().Address, port);
                endpointCallback(ns, ipEndpoint);
            }
        }

        public void ReportNTP()
        {
            double rttMs = Math.Round(rtt / (double)TimeSpan.TicksPerMillisecond , 3);
            double clockDiffS = Math.Round(clockDiff / (double)TimeSpan.TicksPerSecond , 3);
            Console.WriteLine(ns.GetIPEndpoint() + " - RTT: " + rttMs + "ms, Diff: " + clockDiffS + "s.");
        }

        public long GetLastReceive()
        {
            return lastReceiveTime;
        }
	}
}
