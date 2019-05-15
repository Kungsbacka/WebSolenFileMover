# WebSolenFileMover

A service that moves scanned documents from a source directory to a destination directory with a folder structure that can be consumed by WebSolen.

## Install

Rename App.example.config to App.config. Update configuration settings and build the service. [Visual Studio Community](https://www.visualstudio.com/vs/community/) can be used for this.

Create the folder C:\Program Files\WebSolenFileMover and copy WebSolenFileMover.exe and WebSolenFileMover.config to the folder.

When the service starts it will try to register a new event log and source. If the service account does not have permission to create the event log it has to be created manually before the service is started. To create the event log run the following command in an elevated PowerShell console:

    New-EventLog -LogName 'WebSolenFileMover' -Source 'WebSolenFileMover'

Either create the service running under LocalSystem:

    sc.exe create WebSolenFileMover binPath= "C:\Program Files\WebSolenFileMover\WebSolenFileMover.exe" start= auto

...or create the service running under a specified account:

    sc.exe create WebSolenFileMover binPath= "C:\Program Files\WebSolenFileMover\WebSolenFileMover.exe" start= auto obj= service-user password= secret

Start the service

    sc.exe start WebSolenFileMover

## Uninstall

Stop and delete the service:

    sc.exe stop WebSolenFileMover
    sc.exe delete WebSolenFileMover

Delete the custom event log (run in an elevated PowerShell console):

    Remove-EventLog -LogName 'WebSolenFileMover'

Remove the folder C:\Program Files\WebSolenFileMover

## Logging

Errors are always logged in a custom event log named WebSolenFilenMover. A second logging option can (and should) be enabled by configuring a log directory in App.config. The file log contains more detailed information about the error than the event log entry. The reason for the two logs is to avoid that sensitive information ends up in the event log and potentially gets shipped to an untrusted log store.
