namespace OnFileChange

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open System.Diagnostics
open System.Reflection
open System.IO

module Arguments =

    // Type for simple argument checking
    type ArgInfo = { Command:string; Alias:string; Description:string }

    let getAttribute<'T> () =
        let attrs = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof<'T>, false)
        match attrs.Length with
        | x when x > 0 -> Some(attrs.[0] :?> 'T)
        | _ -> None

    // Displays the help arguments
    let DisplayHelp (defs:ArgInfo list) =
        match defs with
        | [] -> Console.WriteLine "No help text defined."
        | _ ->
            let title =
                match getAttribute<AssemblyTitleAttribute>() with
                | Some(x) -> x.Title
                | None -> "OnFileChange"
            let version =
                match getAttribute<AssemblyVersionAttribute>() with
                | Some(x) -> x.Version
                | None -> "0.0.0.0"
            let description =
                match getAttribute<AssemblyDescriptionAttribute>() with
                | Some(x) -> x.Description
                | None -> "A tool that does stuff when files change."
            
            Console.WriteLine (
                sprintf "%s %s, %s"
                    title
                    (version.Substring(0, version.LastIndexOf('.')))
                    (description.ToLowerInvariant()))
            
            let executable =
                let mutable name = Environment.CommandLine.Split(' ').[0]
                name.Substring(name.LastIndexOf(Path.DirectorySeparatorChar) + 1)
            
            Console.WriteLine (Environment.NewLine + "Arguments:")
            let aliasWidth =
                defs
                |> Seq.map (fun x -> x.Alias.Length + 1)
                |> Seq.max
                |> string
            let commandWidth =
                defs
                |> Seq.map (fun x -> x.Command.Length + 1)
                |> Seq.max
                |> string
            defs
            |> List.iter (fun def ->
                let format = "  -{0,-" + aliasWidth + "} --{1,-" + commandWidth + "} {2}"
                let helpText = String.Format(format, def.Alias + ",", def.Command, def.Description)
                Console.WriteLine helpText)

    // Displays the found arguments
    let DisplayArgs (args:Dictionary<string, string>) =
        match args.Keys.Count with
        | 0 -> Console.WriteLine "No arguments found."
        | _ ->
            Console.WriteLine "Arguments found:"
            for arg in args.Keys do
                if String.IsNullOrEmpty(args.[arg]) then
                    Console.WriteLine (sprintf "-%s" arg)
                else
                    Console.WriteLine (sprintf "-%s '%s'" arg args.[arg])

    // Parse the input arguments
    let ParseArgs (args:string array) (defs:ArgInfo list) =

        let parsedArgs = new Dictionary<string, string>()

        // Ensure help is supported if defintions provided
        let fullDefs = 
            if not (List.exists (fun def -> String.Equals(def.Command, "help")) defs) then
                { ArgInfo.Command="help"; ArgInfo.Alias="h"; Description="Display help" } :: defs
            else
                defs

        // Report errors
        let reportError errorText =
            DisplayArgs parsedArgs
            DisplayHelp fullDefs
            let errMessage = sprintf "Error occured: %A" errorText
            Console.Error.WriteLine errMessage
            Console.Error.Flush()
            Environment.Exit(1)

        // Capture variables
        let captureArg command value =
            match defs with
            | [] -> parsedArgs.Add(command, value)
            | _ ->
                let finder def =
                    String.Equals("--" + def.Command, command) || String.Equals("-" + def.Alias, command)
                if not (List.exists finder fullDefs) then
                    reportError (sprintf "Command '%s' does not exist." command)
                else
                    let command = (List.find finder fullDefs).Command
                    if not (parsedArgs.ContainsKey(command)) then
                        parsedArgs.Add(command, value)

        let (|IsCommand|_|) (command:string) =
            let m = Regex.Match(command, "^(?<command>-{1,2}.*)$")
            if m.Success then Some(m.Groups.["command"].Value) else None

        let rec loop (argList:string list) =
            match argList with
            | [] -> ()
            | head::tail ->
                match head with
                | IsCommand command ->
                    match tail with
                    | [] -> captureArg command String.Empty
                    | iHead::iTail -> 
                        match iHead with
                        | IsCommand iCommand ->
                            captureArg command String.Empty
                            loop tail
                        | _ ->
                            captureArg command iHead
                            loop iTail
                | _ -> reportError (sprintf "Expected a command but got '%s'" head)
        loop (Array.toList args)

        // Look to see if help has been requested
        if (parsedArgs.ContainsKey("help")) then
            DisplayHelp defs
                  
        parsedArgs