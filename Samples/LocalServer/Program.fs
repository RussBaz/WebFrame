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
    
    app.Services.StaticFiles.Enabled <- true // Serving Static Files is disable by default
    app.Services.StaticFiles.AllowBrowsing <- true // Only enable this if absolutely necessary
    app.Services.StaticFiles.WebRoot <- "." // Default location: wwwroot
    
    // The next line adds a prefix to all static files
    // and it must be a valid path
    // app.Services.StaticFiles.Route <- "/static"
    
    app.Services.ContentRoot <- location
    
    printfn $"Displaying contents of the following folder: {location}"
    
    app.Run ()
    
    0 // return an integer exit code