using System.Diagnostics;
using System.ServiceProcess;

namespace WebSolenFileMover
{
    class Program
    {
        static void Main()
        {
            var services = new ServiceBase[] { new Service() };
#if DEBUG
            var fileMover = new FileMover(services[0].EventLog);
            fileMover.Start();
            System.Console.WriteLine("Service started...");
            System.Console.WriteLine("Press ENTER to stop service");
            System.Console.ReadLine();
            fileMover.Stop();
#else
            ServiceBase.Run(services);
#endif
        }
    }
}
