using System;

namespace StarNetwork
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			if (args.Length > 0 && args[0] == "--server")
			{
				Console.WriteLine("Running star server on port 2076!");
				StarServer ss = new StarServer();
				ss.Run();
			}
			else
			{
				StarClient sc = new StarClient();
				sc.Run();
			}
		}
	}
}
