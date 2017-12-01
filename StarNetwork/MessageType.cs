using System;
namespace StarNetwork
{
	public enum MessageType
	{
		HEARTBEAT,
        GUID,
		CONNECT_ENDPOINT,
		DISCONNECT_ENDPOINT,
        NTP_MESSAGE,
        NTP_COMPLETE,
        DISCOVER,
        SEND_UDP_ENDPOINT
	}
}
