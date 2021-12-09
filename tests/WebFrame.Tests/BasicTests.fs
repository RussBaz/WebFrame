module WebFrame.Tests.BasicTests

open System
open System.Net
open System.Net.Http

open Microsoft.Extensions.Configuration
open NUnit.Framework
open FsUnitTyped

open Microsoft.AspNetCore.TestHost

open WebFrame
open WebFrame.Exceptions
open WebFrame.Http
open WebFrame.RouteTypes
open type Endpoints.Helpers

type MyRecord = {
    Id: Guid
    Name: string
    Position: int
}
let app = App ()

let api = AppModule "/api"
api.Get "/log" <- fun serv ->
    serv.Log.Information "Test Log"
    
    let description = serv.RouteDescription
    
    serv.Log.Information $"Name: {description.Name}"
    
    serv.EndResponse ()

app.Log.Information "Hello"

app.Config.[ "hello" ] <- "World"

app.[ Get "/" ] <- fun _ -> task { return TextResponse "Hello World!" }
app.[ Get "/data" ] <- alwaysTask ( TextResponse "Data" )
app.[ Post "/data" ] <- fun serv -> task {
    let q = serv.Query.Get "q" ""
    return JsonResponse
        {|
            Q = q
        |}
}

app.PostTask "/guid" <- fun serv -> task {
    let! testField = serv.Body.Json.Optional<Guid option> "id"
    let field = testField |> Option.flatten
    
    return serv.EndResponse {| Result = field |}
}

app.Get "/log/{groupId:int?}" <- fun serv ->
    serv.Log.Information "Test Log"
    
    let description = serv.RouteDescription
    
    serv.Log.Information $"Name: {description.Name}"
    
    serv.EndResponse ()
    
app.Module "api" <- api

[<SetUp>]
let Setup () =
    ()

[<Test>]
let ``Verifies that the test server is alive`` () = task {
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual "Hello World!"
    
    let! r = client.GetAsync "/unknown"
    
    r.StatusCode |> shouldEqual HttpStatusCode.NotFound
    
    let! r = client.GetAsync "/data"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual "Data"
    
    let data = Map [
        "a", "f"
    ]
    use requestContent = new FormUrlEncodedContent ( data )
    let! r = client.PostAsync ( "/data?q=123", requestContent )
    let! content = r.Content.ReadAsStringAsync ()
    
    content |> shouldEqual """{"Q":"123"}"""
    
    use requestContent = new FormUrlEncodedContent ( data )
    let! r = client.PostAsync ( "/data?n=123", requestContent )
    let! content = r.Content.ReadAsStringAsync ()
    
    content |> shouldEqual """{"Q":""}"""
}

[<Test>]
let ``Guid option json test`` () = task {
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    let data = """
{
    "id": {
        "Case": "Some",
        "Fields": [
            "00000000-0000-0000-0000-000000000001"
        ]
    }
}
"""
    let expected = """{"Result":{"Case":"Some","Fields":["00000000-0000-0000-0000-000000000001"]}}"""
    use c = new StringContent ( data )
    c.Headers.ContentType <- Headers.MediaTypeHeaderValue.Parse "application/json"
    let! r = client.PostAsync ( "/guid", c )
    
    let! content = r.Content.ReadAsStringAsync ()
    
    content |> shouldEqual expected
    return ()
}

[<Test>]
let ``Logging works as expected`` () = task {
    // TODO: actually make this test work
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! _ = client.GetAsync "/log"
    let! _ = client.GetAsync "/api/log"
    
    return ()
}

[<Test>]
let ``Verifying the IServiceProvider getter method logic`` () = task {
    let expectedResponse = "Hello World!"
    
    // Creating a local app instance in order to avoid conflicts with other tests
    let app = App ()
    app.Get "/" <- always expectedResponse
    
    let getServiceProvider = app.GetServiceProvider
    
    fun _ -> getServiceProvider () |> ignore
    |> shouldFail<HostNotReadyException>
    
    app.Build () |> ignore
    let a1 = getServiceProvider ()
    let h1 = a1.GetHashCode ()
    
    app.Build () |> ignore
    let a2 = getServiceProvider ()
    let h2 = a2.GetHashCode ()
    
    h1 |> shouldNotEqual h2
    
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual expectedResponse
    
    let a3 = getServiceProvider ()
    let h3 = a3.GetHashCode ()
    
    h3 |> shouldNotEqual h1
    h3 |> shouldNotEqual h2
}

[<Test>]
let ``Confirming that the Service Provider returned from App works`` () = task {
    use! server = app.TestServer ()
    let serviceProvider = app.GetServiceProvider ()
    let confService = serviceProvider.Required<IConfiguration> ()
    
    confService.[ "Hello" ] |> shouldEqual "World"
}

[<Test>]
let ``Testing basic hooks`` () = task {
    let expectedResponse = "Hello World!"
    let app = App ()
    app.Get "/" <- always expectedResponse
    
    let mutable onStartHookRan = false
    let mutable onStopHookRan = false
    
    fun _ -> onStartHookRan <- true
    |> app.Hooks.AddOnStartHook
    
    fun _ -> onStopHookRan <- true
    |> app.Hooks.AddOnStopHook
    
    use! server = app.TestServer ()
    
    do! server.StopAsync ()
    
    onStartHookRan |> shouldEqual true
    onStartHookRan |> shouldEqual true
}
