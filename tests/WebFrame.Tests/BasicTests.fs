module WebFrame.Tests.BasicTests

open System
open System.Globalization
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

open Helpers

type CoffeeException () = inherit Exception "I am a teapot!"
type NotEnoughCoffeeException () = inherit Exception "We need more coffee!"
type TooMuchCoffeeException () = inherit Exception "We've had enough coffee already!"

type MyRecord = {
    Id: Guid
    Name: string
    Position: int
}
type AnotherRecord = {
    Value: string
    Number: int
}
type NestedResponse<'T> = {
    Name: string
    Data: 'T
}
let app = App ()

let api = AppModule "/api"
api.Get "/log" <- fun serv ->
    serv.Log.Information "Test Log"
    
    let description = serv.RouteDescription
    
    serv.Log.Information $"Name: {description.Name}"
    
    serv.EndResponse ()
    
let localization = AppModule "/loc"
localization.Get "/culture" <- fun serv ->
    let c = serv.Globalization.RequestCulture
    serv.EndResponse c.Name

app.Log.Information "Hello"

app.Services.Globalization.AllowedCultures <- [
    CultureInfo "en"
    CultureInfo "en-GB"
    CultureInfo "fr"
    CultureInfo "ru-RU"
]

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
app.Module "localization" <- localization

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
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/plain"
    content |> shouldEqual expectedResponse
    
    let a3 = getServiceProvider ()
    let h3 = a3.GetHashCode ()
    
    h3 |> shouldNotEqual h1
    h3 |> shouldNotEqual h2
}

[<Test>]
let ``Confirming that the Service Provider returned from App works`` () = task {
    use! _server = app.TestServer ()
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

[<TestCase( "<h1>Title</h1>\n<p>World</p>", IncludePlatform="MacOsX" )>]
[<TestCase( "<h1>Title</h1>\n<p>World</p>", IncludePlatform="Unix, Linux" )>]
[<TestCase( "<h1>Title</h1>\r\n<p>World</p>", IncludePlatform="Win" )>]
let ``Testing basic DotLiquid templating `` ( expectedContent:string ) = task {
    use _ = new EnvVar ( "ASPNETCORE_ENVIRONMENT", "Development" )
    let app = App ()
    
    app.Services.ContentRoot <- __SOURCE_DIRECTORY__
    app.Get "/" <- fun serv ->
        serv.Page "Index.liquid" {| Hello = "World" |}
        
    app.Get "/about" <- page "About.html"
    app.Get "/txt" <- file "Sample.txt"
    app.Get "/txt1" <- file ( "Sample.txt", "text/html" )
    app.Get "/txt2" <- file "About.html"
    
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/html"
    content |> shouldEqual expectedContent
    
    let! r = client.GetAsync "/"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/html"
    content |> shouldEqual expectedContent
    
    let! r = client.GetAsync "/txt"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/plain"
    content |> shouldEqual """sample file"""
    
    let! r = client.GetAsync "/txt1"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/html"
    content |> shouldEqual """sample file"""
    
    let! r = client.GetAsync "/txt2"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/html"
    content |> shouldEqual """<h1>About</h1>"""
    
    let! r = client.GetAsync "/about"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.ToString ()
    ct |> shouldEqual "text/html"
    content |> shouldEqual """<h1>About</h1>"""
}

[<Test>]
let ``Checking the nested object response functionality`` () = task {
    // TODO: This will not work if the record constructor is private, therefore, it may need a different solution.
    let sampleInnerData = [ { Value = "sup"; Number = 3 }; { Value = "another"; Number = 14 } ]
    let expectedResult = """{"Name":"Test","Data":[{"Value":"sup","Number":3},{"Value":"another","Number":14}]}"""
    
    let app = App ()
    app.Get "/" <- fun serv ->
        let r = {
            Name = "Test"
            Data = sampleInnerData
        }
        serv.EndResponse r
        
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    let ct = r.Content.Headers.ContentType.MediaType
    ct |> shouldEqual "application/json"
    content |> shouldEqual expectedResult
}

[<Test>]
let ``Verifying Accept-Language handling`` () = task {
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/loc/culture"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual CultureInfo.CurrentCulture.Name
    
    use req = new HttpRequestMessage ( HttpMethod.Get, "/loc/culture" )
    req.Headers.AcceptLanguage.ParseAdd "ru-RU"
    let! r = client.SendAsync req
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual "ru-RU"
    
    use req = new HttpRequestMessage ( HttpMethod.Get, "/loc/culture" )
    req.Headers.AcceptLanguage.ParseAdd "en, ru-RU; q=0.9, en-GB; q=0.8, fr"
    let! r = client.SendAsync req
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual "en"
    
    use req = new HttpRequestMessage ( HttpMethod.Get, "/loc/culture" )
    req.Headers.AcceptLanguage.ParseAdd "ru-RU; q=0.9, en-GB; q=0.8, fr, en"
    let! r = client.SendAsync req
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual "fr"
    
    use req = new HttpRequestMessage ( HttpMethod.Get, "/loc/culture" )
    req.Headers.AcceptLanguage.ParseAdd "aa-bb"
    let! r = client.SendAsync req
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> shouldEqual HttpStatusCode.OK
    content |> shouldEqual CultureInfo.CurrentCulture.Name
}

[<Test>]
let ``Checking for custom error handlers`` () = task {
    let app = App ()
    app.Get "/coffee" <- fun serv ->
        raise ( CoffeeException () )
        serv.EndResponse ()
        
    app.Get "/work" <- fun serv ->
        raise ( NotEnoughCoffeeException () )
        serv.EndResponse ()
        
    app.Get "/double-shot/coffee" <- fun serv ->
        raise ( TooMuchCoffeeException () )
        serv.EndResponse ()

    app.Errors <- Error.codeFor<CoffeeException> 418
    app.Errors <- Error.on <| fun ( e: NotEnoughCoffeeException ) serv ->
        serv.StatusCode <- 400
        serv.EndResponse $"{e.Message}"
    app.Errors <- Error.onTask <| fun ( e: TooMuchCoffeeException ) serv -> task {
        serv.StatusCode <- 500
        return serv.EndResponse $"{e.Message}"
    }
    
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/coffee"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 418
    content |> shouldEqual "CoffeeException: I am a teapot!"

    let! r = client.GetAsync "/work"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> int |> shouldEqual 400
    content |> shouldEqual "We need more coffee!"
    
    let! r = client.GetAsync "/double-shot/coffee"
    let! content = r.Content.ReadAsStringAsync ()
    
    r.StatusCode |> int |> shouldEqual 500
    content |> shouldEqual "We've had enough coffee already!"
}

[<Test>]
let ``Checking exception filter`` () = task {
    use _ = new EnvVar ( "ASPNETCORE_ENVIRONMENT", "Development" )
    let app = App ()
    app.Services.Exceptions.UserExceptionFilter <-
        app.Services.Exceptions.UserExceptionFilter
        |> Map.add "Staging" false
    app.Services.Exceptions.InputExceptionFilter <-
        app.Services.Exceptions.InputExceptionFilter
        |> Map.add "Staging" false
    app.Services.Exceptions.ServerExceptionFilter <-
        app.Services.Exceptions.ServerExceptionFilter
        |> Map.add "Staging" true
    
    app.Get "/coffee" <- fun serv ->
        raise ( CoffeeException () )
        serv.EndResponse ()
        
    app.Get "/input" <- fun serv ->
        raise ( InputException "My Input Error" )
        serv.EndResponse ()
        
    app.Get "/server" <- fun serv ->
        raise ( ServerException "My Server Error" )
        serv.EndResponse ()
        
    app.Errors <- Error.codeFor<CoffeeException> 418
    
    app.Build () |> ignore
    
    // Development Env Tests
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/coffee"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 418
    content |> shouldEqual "CoffeeException: I am a teapot!"
    
    let! r = client.GetAsync "/input"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 400
    content |> shouldEqual "InputException: My Input Error"
    
    let! r = client.GetAsync "/server"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 500
    content |> shouldEqual "ServerException: My Server Error"
    
    // Staging Env Tests
    use _ = new EnvVar ( "ASPNETCORE_ENVIRONMENT", "Staging" )
    app.Build () |> ignore
    
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/coffee"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 418
    content |> shouldEqual "Workflow Error"
    
    let! r = client.GetAsync "/input"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 400
    content |> shouldEqual "Input Validation Error"
    
    let! r = client.GetAsync "/server"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 500
    content |> shouldEqual "ServerException: My Server Error"
    
    // Production Env Tests
    use _ = new EnvVar ( "ASPNETCORE_ENVIRONMENT", "Production" )
    app.Build () |> ignore
    
    use! server = app.TestServer ()
    use client = server.GetTestClient ()
    
    let! r = client.GetAsync "/coffee"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 418
    content |> shouldEqual "CoffeeException: I am a teapot!"
    
    let! r = client.GetAsync "/input"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 400
    content |> shouldEqual "InputException: My Input Error"
    
    let! r = client.GetAsync "/server"
    let! content = r.Content.ReadAsStringAsync ()    

    r.StatusCode |> int |> shouldEqual 500
    content |> shouldEqual "Server Error"
}
