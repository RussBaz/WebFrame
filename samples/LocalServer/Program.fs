open System
open System.IO

open WebFrame

// The first argument is the current program name 
let argv = Environment.GetCommandLineArgs ()

let app = App ()

let location =
    argv
    |> Array.tail
    |> Array.tryHead
    |> Option.map Path.GetFullPath
    |> Option.defaultWith ( fun _ -> invalidArg "path" "Could not find the specified file path." )

app.Log.Information $"Preparing to display contents of the following folder: {location}"

app.Services.StaticFiles.Enabled <- true // Serving Static Files is disabled by default
app.Services.StaticFiles.AllowBrowsing <- true // Only enable this if absolutely necessary
app.Services.StaticFiles.WebRoot <- "." // Default location: wwwroot

// The next line adds a prefix to all static files
// and it must be a valid path
// app.Services.StaticFiles.Route <- "/static"

// The root location for serving any files
app.Services.ContentRoot <- location

app.Log.Information $"Displaying contents of the following folder: {location}"

app.Run ()
