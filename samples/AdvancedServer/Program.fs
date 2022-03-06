namespace AdvancedServer

open System
open System.IO

open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open WebFrame
open WebFrame.Http
open WebFrame.RouteTypes
open WebFrame.SystemConfig

open type WebFrame.Endpoints.Helpers

type SampleRequestBody = {
    Value: decimal
    Name: string
}

// Sample Exception
type CoffeeException () =
    inherit Exception "I am a teapot!"
    
// Sample service to inject
type IMyService =
    abstract member Print : string -> unit
    
type MyService () =
    interface IMyService with
        member _.Print text = printfn $"Text: {text}"
    
module MyService =
    let configureService: ServiceSetup = fun env config serv ->
        serv.AddScoped<IMyService, MyService> ()
        
module MyApp =
    let configureApp: AppSetup = fun env config app ->
        if env.IsDevelopment () then
            app
        else
            app.UseExceptionHandler "/error"


// These examples will try showing as many available helpers as possible
// However, please refer to the docs for more information
module Program =
    // The first item is always the full path to the executable
    let args = Environment.GetCommandLineArgs () |> Array.tail
            
    let app = App args
    
    // This is a host logger and it is configured in the system defaults
    // It is created even before the app is built and running
    // If the app raises an exception during the setup,
    // then it may terminate before the message is displayed in the console
    app.Log.Information "The app has been created"

    // Setting up in-memory overrides for config values
    app.Config.[ "MY_OPTION" ] <- "Brave New World!"
    app.Config.[ "COUNT" ] <- "12"
    
    // Returning 418 whenever a "CoffeeException" is raised within this module ("App" only in this case)
    app.Errors <- Error.codeFor<CoffeeException> 418
    
    // Registering a custom service before registering any services provided by the WebFrame
    app.Services.BeforeServices <- MyService.configureService
    
    // Adding an exception redirection when not in development
    
    // This is the earliest slot available for the app configuration
    // It happens before anything else is configured
    app.Services.BeforeApp <- MyApp.configureApp

    // Web Page route
    app.Get "/" <- page "Test.html" // It should end .html or Server Side exception would be thrown
    // Text response helper
    app.Get "/hello" <- always ( TextResponse "world" )
    // Few different ways to send a file over
    app.Get "/file" <- file "Text.txt" // You can also add a required content-type
    app.Get "/file2" <- fun _ -> FileResponse "Text.txt"
    app.Get "/file3" <- fun serv -> serv.File "Text.txt" // You can also add a required content-type
    
    // Accessing a custom service
    app.Get "/service" <- fun serv ->
        let s = serv.Services.Required<IMyService> ()
        
        s.Print "Hello"
        
        serv.EndResponse ()
        
    app.Get "/fail" <- fun _ ->
        failwith "I have failed you."

    // Accessing Configuration
    app.Get "/my" <- fun serv ->
        // Optional with default
        let myOption = serv.Config.Get "MY_OPTION" ""
        // Required property that can be parsed into int
        let count = serv.Config.Required<int> "COUNT"
        
        // Returns a string
        serv.EndResponse $"[{count}] {myOption}"

    // Always return an empty page with a 404 status code
    app.Get "/error" <- fun serv ->
        serv.StatusCode <- 404
        // Let's log the error too
        serv.Log.Warning "A page was not found"
        EndResponse
        
    // EndResponse method returns the HttpWorkload type
    // So you do not have to import the type in this case
    app.Get "/coffee" <- fun serv ->
        CoffeeException () |> raise
        serv.EndResponse ()
        
    // Requesting ASP.NET Core services
    // and returning objects (json by default)
    app.Get "/env" <- fun serv ->
        let env = serv.Services.Required<IWebHostEnvironment> ()
        
        serv.EndResponse env

    // Showing route and query parameters
    // For the route pattern syntax please check ASP.NET Core docs
    app.Post "/new/{item:guid}/{groupId:int?}/{**slug}" <- fun serv ->
        // Route Values are always singular
        let slug = serv.Route.Get "slug" ""
        let item = serv.Route.Required<Guid> "item"
        let groupId = serv.Route.Optional<int> "groupId"
        
        // Query Values are normally returned as a list
        // All values found must match the specified type
        
        // Only Get method tries to return the first item
        // It returns the first item found in the list
        let order = serv.Query.Get "order" "desc"
        // Required will return all the items found in a list
        // But they all have to match the specified type
        // Otherwise it fails
        let q = serv.Query.Required<bool> "q"
        let nextPosition = serv.Query.Optional<int> "next"
        // Will try find all the query parameters names "custom"
        // and returns them as a list of strings
        // Returns an empty list if nothing is found
        let allCustomQ = serv.Query.All<string> "custom"
        
        // A json response with an anonymous record
        serv.EndResponse
            {|
                Item = item
                GroupId = groupId
                Slug = slug
                Ordering = order
                Q = q
                Next = nextPosition
                Custom = allCustomQ
            |}
            
    // Accessing headers and cookies
    app.Get "/name" <- fun serv ->
        // Headers and Cookies follow the same principal as the Route and Query Parameters
        // You can request Required, Optional and so on
        // Headers are Cookies are string values
        // In addition, Headers are represented as a list
        
        // Requesting Optional Header from the request with a default value
        let specialHeader = serv.Headers.Get "Custom" "None"
        // Reading Optional Cookie with a default value
        let randomCookie = serv.Cookies.Get "Random" "0"
        
        // Setting up a custom header on the response
        // Header methods that write to the response: Set, Append, Delete
        serv.Headers.Set "Custom" [ "Random" ]
        
        let rnd = Random ()
        let randomNumber = rnd.Next ( 1, 10 )
        
        if randomNumber < 8 then
            // Setting up a custom Cookie on the response
            serv.Cookies.Set "Random" $"{randomNumber}"
        else
            // Asking for the cookie to be marked as expired
            serv.Cookies.Delete "Random"
            
        // In addition, you can pass ASP.NET Core CookieOptions class to customise them even further
        // Please use SetWithOptions and DeleteWithOptions methods for that purpose
        
        serv.EndResponse $"Custom: {specialHeader} [{randomCookie}]"
            
    // Another example of a json response
    app.Put "/new/{name}" <- fun serv ->
        // Another way to declare expected type
        let name: string = serv.Route.Required "name"
        
        // Presence of Required Form Fields would imply that
        // the form is sent correctly
        let number: int list = serv.Body.Form.Required "number"
        // The Form works just like the queries
        let zipCode = serv.Body.Form.Get "zip" ""
        
        // You can check explicitly if the form is present
        let present = serv.Body.Form.IsPresent
        
        let number = number |> List.tryHead |> Option.defaultValue 0
        
        let response =
            {|
                Name = name
                Number = $"+44{number}"
                ZipCode = zipCode
                FormWasPresent = present
            |}
            
        JsonResponse response
        
    // Async Workflows
            
    // Sometimes the request handler needs to work with Tasks
    // Then you can use a separate set of helper methods
    app.PostTask "/new" <- fun serv -> task {
        // Asynchronously (Task) read the body
        // It must be present and of the specified type
        // You can use Optional instead if you want to do something special
        // when the body is not an anticipated json  
        let! body = serv.Body.Json.Exact<SampleRequestBody> ()
        
        // A shortcut for accessing Content-Type header
        let contentType = serv.ContentType
        
        return JsonResponse {| Body = body; ContentType = contentType |}
    }

    // Another example of reading json
    app.PostTask "/new-item" <- fun serv -> task {
        // The type can be inlined and anonymous
        // Furthermore, it will discard additional fields if present
        let! body = serv.Body.Json.Exact<{| Name: string |}> ()
            
        return JsonResponse {| Body = body |}
    }

    // Reading a raw body as a stream
    app.PostTask "/new-item2" <- fun serv -> task {
        let bodyStream = serv.Body.Raw
        
        use reader = new StreamReader ( bodyStream )
        
        let! body = reader.ReadToEndAsync ()
        
        return serv.EndResponse {| Body = body |}
    }

    // Alternative method of setting up routes
    // Accepts Tasks only
    app.[ Delete "/" ] <- fun serv -> task {
        // You can access the entire asp.net core context
        // For example, here is the connection ip address
        let ip = serv.Context.Connection.RemoteIpAddress
        
        printfn $"IP: {ip}"
        
        // Terminates the response
        // Useful when you are manually preparing a response 
        return EndResponse
    }

    app.Run ()
