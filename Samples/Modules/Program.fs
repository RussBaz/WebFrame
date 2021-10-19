open WebFrame

[<EntryPoint>]
let main _ =
    let api = AppModule "/api"
    
    api.Get "/" <- fun serv -> serv.EndResponse "Api"
    
    let app = App ()
    
    app.Get "/" <- fun serv -> serv.EndResponse "Main"
    
    app.Module "api" <- api
    
    app.Run ()
    0 // return an integer exit code