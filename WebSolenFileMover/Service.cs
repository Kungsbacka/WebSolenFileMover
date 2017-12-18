using System.Diagnostics;
using System.ServiceProcess;

namespace WebSolenFileMover
{
    public class Service : ServiceBase
    {
        private FileMover fileMover;

        public Service()
        {
            if (!EventLog.SourceExists("WebSolenFileMover"))
            {
                EventLog.CreateEventSource("WebSolenFileMover", "WebSolenFileMover");
            }
            EventLog.Source = "WebSolenFileMover";
            EventLog.Log = "WebSolenFileMover";
            fileMover = new FileMover(EventLog);
        }

        protected override void OnStart(string[] args)
        {
            fileMover.Start();
            ExitCode = fileMover.ExitCode;
        }

        protected override void OnStop()
        {
            fileMover.Stop();
        }
    }
}
