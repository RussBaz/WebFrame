open WebFrame

let app = App ()

app.Get "/" <- fun serv -> serv.EndResponse "Hello World!"

app.Run ()
