open WebFrame
open type WebFrame.Endpoints.Helpers

[<EntryPoint>]
let main argv =
    let items = [ "todo1"; "todo2"; "todo3" ]
    
    let api = AppModule "/api"
    
    // Returning items
    api.Get "/" <- fun serv ->
        serv.EndResponse items
        
    // Adding items
    // By sending an item Name as a string field in a form
    api.Post "/" <- fun serv ->
        // If a required property in user input is not found,
        // then 400 error is issued automatically
        let itemName = serv.Body.Form.Required<string> "name"
        
        if items |> List.contains itemName then
            serv.StatusCode <- 409
            printfn $"Item {itemName} already exists"
        else
            serv.StatusCode <- 201    
            printfn $"Adding a new item {itemName}"
        
        serv.EndResponse ()
    
    let app = App argv
    
    app.Get "/" <- page "Pages/Index.html"
    app.Get "/About" <- page "Pages/About.html"
    
    app.Module "ToDoApi" <- api
    
    app.Run ()
    
    0 // exit code
