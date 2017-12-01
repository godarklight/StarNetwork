using System;
using System.Net;
namespace StarNetwork
{
    public class UDPSibling
    {
        public Guid guid;
        public IPEndPoint endpoint;
        public bool isConnected = false;
        public long rtt;
        public long clockDiff;

        public void ReportNTP()
        {
            double rttMs = Math.Round(rtt / (double)TimeSpan.TicksPerMillisecond, 3);
            double clockDiffS = Math.Round(clockDiff / (double)TimeSpan.TicksPerSecond, 3);
            Console.WriteLine(endpoint + " - RTT: " + rttMs + "ms, Diff: " + clockDiffS + "s.");
        }
    }
}
