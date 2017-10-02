using System;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Threading;
using System.Security.AccessControl;
using System.Collections.Generic;

namespace WebSolenFileMover
{
    class FileMover
    {
        private EventLog eventLog;
        private volatile bool stop;
        private string sourceDirectory;
        private string destinationDirectory;
        private bool shouldResetPermissions;
        private HashSet<int> loggedTargets;

        public FileMover(EventLog eventLog)
        {
            this.eventLog = eventLog;
            loggedTargets = new HashSet<int>();
        }

        public void Start()
        {
            sourceDirectory = ConfigurationManager.AppSettings["SourceDirectory"];
            if (!Directory.Exists(sourceDirectory))
            {
                LogError("Invalid source directory", sourceDirectory, null, 30);
                return;
            }
            destinationDirectory = ConfigurationManager.AppSettings["DestinationDirectory"];
            if (!Directory.Exists(destinationDirectory))
            {
                LogError("Invalid destination directory", destinationDirectory, null, 30);
                return;
            }
            string value = ConfigurationManager.AppSettings["ResetPermissionsAfterMove"];
            if (!bool.TryParse(value, out shouldResetPermissions))
            {
                LogError("Invalid value for ResetPermissionsAfterMover. Valid values are \"true\" or \"false\"", value, null, 30);
                return;
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

        private void LogError(string msg, string target, Exception ex, int id)
        {
            if (null == eventLog)
            {
                return;
            }
            // Check if we have logged this message before to avoid
            // unnecessary spamming of the event log if a file gets stuck.
            int hash = $"{id}:{target}".GetHashCode();
            if (loggedTargets.Contains(hash))
            {
                return;
            }
            loggedTargets.Add(hash);
            eventLog.WriteEntry($"{msg}\r\n\r\nTarget: {target}\r\n\r\n{ex?.ToString()}", EventLogEntryType.Error, id);
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
                    catch (IOException ex)
                    {
                        LogError("Failed to create destination directory", destPath, ex, 20);
                        continue;
                    }
                    destPath = Path.Combine(destPath, fileName);
                    if (File.Exists(destPath))
                    {
                        try
                        {
                            File.Delete(sourcePath);
                        }
                        catch (IOException ex)
                        {
                            LogError("Failed to remove file", sourcePath, ex, 10);
                        }
                        continue;
                    }
                    try
                    {
                        File.Move(sourcePath, destPath);
                    }
                    catch (IOException ex)
                    {
                        LogError("Failed to move file", destPath, ex, 20);
                        continue;
                    }
                    if (shouldResetPermissions)
                    {
                        try
                        {
                            ResetPermissions(destPath);
                        }
                        catch (IOException ex)
                        {
                            LogError("Failed to reset permissions", destPath, ex, 20);
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