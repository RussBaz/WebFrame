open WebFrame

let api = AppModule "/api"
api.Get "/" <- fun serv -> serv.EndResponse "Api"

let app = App ()
app.Get "/" <- fun serv -> serv.EndResponse "Main"
app.Module "api" <- api

app.Run ()
