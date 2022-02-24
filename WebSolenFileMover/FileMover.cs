using System;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Threading;
using System.Security.AccessControl;
using System.Collections.Generic;
using System.Globalization;

namespace WebSolenFileMover
{
    class FileMover
    {
        private EventLog eventLog;
        private volatile bool stop;
        private string sourceDirectory;
        private string destinationDirectory;
        private string logDirectory;
        private bool shouldResetPermissions;
        private Dictionary<string, int> loggedTargets = new Dictionary<string, int>();

        public int ExitCode { get; private set; } = 0;

        public FileMover(EventLog eventLog)
        {
            this.eventLog = eventLog;
        }

        public void Start()
        {
            sourceDirectory = ConfigurationManager.AppSettings["SourceDirectory"];
            if (!Directory.Exists(sourceDirectory))
            {
                LogError("Invalid source directory.", sourceDirectory, null, 30);
                ExitCode = 1;
                return;
            }
            destinationDirectory = ConfigurationManager.AppSettings["DestinationDirectory"];
            if (!Directory.Exists(destinationDirectory))
            {
                LogError("Invalid destination directory.", destinationDirectory, null, 30);
                ExitCode = 1;
                return;
            }
            string boolString = ConfigurationManager.AppSettings["ResetPermissionsAfterMove"];
            if (!bool.TryParse(boolString, out shouldResetPermissions))
            {
                LogError("Invalid value for ResetPermissionsAfterMover. Valid values are \"true\" or \"false\".", boolString, null, 30);
                ExitCode = 1;
                return;
            }
            logDirectory = ConfigurationManager.AppSettings["LogDirectory"];
            if (!string.IsNullOrEmpty(logDirectory))
            {
                try
                {
                    Directory.CreateDirectory(logDirectory);
                    string path = Path.Combine(logDirectory, Path.GetRandomFileName());
                    using (File.Create(path, 1, FileOptions.DeleteOnClose)) { }
                }
                catch (IOException ex)
                {
                    LogError("Failed to initialize log directory.", logDirectory, ex, 30);
                    ExitCode = 1;
                    return;
                }
            }
            Thread thread = new Thread(delegate ()
            {
                try
                {
                    MoveFiles();
                }
                catch (Exception ex)
                {
                    LogError("An unhandled exception occurred. Service stopped.", null, ex, 30);
                    ExitCode = 1;
                }
            });
            thread.Start();
        }

        public void Stop()
        {
            stop = true;
        }

        private string GetDestinationDirectory(string fileName)
        {
            if (null == fileName || fileName.Length < 14)
            {
                return null;
            }
            for (int i = 0; i < 10; i++)
            {
                if (fileName[i] < '0' || fileName[i] > '9')
                {
                    return null;
                }
            }
            return Path.Combine(
                destinationDirectory,
                fileName.Substring(0, 2),
                fileName.Substring(0, 6) + "-" + fileName.Substring(6, 4)
            );
        }

        private static void ResetPermissions(string path)
        {
            var fileSecurity = new FileSecurity();
            fileSecurity.SetAccessRuleProtection(false, false);
            File.SetAccessControl(path, fileSecurity);
        }

        private void LogError(string msg, string target, Exception ex, int id, bool delayed = false)
        {
            if (eventLog == null)
            {
                return;
            }
            // Check if we have logged this message before to avoid
            // unnecessary spamming of the event log if a file gets stuck.
            string key = $"{id}{target}";
            if (loggedTargets.TryGetValue(key, out int retries))
            {
                if (retries < 0)
                {
                    return;
                }
                else if (retries > 0)
                {
                    loggedTargets[key]--;
                    return;
                }
                loggedTargets[key] = -1;
            }
            else
            {
                if (delayed)
                {
                    loggedTargets.Add(key, 15);
                    return;
                }
                else
                {
                    loggedTargets.Add(key, -1);
                }
            }
            if (string.IsNullOrEmpty(logDirectory))
            {
                eventLog.WriteEntry(
                    $"{msg} To enable detailed logging, specify a logging directory in the config file.",
                    EventLogEntryType.Error,
                    id
                );
            }
            else
            {
                string logPath = Path.Combine(
                    logDirectory,
                    DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".txt"
                );
                File.WriteAllText(logPath, $"{msg}\r\n\r\nTarget: {target}\r\n\r\n{ex?.ToString()}");
                eventLog.WriteEntry(
                    $"{msg} More details found in log file {logPath}.",
                    EventLogEntryType.Error,
                    id
                );
            }
        }

        private void MoveFiles()
        {
            while (!stop)
            {
                string[] files = Directory.GetFiles(sourceDirectory, "*.pdf", SearchOption.TopDirectoryOnly);
                foreach (string sourcePath in files)
                {
                    if (stop)
                    {
                        return;
                    }
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = GetDestinationDirectory(fileName);
                    if (string.IsNullOrEmpty(destPath))
                    {
                        continue;
                    }
                    try
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        LogError("Failed to create destination directory.", destPath, ex, 20);
                        continue;
                    }
                    destPath = Path.Combine(destPath, fileName);
                    if (File.Exists(destPath))
                    {
                        LogError("A file with the same name already exists in the destination dirctory.", destPath, null, 40);
                        try
                        {
                            File.Delete(sourcePath);
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        {
                            LogError("Failed to delete file.", sourcePath, ex, 10, true);
                        }
                        continue;
                    }
                    try
                    {
                        File.Move(sourcePath, destPath);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        LogError("Failed to move file.", destPath, ex, 20, true);
                        continue;
                    }
                    if (shouldResetPermissions)
                    {
                        try
                        {
                            ResetPermissions(destPath);
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        {
                            LogError("Failed to reset permissions on file.", destPath, ex, 20);
                        }
                    }
                }
                if (!stop)
                {
                    Thread.Sleep(3000);
                }               
            }
        }
    }
}