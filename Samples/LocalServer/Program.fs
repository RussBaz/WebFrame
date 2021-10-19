open System.IO
open WebFrame

[<EntryPoint>]
let main argv =
    let app = App ()
    
    let location =
        argv
        |> Array.tryHead
        |> Option.map Path.GetFullPath
        |> Option.defaultWith ( fun _ -> invalidArg "path" "Could not find the specified file path." )
    
    app.Services.StaticFiles.Enabled <- true
    app.Services.StaticFiles.AllowBrowsing <- true
    app.Services.StaticFiles.WebRoot <- "."
    
    app.Services.ContentRoot <- location
    
    printfn $"Displaying contents of the following folder: {location}"
    
    app.Run ()
    
    0 // return an integer exit code