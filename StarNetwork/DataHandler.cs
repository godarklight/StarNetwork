using System;
namespace StarNetwork
{
	public interface DataHandler
	{
		void HandleMessage(MessageType mt, byte[] data);
	}
}
