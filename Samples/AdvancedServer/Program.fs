namespace AdvancedServer

open WebFrame

open type WebFrame.Endpoints.Helpers

module Program =

    [<EntryPoint>]
    let main args =
        let app = App args
        
        app.Get "/" <- page "Test.html"
        
        app.Run ()
        0 // Exit code
