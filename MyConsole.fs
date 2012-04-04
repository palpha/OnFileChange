namespace OnFileChange

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.IO
open System.Diagnostics
open System.Threading
open System.Text.RegularExpressions
open Arguments

module MyConsole =
    type returnCodes =
        | Ok = 0
        | ArgumentMissing = 1
        | NothingToWatch = 2
        | InvalidRegularExpression = 3

    let mutable (action : string) = null
    let mutable useProfile = false
    let mutable delay = 100
    let mutable pattern = String.Empty
    let eventTimers = ConcurrentDictionary<string, Timer>()

    let runCommand cmd changeType file =
        // strip start and end quotes
        let mutable bareCmd = Regex.Replace(cmd, "(^\"|\"$)", String.Empty)
        
        // wrap in script block with params
        bareCmd <- "\"& { param ($changeType, $file) " + bareCmd + " } '" + changeType + "' '" + file + "' \""

        let mutable commandStr = "-command " + bareCmd
        if not useProfile then
            commandStr <- "-noprofile " + commandStr
        let psi = new System.Diagnostics.ProcessStartInfo("powershell", commandStr)
        psi.UseShellExecute <- false
        let p = System.Diagnostics.Process.Start(psi)
        p.WaitForExit()
        p.ExitCode

    let handleEvent changeType fullPath =
        Console.WriteLine ("{0}: {1}", changeType, fullPath)
        runCommand action (changeType.ToString()) fullPath |> ignore
        eventTimers.TryRemove(fullPath) |> ignore

    let queueEvent (args : FileSystemEventArgs) =
        if pattern = null || Regex.IsMatch(args.FullPath, pattern) then
            let timer =
                new Timer(
                    (fun x -> handleEvent args.ChangeType args.FullPath),
                    null, delay, Timeout.Infinite)
            if eventTimers.TryAdd(args.FullPath, timer) = false then
                timer.Dispose()
        
    let onChange (args : FileSystemEventArgs) =
        queueEvent args

    let extractFiles (filesStr : string) =
        filesStr.Split(',')
        |> Array.map (fun x -> FileInfo(x))
        |> Seq.filter (fun x -> x.Exists)
        |> Seq.map (fun x -> x.DirectoryName, x.Name, false)

    let watchFile (path, filter, watchSubdir) =
        let effectiveFilter =
            if filter = null then pattern
            else filter
        Console.WriteLine ("Watching {0} in {1}", effectiveFilter, path)
        let fsw = new FileSystemWatcher()
        fsw.Path <- path
        if filter <> null then
            fsw.Filter <- filter
        fsw.Changed.Add(onChange)
        fsw.Deleted.Add(onChange)
        fsw.Created.Add(onChange)
        fsw.Renamed.Add(onChange)
        fsw.NotifyFilter <- NotifyFilters.FileName ||| NotifyFilters.LastWrite
        fsw.IncludeSubdirectories <- watchSubdir
        fsw.EnableRaisingEvents <- true
        fsw

    let setupWatchers (parsedArgs : Dictionary<string, string>) =
        let watchers = List<FileSystemWatcher>()

        if (parsedArgs.ContainsKey("files")) then
            parsedArgs.Item("files")
            |> extractFiles
            |> Seq.map watchFile
            |> Seq.iter watchers.Add
        else
            pattern <- parsedArgs.Item("filter")
            watchFile (Environment.CurrentDirectory, null, true)
            |> watchers.Add

        if watchers.Count = 0 then
            Console.WriteLine "Nothing to watch."
            returnCodes.NothingToWatch
        else
            Console.WriteLine "Press any key to stop watching."
            Console.ReadLine() |> ignore

            watchers |> Seq.iter (fun x -> x.EnableRaisingEvents <- false; x.Dispose())
            returnCodes.Ok

    let setupAction (parsedArgs : Dictionary<string, string>) =
        if parsedArgs.ContainsKey("action") then
            action <- parsedArgs.Item("action")
        else
            let poolName = parsedArgs.Item("recycle")
            action <- sprintf "\"& $env:windir\\system32\\inetsrv\\appcmd recycle apppool /apppool.name:%s\"" poolName
        
        parsedArgs

    let validateFilter (parsedArgs : Dictionary<string, string>) =
        if parsedArgs.ContainsKey("filter") then
            try
                Regex.IsMatch(String.Empty, parsedArgs.Item("filter")) |> ignore
                true
            with
            | _ -> false
        else true

    let Run args =
        let defs = [
                { ArgInfo.Command="files"; ArgInfo.Alias="f"; Description="List of files to watch" }
                { ArgInfo.Command="filter"; ArgInfo.Alias="ft"; Description="Filename filter (.NET regular expression)" }
                { ArgInfo.Command="action"; ArgInfo.Alias="a"; Description="PowerShell command ($changeType and $file will exist)" }
                { ArgInfo.Command="recycle"; ArgInfo.Alias="r"; Description="Name of IIS Application Pool to recycle" }
                { ArgInfo.Command="delay"; ArgInfo.Alias="d"; Description="Milliseconds to wait before reacting to a change" }
                { ArgInfo.Command="profile"; ArgInfo.Alias="p"; Description="Run PowerShell command without -NoProfile" }
            ]

        let parsedArgs = Arguments.ParseArgs args defs

        if parsedArgs.ContainsKey("delay") && Int32.TryParse(parsedArgs.Item("delay"), &delay) = false then
            delay <- 100
        if parsedArgs.ContainsKey("profile") then
            useProfile <- true

        if parsedArgs.ContainsKey("help") then
            returnCodes.Ok
        elif not (parsedArgs.ContainsKey("action") || parsedArgs.ContainsKey("recycle")) then
            Console.WriteLine "Please specify either --action or --recycle."
            returnCodes.ArgumentMissing
        elif not (parsedArgs.ContainsKey("files") || parsedArgs.ContainsKey("filter")) then
            Console.WriteLine "Please specify either --files or --filter."
            returnCodes.ArgumentMissing
        elif not (validateFilter parsedArgs) then
            Console.WriteLine "Please specify --filter as a valid regular expression."
            returnCodes.InvalidRegularExpression
        else
            parsedArgs
            |> setupAction
            |> setupWatchers
