open System.IO

let extractFiles (filesStr : string) =
    filesStr.Split(',')
    |> Array.map (fun x -> FileInfo(x))
    |> Seq.map (fun x -> x.DirectoryName, x.Name)

extractFiles "c:\\autoexec.bat"