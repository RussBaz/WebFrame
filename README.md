# WebFrame
A massive helper F# framework for making rapid prototyping with ASP.NET Core pleasant.

## Introduction - A Story Time
Some long time ago I used to write web stuff using Python frameworks such as Django. More recently I got deeply into the F#. It satisfies a lot of my requirements. However, I was not satisfied with the current state of the F# web development. Every time I tried to write something quickly, I often had to choose between a heavily functional programming oriented frameworks or extremely tedious ASP.NET Core.

I wanted something quick. Why couldn't I just do the following?

```F#
open WebFrame

[<EntryPoint>]
let main _ =
    let app = App ()
    
    app.Get "/" <- fun serv -> serv.EndResponse "Hello World!"
    
    app.Run ()
    
    0 // exit code
```

So I did write it myself!

Yes, you can just write it and experience the full ASP.NET Core server!

There are a lot of helper methods available and mostly all of them are attached to the only (HttpServices) parameter that is passed to the handler on each request. This setup uses the endpoints api and all the routes can be inspected at any time.

This project is still work in progress and it is far from being a final product. Therefore - all contributions are absolutely welcome.

## Guiding Principles
Here are the guiding principals for the development and vision of the project:
* The common scenario should require the least amount of code
* The code should be obvious, self descriptive and visible
* Prefer existing F# syntax and avoid custom operators. Overloading is OK when it helps with previous points.
* Be recognisable to non-FP developers and web developers from other languages
* Make it easy to drop down to raw ASP.NET Core when the case requires it

Therefore, explicit is better than implicit but the speed of prototyping must always be considered. It is a number one priority.

* And finally, a beautiful code (in my eyes) is a better code.
## Setup for the first timers
For those who just start with F#, I recommend starting with the following website ['F# for Fun and Profit: F# syntax in 60 seconds'](https://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/).

Once you familiarise yourself with the syntax and install [.NET](https://dotnet.microsoft.com/download) runtime, you should check the `Samples` folder.

Clone this repo and copy the Minimal project into a new folder in your project folder. You need to adjust a project reference in the project file (called Minimal.fsproj in the example) to point at the correct location of the WebFrame.fsproj file.

This manual adjusting step is currently required because I have not published this project to any package repository and I have not created any other templates.

Once this is done, run the following command (in the same folder where your .fsproj file is) to start the server:

`dotnet run`

## Setup for an advanced audience
Create a new console or an empty asp core project with F#.

If it is a console project, add Web framework reference.

If it is a Web project, delete the Setup file and clean up the Program file.

Add WebFrame project reference and open it in the main file. It will immediately import all the required stuff for the minimal setup.

Please consider using [Paket](https://fsprojects.github.io/Paket/) if you do not mind (as it can reference GitHub projects directly)

Update the dependencies if required.

## Examples
Please check the Samples folder for examples of most of available apis.
* Minimal - most basic setup
* Modules - shows how to modularise the app
* LocalServer - shows how to work with the static files. It sets up a server that will share the specified folder (first command line argument) on the local network.
* TestServer - shows how you can access a virtual test server that can be used for testing. You can also check out the WebFrame.Test folder for more details on how to use it.
* AdvancedServer - a kitchen sink of most other available apis and helpers from the simplest to the most complicated
