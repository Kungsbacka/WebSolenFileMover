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
            EventLog.WriteEntry("Service started.", EventLogEntryType.Information, 1);
            fileMover.Start();
        }

        protected override void OnStop()
        {
            fileMover.Stop();
            EventLog.WriteEntry("Service stopped.", EventLogEntryType.Information, 2);
        }
    }
}
