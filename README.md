Description
-----------
A simple tool that watches files/directories for changes and does something
when a change happens. Its primary purpose is recycling IIS application pools,
but it can do anything you can do in PowerShell.

License
-------
See LICENSE.

Requirements
------------
.NET 4 Client Profile, msbuild.

Installation
------------
Build .sln file with msbuild or Visual Studio (2010 or higher).

Usage
-----
OnFileChange --help will present a smörgåsbord of options.

If you need to recycle application pools, you'll need to run the tool in an
elevated command prompt/PowerShell.

Examples
--------
    OnFileChange --recycle SomeAppPool --filter "\.config$"
    OnFileChange --action "Write-Host $file $changeType" --files some.txt,other.txt
    OnFileChange -a "msbuild myproj.csproj" -ft "\.(resx|cs)$" --delay 1000

Feedback
--------
If you find this tool useful, or if you have any suggestions,
do send a message or a tweet my way.
