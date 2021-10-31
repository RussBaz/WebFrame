namespace AdvancedServer

open System
open System.IO

open Microsoft.AspNetCore.Hosting

open WebFrame
open WebFrame.Http
open WebFrame.RouteTypes

open type WebFrame.Endpoints.Helpers

type SampleRequestBody = {
    Value: decimal
    Name: string
}

module Program =

    [<EntryPoint>]
    let main args =
        // This example will try showing the most of available helpers
        
        let app = App args
        
        // Setting up in-memory overrides for config values
        app.Config.[ "MY_OPTION" ] <- "Brave New World!"
        app.Config.[ "COUNT" ] <- "12"
        
        // Web Page route
        app.Get "/" <- page "Test.html" // It should end .html or Server Side exception would be thrown
        // Text response helper
        app.Get "/hello" <- always ( TextResponse "world" )
        // Few different ways to send a file over
        app.Get "/file" <- file "Text.txt" // You can also add a required content-type
        app.Get "/file2" <- fun _ -> FileResponse "Text.txt"
        app.Get "/file3" <- fun serv -> serv.File "Text.txt" // You can also add a required content-type
        
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
            EndResponse
            
        // EndResponse method returns the HttpWorkload type
        // So you do not have to import the type in this case
        app.Get "/coffee" <- fun serv ->
            serv.StatusCode <- 418
            serv.EndResponse ()
            
        // Requesting ASP.NET Core services
        // and returning objects (json by default)
        app.Get "/env" <- fun serv ->
            let env = serv.GetService<IWebHostEnvironment> ()
            
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
        0 // Exit code
