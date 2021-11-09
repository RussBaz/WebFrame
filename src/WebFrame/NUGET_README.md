# WebFrame
[![Build Status](https://img.shields.io/github/workflow/status/RussBaz/WebFrame/.NET%20Core)](https://github.com/russbaz/webframe/actions/workflows/github-actions.yml)
[![Latest Published Nuget Version](https://img.shields.io/nuget/v/RussBaz.WebFrame)](https://www.nuget.org/packages/RussBaz.WebFrame/)
[![Latest Published Templates Version](https://img.shields.io/nuget/v/RussBaz.WebFrame.Templates?label=templates)](https://www.nuget.org/packages/RussBaz.WebFrame.Templates/)

F# framework for rapid prototyping with ASP.NET Core

## Quickstart
Documentation: https://github.com/RussBaz/WebFrame

Installation:

1. Get templates: `dotnet new --install RussBaz.WebFrame.Templates::*`
2. Create new project: `dotnet new webframe`

```F#
open WebFrame

[<EntryPoint>]
let main _ =
    let app = App ()
    
    app.Get "/" <- fun serv -> serv.EndResponse "Hello World!"
    
    app.Run ()
    
    0 // exit code
```
