namespace OnFileChange

open System

module Program =   

    [<EntryPoint>]
    let Main(args) =
        int(MyConsole.Run args)