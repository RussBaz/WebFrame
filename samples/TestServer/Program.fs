open System.Threading.Tasks

open Microsoft.AspNetCore.TestHost

open WebFrame


let app = App ()

app.Get "/" <- fun serv -> serv.EndResponse "index"

let t = [
    task {
        use! server = app.TestServer ()
        let client = server.GetTestClient ()
        
        let! r = client.GetAsync "/"
        let! c = r.Content.ReadAsStringAsync ()
        
        if c <> "index" then failwith "wrong content"
        
        return ()
    } :> Task
]

t |> Array.ofList |> Task.WaitAll
