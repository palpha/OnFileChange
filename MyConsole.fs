namespace OnFileChange

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.IO
open System.Diagnostics
open System.Threading
open Arguments

module MyConsole =
    let mutable action = ""
    let mutable delay = 100
    let eventTimers = ConcurrentDictionary<string, Timer>()

    let buildEventKey changeType fullPath =
        sprintf "%s%s" (changeType.ToString()) fullPath

    let runCommand cmd =
        let psi = new System.Diagnostics.ProcessStartInfo("powershell", "-command " + cmd)
        psi.UseShellExecute <- false
        let p = System.Diagnostics.Process.Start(psi)
        p.WaitForExit()
        p.ExitCode

    let handleEvent changeType fullPath =
        Console.WriteLine("{0}: {1}", changeType, fullPath)
        runCommand action |> ignore
        let eventKey = buildEventKey changeType fullPath
        eventTimers.TryRemove(eventKey) |> ignore

    let queueEvent (args : FileSystemEventArgs) =
        let eventKey = buildEventKey args.ChangeType args.FullPath
        let timer =
            new Timer(
                (fun x -> handleEvent args.ChangeType args.FullPath),
                null, delay, Timeout.Infinite)
        if eventTimers.TryAdd(eventKey, timer) = false then
            timer.Dispose()
        
    let onChange (args : FileSystemEventArgs) =
        queueEvent args

    let extractFiles (filesStr : string) =
        filesStr.Split(',')
        |> Array.map (fun x -> FileInfo(x))
        |> Seq.filter (fun x -> x.Exists)
        |> Seq.map (fun x -> x.DirectoryName, x.Name)

    let watchFile (path, filter) =
        Console.WriteLine("Watching {0} in {1}", filter, path)
        let fsw = new FileSystemWatcher(path, filter)
        fsw.Changed.Add(onChange)
        fsw.NotifyFilter <- NotifyFilters.FileName ||| NotifyFilters.LastWrite
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
            watchFile (Environment.CurrentDirectory, parsedArgs.Item("filter"))
            |> watchers.Add

        watchers

    let setupAction (parsedArgs : Dictionary<string, string>) =
        if parsedArgs.ContainsKey("action") then
            action <- parsedArgs.Item("action")
        else
            let poolName = parsedArgs.Item("recycle")
            action <- sprintf "\"& $env:windir\\system32\\inetsrv\\appcmd recycle apppool /apppool.name:%s\"" poolName
        
        parsedArgs

    let Run args =
        let defs = [
                { ArgInfo.Command="files"; ArgInfo.Alias="f"; Description="List of files to watch"; Required=false }
                { ArgInfo.Command="filter"; ArgInfo.Alias="ft"; Description="Filename filter"; Required=false }
                { ArgInfo.Command="action"; ArgInfo.Alias="a"; Description="PowerShell script block"; Required=false }
                { ArgInfo.Command="recycle"; ArgInfo.Alias="r"; Description="Name of IIS Application Pool to recycle"; Required=false }
                { ArgInfo.Command="delay"; ArgInfo.Alias="d"; Description="Milliseconds to wait before reacting to a change"; Required=false }
            ]

        let parsedArgs = Arguments.ParseArgs args defs

        if not (parsedArgs.ContainsKey("action") || parsedArgs.ContainsKey("recycle")) then
            Console.WriteLine("Please specify either --action or --recycle.")
            1
        elif not (parsedArgs.ContainsKey("files") || parsedArgs.ContainsKey("filter")) then
            Console.WriteLine("Please specify either --files or --filter.")
            2
        else
            if parsedArgs.ContainsKey("delay") && Int32.TryParse(parsedArgs.Item("delay"), &delay) = false then
                delay <- 100
        
            let watchers =
                parsedArgs
                |> setupAction
                |> setupWatchers

            if watchers.Count = 0 then
                Console.WriteLine("Nothing to watch.")
                3
            else
                Console.WriteLine("Press any key to stop watching.")
                Console.ReadLine() |> ignore

                watchers |> Seq.iter (fun x -> x.EnableRaisingEvents <- false; x.Dispose())

                0
