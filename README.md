# WebFrame
[![Build Status](https://img.shields.io/github/workflow/status/RussBaz/WebFrame/.NET%20Core)](https://github.com/russbaz/webframe/actions/workflows/github-actions.yml)
[![Latest Published Nuget Version](https://img.shields.io/nuget/v/RussBaz.WebFrame)](https://www.nuget.org/packages/RussBaz.WebFrame/)
[![Latest Published Templates Version](https://img.shields.io/nuget/v/RussBaz.WebFrame.Templates?label=templates)](https://www.nuget.org/packages/RussBaz.WebFrame.Templates/)

F# framework for rapid prototyping with ASP.NET Core.

### Fast Travel
* [Introduction - A Story Time](#introduction---a-story-time)
* [Guiding Principles](#guiding-principles)
* [Setup](#setup)
  * [For the first timers](#for-the-first-timers)
  * [For an advanced audience](#for-an-advanced-audience)
* [Examples](#examples)
  * [Sample Code](#sample-code)
* [Documentation](#documentation)
  * [Main App](#main-app)
  * [Request Handling](#request-handling)
  * [Request Services](#request-services)
    * [Path Parts](#path-parts)
    * [Route Parts](#route-parts)
    * [Query Parts](#query-parts)
    * [Header Parts](#header-parts)
    * [Cookie Parts](#cookie-parts)
    * [Config Parts](#config-parts)
    * [Body Parts](#body-parts)
      * [Form](#form)
      * [Json](#json)
    * [Request Logging](#request-logging)
  * [Host Logging](#host-logging)
  * [System Configuration](#system-configuration)
  * [Request Helpers](#request-helpers)
  * [Modules](#modules)
  * [Testing](#testing)
  * [Exceptions](#exceptions)
* [Changelog](#changelog)

## Introduction - A Story Time
Some long time ago I used to write web stuff using Python frameworks such as Django. More recently I got deeply into the F#. It satisfies a lot of my requirements. However, I was not satisfied with the current state of the F# web development. Every time I tried to write something quickly, I often had to choose between a heavily functional programming oriented frameworks or extremely tedious ASP.NET Core.

I wanted something quick. Why couldn't I just do the following?

```F#
open WebFrame

let app = App ()

app.Get "/" <- fun serv -> serv.EndResponse "Hello World!"
app.Run ()
```

So I did write it myself!

Yes, you can just write it and experience the full ASP.NET Core server!

There are a lot of helper methods available and mostly all of them are attached to the only (RequestServices) parameter that is passed to the handler on each request. This setup uses the endpoints api and all the routes can be inspected at any time.

This project is still a work in progress and it is far from being a final product. Therefore - all contributions are absolutely welcome.

## Guiding Principles
Here are the guiding principals for the development and vision of the project:
* The common scenario should require the least amount of code
* The code should be obvious, self descriptive and visible
* Prefer existing F# syntax and avoid custom operators. Overloading is OK when it helps with previous points.
* Be recognisable to non-FP developers and web developers from other languages
* Make it easy to drop down to raw ASP.NET Core when the case requires it

Therefore, explicit is better than implicit but the speed of prototyping must always be considered. It is a number one priority.

* And finally, a beautiful code (in my eyes) is a better code.

## Setup
Before you begin, make sure that [.NET](https://dotnet.microsoft.com/download) 6.0+ is installed and reachable from your terminal (i.e. it is present in the Path environment variable)
### For the first timers
For those who just start with F#, I recommend starting with the following website ['F# for Fun and Profit: F# syntax in 60 seconds'](https://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/).

Once you familiarise yourself with the syntax and deal with the .Net runtime, you should check the `Samples` folder.

Install the minimal template:

```
dotnet new -i "RussBaz.WebFrame.Templates::*"
```

Create a new project just like you would normally do in a new directory of your choice:

```
dotnet new webframe
```

Once this is done, run the following command (in the same folder where your .fsproj file is) to start the server:

`dotnet run`

Note: you may need to restore the project before your IDE can correctly work with the project: `dotnet restore` and `dotnet build`

Recommended editors by personal preferences for F#:
* VS Code with Ionide-fsharp extension
* JetBrains Rider
* Visual Studio

### For an advanced audience
Create a new console or an empty asp core project with F#.

If it is a console project, add Web framework reference.

If it is a Web project, delete the Setup file and clean up the Program file.

Add WebFrame package reference and open it in the main file. It will immediately import all the required stuff for the minimal setup.

Please consider using [Paket](https://fsprojects.github.io/Paket/) if you do not mind (it can reference GitHub projects directly)

Update the dependencies if required.

## Examples
Please check the Samples folder for examples of most of available apis.
* Minimal - most basic setup
* Modules - shows how to modularise the app
* LocalServer - shows how to work with the static files. It sets up a server that will share the specified folder (first command line argument) on the local network.
* TestServer - shows how you can access a virtual test server that can be used for testing. You can also check out the WebFrame.Test folder for more details on how to use it.
* StandardServer - shows common scenarios 
* AdvancedServer - a kitchen sink of most other available apis and helpers from the simplest to the most complicated

### Sample Code
The following snippet shows some common scenarios.
```F#
open WebFrame
open type WebFrame.Endpoints.Helpers

//Sample exceptions
exception TeapotException

type ItemAlreadyExistsException ( itemName: string ) =
    inherit System.Exception $"Item {itemName} already exists."

[<EntryPoint>]
let main argv =
    let items = [ "todo1"; "todo2"; "todo3" ]
    
    let api = AppModule "/api"
    
    // Whenever a TeapotException is thrown in this module
    // It will be returning code 418
    api.Errors <- Error.codeFor<TeapotException> 418
    
    // And the same with "ItemAlreadyExistsException"
    // You can even catch the automatically captured exceptions
    api.Errors <- Error.codeFor<ItemAlreadyExistsException> 409
    
    // Returning items
    api.Get "/" <- fun serv ->
        serv.EndResponse items
        
    // Adding items
    // By sending an item Name as a string field in a form
    api.Post "/" <- fun serv ->
        // If a required property in user input is not found,
        // then 400 error is issued automatically
        let itemName = serv.Body.Form.Required<string> "name"
        
        // If you need to check the content type, you can try:
        
        // Is it a form? (bool property)
        // serv.Body.Form.IsPresent
        
        // Is it a json conent type? (bool property)
        // serv.Body.Json.IsJsonContentType
        
        // Is it a json (checks the content type)?
        // If yes, try validating it. (bool task method)
        // serv.Body.Json.IsPresent ()
        
        // In all other cases (string property):
        // serv.ContentType
        
        // ContentType is an empty string if the header is missing
        
        // To set a response Content-Type manually and quickly,
        // Just assign the required value to the same property
        // Reading it will still return the content type of the request
        serv.ContentType <- "text/plain"
        
        // This exception was already registered in the module
        // and it will be automatically handled
        if items |> List.contains itemName then raise ( ItemAlreadyExistsException itemName )
        
        if itemName = "coffee" then raise TeapotException
        
        serv.StatusCode <- 201
        printfn $"Faking a successful addition of a new item {itemName}"
    
        serv.EndResponse ()
    
    // Passing command arguments to the ASP.NET Core server
    let app = App argv
    
    // Serving Static Files is disabled by default
    app.Services.StaticFiles.Enabled <- true
    
    // Optionally adding a prefix to all static files
    app.Services.StaticFiles.Route <- "/static"
    
    // Please check the LocalServer sample for more information on the static files
    
    // Overriding config
    app.Config.[ "Hello" ] <- "World"
    // Overriding connecition string for "MyConnection"
    app.Config.ConnectionStrings "MyConnection" <- "host=localhost;"
    
    app.Get "/" <- page "Pages/Index.html"
    app.Get "/About" <- page "Pages/About.html"
    
    app.Get "/Hello" <- fun serv ->
        // Accessing configuration at runtime
        // The field naming syntax is the same as in ASP.NET Core
        let hello = serv.Config.Required<string> "hello"
        // Always present as well as some other web host properties
        let isDevelopment = serv.Config.IsDevelopment
        
        serv.EndResponse $"[ {hello} ] Dev mode: {isDevelopment}"
    
    app.Module "ToDoApi" <- api
    
    // Running on the default ports if not overriden in the settings
    app.Run ()
    
    0 // exit code
```
## Documentation
### Main App
How to create, build and run a WebFrame app
```F#
open System
open WebFrame

let argv = Environment.GetCommandLineArgs ()

// All it takes to create an app
let app = App ()

// If you want to pass command line arguments
// to the underlying ASP.NET Core server
let app = App argv

// To run an app (blocking mode)
// If the app is not yet built
// it will run the build step automatically
// However, it will not rebuild the app if it is already built
app.Run ()

// You can also specify directly the connection urls in this step
// This will override all existing 'urls' configurations
// Furthermore, it will force the app to be rebuilt even if it is already built
app.Run [ "http://localhost:5000" ]

// To Build an app manually before running it
// One can run this optional command
// Once the app is built, further changes to configs or endpoints
// will not take place untill the app is rebuilt
app.Build ()

// If you need to adjust some default WebFrame constants
// You can do the following
open WebFrame.Configuration

let defaults = { SystemDefaults.standard with SettingsPrefix = "MyApp" }
let app = App defaults

// You can still pass the args
let defaults = { defaults with Args = args }
let app = App defaults

// Lastly, if your app needs an access to dependency injection outside of request
// for example for writing an extension,
// you can request an IServiceProvider from the app directly
// The app has to be built when the method is requested
// If the app is rebuilt after requesting the provider,
// just call the method again to receive a fresh copy
let serviceProvider = app.GetServiceProvider ()

// If you need to execute something before and after the server runs
// You can add on start and on stop hooks
// The hook is a function that takes an instance of App and returns an unit
app.Hooks.AddOnStartHook ( fun a -> () )
app.Hooks.AddOnStopHook ( fun a -> () )
// and you can clear them all with the following methods
app.Hooks.ClearOnStartHooks ()
app.Hooks.ClearOnStopHooks ()

// For additional options for adjusting the defaults,
// please check the configuration section of the docs
```
### Request Handling
How to process incoming requests
```F#
// There are two types of request handlers
// Each returning the HttpWorkload in some form
// Internally, all the handlers are converted into TaskHttpHandler
type HttpWorkload =
    | EndResponse            // ends the response processing
    | TextResponse of string // the resonse body as string
    | FileResponse of string // filename of the file to be returned
    | JsonResponse of obj    // an obj to be serialised and returned as json

// Internal handlers
type HttpHandler = HttpContext -> HttpWorkload
type TaskHttpHandler = HttpContext -> Task<HttpWorkload>

// User provided handlers, that are converted to the internal representation
type ServicedHandler = RequestServices -> HttpWorkload
type TaskServicedHandler = RequestServices -> Task<HttpWorkload>

// Use EndResponse when you plan to construct the response manually
// And do not want any further processing to be applied after that
// If no response is provided at all, a default empty bodied 200 response is returned
// If you return a workload that contradicts your manual changes to the response object
// Then normally a ServerException would be thrown

// In order to define a request handler,
// Pass a function to an indexed property named after the expected Http Method
// The provided indexer is a string that will be converted into ASP.Net Core RoutePattern
// MS Docs Reference on Route Templating Syntax
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0#route-template-reference 
app.Get "/" <- fun serv -> serv.EndResponse ()
app.Post "/" <- fun serv -> serv.EndResponse ()
app.Put "/" <- fun serv -> serv.EndResponse ()
app.Patch "/" <- fun serv -> serv.EndResponse ()
app.Delete "/" <- fun serv -> serv.EndResponse ()
app.Head "/" <- fun serv -> serv.EndResponse ()
app.Options "/" <- fun serv -> serv.EndResponse ()
app.Connect "/" <- fun serv -> serv.EndResponse ()
app.Trace "/" <- fun serv -> serv.EndResponse ()

// If you need to perform an ayncronous computation
app.GetTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PostTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PutTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PatchTask "/" <- fun serv -> task { return serv.EndResponse () }
app.DeleteTask "/" <- fun serv -> task { return serv.EndResponse () }
app.HeadTask "/" <- fun serv -> task { return serv.EndResponse () }
app.OptionsTask "/" <- fun serv -> task { return serv.EndResponse () }
app.ConnectTask "/" <- fun serv -> task { return serv.EndResponse () }
app.TraceTask "/" <- fun serv -> task { return serv.EndResponse () }

// You do not need to use EndResponse convinience method of the RequestService parameter
// Your handler can return an HttpWorkload directly
open WebFrame.Http

app.Get "/home" <- fun _ -> TextResponse "Hello World"
app.PostTask "/home" <- fun _ -> task { return TextResponse "Hello World" }

// Moreover, you do not have to use convinience indexed properties named after their methods
// You can provide a method directly
// You must provide a TaskServicedHandler in this case
open WebFrame.RouteTypes

app.[ Post "/api" ] <- fun serv -> task { return serv.EndResponse () }

// Or even like this
open WebFrame.Http

app.[ Get "/" ] <- fun _ -> task { return EndResponse }

// Each method named indexed property will internally use this property
// Therefore, if the method and the endpoint combination repeats,
// It will immediately raise a ServerException
// However, in some corner cases the duplication may slip through the checks
// In that case the ASP.Net Core will raise its own exception
// and there is no guarantee on what exception and when it will be raised
// It depends on the whims of ASP.Net Core developers
```
If an `InputException` is raised during the request handling then a `400` response with the exception message would returned. In case of `ServerException`, a `500` response is issued instead.
### Request Services
`RequestServices` object is passed to all user defined `ServicedHandler` and it is the secret sauce of this framework. It contains intuitively named properties to access different parts of the request and the response. Furthermore, it encapsulates configuration, routes and services (DI). 

It also provides different helpers for many common scenarios. You can even access the raw `HttpRequest` and its properties through it if necessary.

The main rule of thumb that Request Services are trying to follow is:
* When you read a property, you are reading from a request object
* When you write to a property, then you are writing to a response object
* The same applies to the methods (where appropriate)
  * Different `Get`s would normally extract a value from the request
  * Different `Set`s would normally set a value in the response
  * Etc.

List of available 'services':
```F#
// serv: RequestServices
// Defined in: Services

// Raw ASP.NET Core HttpContext
serv.Context

// Request path properties
// Check path Parts for details
serv.Path

// Request route parameters services
// Check Route Parts for details
serv.Route

// Request query parameters services
// Check Query Parts for details
serv.Query

// Request and response header services
// Check Header Parts for details
serv.Headers

// Request and response cookie services
// Check Cookies Parts for details
serv.Cookies

// Server configuration services
// Check Config Parts for details
serv.Config

// Request body services
// Check Body Parts for details
serv.Body

// Server route services
serv.AppRoutes

// Set response status code
serv.StatusCode <- 201

// Get request Content-Type
serv.ContentType
// Set response Content-Type
// Overrides all the existing content-type data on the response
serv.ContentType <- "text/plain"

// Get ASP.NET Core service
// Throws MissingRequiredDependencyException on failure
serv.Services.Required<IRandomService> ()
// Returns optional instead of raising exceptions
serv.Services.Optional<IRandomService> ()
// If it fails to find registered service, then
// it returns the result of the default providing function
serv.Services.Get<IRandomService> RandomService

// Get the ASP.NET Core endpoint associated with this request
serv.GetEndpoint

// Get the description of the currently taken route
serv.RouteDescription

// Possible properties
serv.RouteDescription.Name
serv.RouteDescription.Pattern
serv.RouteDescription.Description

// Enable request buffering in ASP.NET Core
serv.EnableBuffering ()

// Get ILogger for a category called "MyLogCategory"
serv.LoggerFor "MyLogCategory"

// Get simple Logger for the current request
// Check Request Logging for further details
serv.Log

// HttpWorkload helpers
// Must be the final result returned by the handler

// Redirect helper
// 302 to '/'
serv.Redirect "/"
// 302 to '/'
serv.Redirect ( "/", false )
// 301 to '/'
serv.Redirect ( "/", true )

// FileResponse
// Return a file named "myfile.txt"
serv.File "myfile.txt"
// Return a file "image.png" and set the content-type manually to "image/png"
serv.File ( "image.png", "image/png" )

// Return a text reponse
serv.EndResponse "hello world"

// Return Json response
// Pass any non-string value and it will try send it as json
// If the object cannot be parsed as a valid json it will attempt serialising it as string instead
serv.EndResponse {| Value = "hello world" |}

// End a response
// Used to send an empty bodied response
// or when manually constructing the response
serv.EndResponse ()
```
#### Path Parts
Path parts are read only properties describing the path of currently processed web request.
```F#
serv.Path.Method // string property
serv.Path.Protocol // string property
serv.Path.Scheme // string property
serv.Path.Host // string property
serv.Path.Port // int option property
// Path Base is a part of ASP.NET Core
// Most of the time it is an empty string
// It can be useful in some hosting scenarios
// but requires an additional middleware to be activated
// Please refer to this stackoverflow question:
// https://stackoverflow.com/questions/58614864/whats-the-difference-between-httprequest-path-and-httprequest-pathbase-in-asp-n
serv.Path.PathBase // string property
serv.Path.Path // string property
serv.Path.QueryString // string property
serv.Path.IsHttps // bool property
```
#### Route Parts
Route parts services help with reading, parsing and validating the route template properties.
```F#
// The route template syntax is the ASP.NET Core default syntax
// Here is the link to the docs:
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-6.0#route-template-reference
app.Get "/resource/{id:guid}/{property?}" <- fun serv ->
    // The following line reads the "id" route property and tries to parse it into Guid
    // if it is absent or cannot be parsed,
    // then MissingRequiredRouteParameterException is raised
    let resId = serv.Route.Required<Guid> "id"
    // Will return a string optional with the value of "property" route segment
    let property = serv.Route.Optional<string> "property"
    // Alternatively, you can specify the default value for the optional segment inline
    let property = serv.Route.Get "property" "none"
    // If you just want a string option without any parsing, then use:
    let property = serv.Route.String "property"
    // NOTE, this does not override the default ASP.NET Core template matching and type checking
    
    // Accessing raw RouteValueDictionary
    let raw = serv.Route.Raw
    serv.EndResponse ()
```
#### Query Parts
Query parts services help with reading, parsing and validating the request query parameters.
```F#
app.Get "/resource/" <- fun serv ->
    // Trying to read query parameter "q" as a string
    // Can only be None if it is missing as there is no parsing involved
    let res = serv.Query.String "q"
    // This will try getting "q" query parameter and parse it into int
    // It will throw MissingRequiredQueryParameterException if it fails to read or parse
    let res = serv.Query.Required<int> "q"
    // If reading or parsing fails, then it will return None
    let res = serv.Query.Optional<int> "q"
    // Trying to read and parse the "q" query parameter with a default value
    let res = serv.Query.Get "q" 10
    
    // If you were expecting multiple query parameters with the same name
    // Use commands prefixed with All
    
    // Never fails. Returns a list of "q" parameters that managed to parse into the specified type
    let res = serv.Query.All<int> "q"
    // Returns all "q" parameters as strings
    // Returns None if none is found
    let res = serv.Query.AllString "q"
    // It will now attempt to parse the string values into the specified type
    // If any fails - it will throw MissingRequiredQueryParameterException
    let res = serv.Query.AllRequired<int> "q"
    // It will now attempt parsing the string values into the specified type
    // Returns None if any conversion fails
    let res = serv.Query.AllOptional<int> "q"
    
    // This will return the number of times "q" parameter was specified in the request
    // It is 0 if the query parameter is missing
    let count = serv.Query.Count "q"
    
    // Accessing the raw IQueryCollection
    let raw = serv.Query.Raw
    
    serv.EndResponse ()
```
#### Header Parts
Header parts services help with reading, parsing and setting request and response headers.
```F#
app.Get "/" <- fun serv ->
    // Next methods only return the first found value
    // Returns the request header named "custom"
    // Will throw MissingRequiredHeaderException if not found
    let h = serv.Headers.Required "custom"
    // Returns the request header named "custom"
    // None if not found
    let h = serv.Headers.Optional "custom"
    // Tries reading the request header named "custom"
    // If it is not found, then a default value is returned
    let h = serv.Headers.Get "custom" "myvalue"
    
    // Returns a list of comma separated values from the request header named "custom"
    // Returns an empty list if not found
    let h = serv.Headers.All "custom"
    
    // Replaces response header named "custom" with a comma separated list of values
    serv.Headers.Set "custom" [ "myvalue" ]
    // Appends an additional value to the response header named "custom"
    serv.Headers.Append "custom" "myvalue"
    // Removes the response header named "custom"
    serv.Headers.Delete "custom"
    
    // Counting how many comma seaparated segments request header "custom" has
    // If it is 0 if the header is missing
    let count = serv.Headers.Count "count"
    
    // Getting raw request IHeaderCollection
    let inHeaders = serv.Headers.Raw.In
    // Getting raw response IHeaderCollection
    let outHeaders = serv.Headers.Raw.Out
    
    serv.EndResponse ()
```
#### Cookie Parts
Cookie parts services help with reading, parsing and setting request and response cookies.
```F#
app.Get "/" <- fun serv ->
    // Trying to read "mycookie" cookie from the request
    // If not found, then MissingRequiredCookieException is thrown 
    let c = serv.Cookies.Required "mycookie"
    // Trying to read the request cookie
    // Returns None if not found
    let c = serv.Cookies.Optional "mycookie"
    // Trying to read the request cookie
    // And if not found, then the default value is returned
    let c = serv.Cookies.Get "mycookies" "myvalue"
    
    // Asking to set a cookie to the specified value
    serv.Cookies.Set "mycookie" "myvalue"
    
    // Cookie options is an ASP class
    // open Microsoft.AspNetCore.Http
    let emptyOpts = CookieOptions ()
    
    // Requesting a new cookie with additional options
    serv.Cookies.SetWithOptions "mycookies" "myvalue" emptyOpts
    
    // Requesting a removal of the cookie
    serv.Cookies.Delete "mycookies"
    // Requesting a cookie removal with additional options
    serv.Cookies.DeleteWithOptions "mycookie" emptyOpts
    
    // Getting a raw IRequestCookieCollection
    let rawIn = Serv.Cookies.Raw.In
    // Getting a raw IResponseCookies
    let rawOut = Serv.Cookies.Raw.Out
    
    serv.EndResponse ()
```
#### Config Parts
Config parts services help with reading, parsing and validating server settings.

`WebFrame` uses default ASP.NET Core configuration options.

In addition, it provides ways to quickly override the configs with inmemory values.
```F#
let app = App argv

// Overriding the config with custom values
app.Config.[ "Hello:Wolrd" ] <- "Value"
// Overriding connecition string for "MyConnection"
app.Config.ConnectionStrings "MyConnection" <- "host=localhost;"

app.Get "/" <- fun serv ->
    // NOTE: Missing configs are Server Exceptions
    // and therefore will result 5xx errors

    // Trying to read config with given keys
    // Returns None if not found
    let c = serv.Config.String "Hello:World"
    // It will throw MissingRequiredConfigException if the key is not found
    // or cannot be parsed
    let c = serv.Config.Required<string> "Hello:World"
    // Returns None if not found or cannot be parsed
    let c = serv.Config.Optional<string> "Hello:World"
    // Returns a default value if not found or cannot be parsed
    let c = serv.Config.Get "Hello:World" "N/A"
    
    // Helpers for dealing with environments
    
    // String properties (fixed for every request)
    let a = serv.Config.ApplicationName
    let e = serv.Config.EnvironmentName
    
    // Bool properties (fixed for every request)
    let r = serv.Config.IsDevelopment
    let r = serv.Config.IsStaging
    let r = serv.Config.IsProduction
    
    // Returns true if teh environment name matches the provided one
    let r = serv.Config.IsEnvironment "EnvironmentName"
    
    serv.EndResponse ()
```
#### Body Parts
The body parts services allow quick parsing and validation of different types of request bodies during the request handling.
```F#
// If you expect a form encoded body, use the following
serv.Body.Form

// If you expect a json encoded body, use this
serv.Body.Json

// In case you need to read a raw body as a stream
serv.Body.Raw

// or if you want to get a body pipe reader
serv.Body.RawPipe
```
##### Form
Services for dealing with form encoded bodies.
```F#
app.Post "/resource" <- fun serv ->
    // Checking if the Content Type header is set properly on the request
    let isPresent = serv.Body.Form.IsPresent

    // trying to read the "MyField" field from the request body
    // returns a string optional
    // only fails if the field cannot be found
    // returns the first field matching the name if multiple are present
    let f = serv.Body.Form.String "MyField"
    
    // It will take the first matching field and it will try to parse it as int
    // It raises MissingRequiredFormException if the request does not have a valid form encoded body
    // Or raises MissingRequiredJsonFieldException if the field cannot be found or parsed
    let f = serv.Body.Form.Required<int> "MyField"
    
    // The same as above but does not raise any exceptions on failure
    // If the form or field are missing, or if the field cannot be parsed,
    // then None is returned
    let f = serv.Body.Form.Optional<int> "MyField"
    
    // If the field cannot be read, then returns a default value
    let f = serv.Body.Form.Get "MyField" 42
    
    // Returns all the found "MyFIeld" fields and parses them into int
    // If any of them fails, then returns an empty list
    // If also returns an empty list if the form itself is not found
    let fields = serv.Body.Form.All<int> "MyField"
    
    // The same as above but leaves all the values as strings
    // It cannot fail due to parsing errors
    let fields = serv.Body.Form.AllString "MyField"
    
    // It will take all the matching fields and it will try to parse them as int
    // It raises MissingRequiredFormException if the request does not have a valid form encoded body
    // Or raises MissingRequiredJsonFieldException if the field cannot be found
    // It also raises MissingRequiredJsonFieldException if any of found fields cannot be parsed
    let fields = serv.Body.Form.AllRequired<int> "MyField"
    
    // The same as AllRequired but will return None on any failure
    let fields = serv.Body.Form.AllOptional<int> "MyField"
    
    // Returns the number of times the field has appeared in the form
    // Returns zero if the form is missing.
    let count = serv.Body.Form.Count "MyField"
    
    // Getting the raw IFormCollection from the request
    // If the form cannot be parsed, then returns None
    let form = serv.Body.Form.Raw
    
    // If the form has files, they can be accessed here
    // Returns None if the form is missing
    // Always present otherwise even if no files are available
    let files = serv.Body.Form.Files
    
    // TODO: make retuned files unwrapped by the next release
    // TODO: it should only fail when the Required method is called
    
    // Unwrapping option for simplicity
    // Will throw Null Exceptions if None!
    let files = files.Value
    
    // Getting the list of all provided files
    // returns IFormFile list
    let f = files.All ()
    
    // Getting the file descriptor from the form
    // Raises MissingRequiredFormFileException if the expected filename is not found
    let f = files.Required "MyFileName"
    
    // The same as above but returns None on failure
    let f = files.Optional "MyFileName"
    
    // Returns the number of files received in the form
    let fileCount = files.Count
    
    serv.EndResponse ()
```
##### Json
Services for dealing with JSON encoded bodies.
```F#
app.PostTask "/resource" <- fun serv -> task {
    // Reading a body and parsing as a specified type, even an anonymous one
    // If the body cannot does not match they type, ewhether missing or additional properties are found,
    // MissingRequiredJsonException is raised
    let! data = serv.Body.Json.Exact<{| Name: string; Age: int |}> ()
    
    // Alternatively, you can pass either a json path string
    // or a simplified Newtonsoft.Json query syntax
    // to query the body field by field
    
    // Here we are getting a string option
    // If the Json body is missing or the field is missing
    // it will return None
    
    let! data = serv.Body.Json.String "$.Name"
    let! data = serv.Body.Json.String "Name"
    
    // You can provide a default value for the field
    let! data = serv.Body.Json.Get "Name" "Default Value"
    
    // If the query cannot return the field
    // Then the MissingRequiredJsonFieldException is raised
    // If the Json body itself cannot be parsed,
    // then MissingRequiredJsonException is raised
    let! data = serv.Body.Json.Required<int> "Age"
    
    // The same as above but errors return None instead
    let! data = serv.Body.Json.Optional<int> "Age"
    
    // If you want to return multiple results qwith a single query
    // If any found field fails to parae,
    // it returns an empty list
    let! data = serv.Body.Json.All<int> "$.Points[:].X"
    // This was a finctional query to return property X from every object in the array Points as a list
    
    // The same as above but it cannot fail due to parsing errors
    // as it returns a list of strings
    // it returns an empty list in case of errors
    let! data = serv.Body.Json.AllString "$.Points[:].X"
    
    // If any field fails to parse, then
    // MissingRequiredJsonFieldException is raised
    // if the json body cannot be parsed, then
    // MissingRequiredJsonException is raised
    let! data = serv.Body.Json.AllRequired<int> "$.Points[:].X"
    
    // The same as above but it returns None if any error is encountered
    let! data = serv.Body.Json.AllOptional<int> "$.Points[:].X"
    
    // Returns the number of fields found to satisfy the query
    // It does not take any type parsing into an account
    // Also, if the json encoded body is missing,
    // then it returns 0
    let! count = serv.Body.Json.Count "$.Points[:].X"
    
    // If you need a raw JObject afor manual processing
    // then you should use this method
    let! j = serv.Body.Json.Raw "Points"
    
    // Confirms if the correct Content Type is received
    let isJson = serv.Body.Json.IsJsonContentType
    
    // Confirms if the Json body is a valid json by parsing it into the memory
    let! isJson = serv.Body.Json.IsPresent ()
    
    return serv.EndResponse ()
}
```
#### Request Logging
Services for dealing with logging during the request handling.
```F#
app.Get "/route" <- fun serv ->
    // Get ILogger for a category called "MyLogCategory"
    let logger = serv.LoggerFor "MyLogCategory"
    
    // You can use the default and simplified logger
    serv.Log.Information "Info"
    serv.Log.Warning "Warn"
    serv.Log.Error "Error"
    serv.Log.Critical "Crit"
    serv.Log.Debug "Debug"
    serv.Log.Trace "Trace"
    
    serv.EndResponse ()
```
### Host Logging
If your app requires an access to an inbuilt ASP Net Core service provider outside of the request handler (for example for your extension), then you can access the Service Provider from the app instance itself.
```F#
// Acquiring the DefaultServiceProvider wrapper
let serviceProvider = app.GetServiceProvider ()

// It is wrapped in DefaultServiceProvider class
// Raises MissingRequriedDependencyException on error
let s = serviceProvider.Required<IRandomService> ()
// Returns None on missing dependency
let s = serviceProvider.Optional<IRandomService> ()
// You can also provide a function returning a default implementation
// if the service is missing for any reason
let s = serviceProvider.Get<IRandomService> ServiceConstructor
```
### System Configuration
### Request Helpers
### Modules
### Testing
### Exceptions
## Changelog
