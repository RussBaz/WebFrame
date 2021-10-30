namespace WebFrame

open System.Collections.Generic

open Microsoft.Extensions.Hosting

open WebFrame.Exceptions
open WebFrame.Http
open WebFrame.RouteTypes
open WebFrame.Services
open WebFrame.SystemConfig

    
type AppModule ( prefix: string ) =
    let routes = Routes ()
    let modules = Dictionary<string, AppModule> ()
    
    let addRoute ( route: RoutePattern ) ( handler: TaskServicedHandler ) =
        if routes.ContainsKey route then
            raise ( DuplicateRouteException ( route.ToString () ) )
            
        let h: TaskHttpHandler = fun ctx -> ctx |> RequestServices |> handler
            
        routes.[ route ] <- RouteDef.createWithHandler route h
        
    let addModule name m =
        if modules.ContainsKey name then
            raise ( DuplicateModuleException name )
            
        modules.[ name ] <- m
        
    let asTask ( h: ServicedHandler ) : TaskServicedHandler =
        fun ( s: RequestServices ) -> task {
            return h s
        }
        
    let prefixRoute ( r: RouteDef ) = r |> RouteDef.prefixWith prefix
    
    let updateModuleRoute ( moduleName: string ) ( r: KeyValuePair<RoutePattern, RouteDef> ) =
        r.Value |> RouteDef.name $"{moduleName}:{r.Value.Name}"
        
    let collectModuleRoutes ( i: KeyValuePair<string, AppModule> ) =
        i.Value.CollectRoutes ()
        |> Seq.map ( updateModuleRoute i.Key )
        
    let getLocalRoutes () = routes |> Seq.map ( fun i -> i.Value )
    let getModuleRoutes () = modules |> Seq.collect collectModuleRoutes
    let prefixRoutes ( r: RouteDef seq ) = r |> Seq.map prefixRoute
    
    member _.Item
        with set ( index: RoutePattern ) ( value: TaskServicedHandler ) = value |> addRoute index
        and get ( index: RoutePattern ) = routes.[ index ]
        
    member this.Connect
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Connect index ] <- value |> asTask
    member this.Delete
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Delete index ] <- value |> asTask
    member this.Get
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Get index ] <- value |> asTask
    member this.Head
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Head index ] <- value |> asTask
    member this.Options
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Options index ] <- value |> asTask
    member this.Patch
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Patch index ] <- value |> asTask
    member this.Post
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Post index ] <- value |> asTask
    member this.Put
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Put index ] <- value |> asTask
    member this.Trace
        with set ( index: string ) ( value: ServicedHandler ) = this.[ Trace index ] <- value |> asTask
        
    member this.ConnectTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Connect index ] <- value
    member this.DeleteTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Delete index ] <- value
    member this.GetTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Get index ] <- value
    member this.HeadTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Head index ] <- value
    member this.OptionsTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Options index ] <- value
    member this.PatchTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Patch index ] <- value
    member this.PostTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Post index ] <- value
    member this.PutTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Put index ] <- value
    member this.TraceTask
        with set ( index: string ) ( value: TaskServicedHandler ) = this.[ Trace index ] <- value
        
    member _.Module
        with set ( name: string ) ( value: AppModule ) = value |> addModule name
        and get ( name: string ) = modules.[ name ]
        
    member _.CollectRoutes () : Routes =
        let result = Routes ()
        
        let addRoute ( route: RouteDef ) =
            if result.ContainsKey route.Pattern then
                raise ( DuplicateRouteException ( route.Pattern.ToString () ) )
                
            result.[ route.Pattern ] <- route
        
        getLocalRoutes ()
        |> Seq.append ( getModuleRoutes () )
        |> prefixRoutes
        |> Seq.iter addRoute
        
        result

type App ( ?args: string [] ) =
    inherit AppModule ""
    let args = defaultArg args [||]
    let mutable host = None
    
    member val Services = DynamicConfig ()
    member val Config = Dictionary<string, string> ()
    
    member private this.GetHostBuilder ( ?testServer: bool ) =
        let testServer = defaultArg testServer false
        
        this.Services.Routes <- this.CollectRoutes ()
        this.Services.Config <- this.Config
        
        if testServer then
            this.Services.CreateTestBuilder args
        else
            this.Services.CreateHostBuilder args
    
    member this.Build () =
        let builder = this.GetHostBuilder ()
        let h = builder.Build ()
        host <- Some h
        h
        
    member this.Run () =
        let host = host |> Option.defaultValue ( this.Build () )
        host.Run ()
        
    member this.Run ( urls: string list ) =
        this.Config.[ "urls" ] <- urls |> String.concat ";"
        
        let host = this.Build ()
        
        host.Run ()
        
    member this.TestServer () =
        let host = this.GetHostBuilder true
        host.StartAsync ()
