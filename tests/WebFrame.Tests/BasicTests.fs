module WebFrame.Tests.BasicTests

open System.Net

open System.Net.Http
open NUnit.Framework
open FsUnitTyped

open Microsoft.AspNetCore.TestHost

open WebFrame
open WebFrame.Http
open WebFrame.RouteTypes
open type WebFrame.Endpoints.Helpers

let app = App ()

app.[ Get "/" ] <- fun _ -> task { return TextResponse "Hello World!" }
app.[ Get "/data" ] <- alwaysTask ( TextResponse "Data" )
app.[ Post "/data" ] <- fun serv -> task {
    let q = serv.Query.Get "q" ""
    return JsonResponse
        {|
            Q = q
        |}
}

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
