namespace WebFrame

open System
open System.Collections.Generic

open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting

open WebFrame.Configuration
open WebFrame.Logging
open WebFrame.Exceptions
open WebFrame.Http
open WebFrame.RouteTypes
open WebFrame.Services
open WebFrame.SystemConfig

    
type AppModule ( prefix: string ) =
    let routes = Routes ()
    let modules = Dictionary<string, AppModule> ()
    let errorHandlers = List<TaskErrorHandler> ()
    
    let addRoute ( route: RoutePattern ) ( handler: TaskServicedHandler ) =
        if routes.ContainsKey route then
            raise ( DuplicateRouteException ( route.ToString () ) )
            
        let h: TaskHttpHandler = fun config ctx -> ( ctx, config ) |> RequestServices |> handler
        
        let routeDef = RouteDef.createWithHandler route h
            
        routes.[ route ] <- routeDef
        
    let addModule name ( m: #AppModule ) =
        if modules.ContainsKey name then
            raise ( DuplicateModuleException name )
            
        modules.[ name ] <- m
        
    let asTask ( h: ServicedHandler ) : TaskServicedHandler =
        fun ( s: RequestServices ) -> task {
            return h s
        }
        
    // Preprocess each route
    let preprocessRoute ( r: RouteDef ) =
        r |> RouteDef.prefixWith prefix |> RouteDef.onErrors ( List.ofSeq errorHandlers )
    
    let updateModuleRoute ( moduleName: string ) ( r: KeyValuePair<RoutePattern, RouteDef> ) =
        r.Value |> RouteDef.name $"{moduleName}.{r.Value.Name}"
        
    let collectModuleRoutes ( i: KeyValuePair<string, AppModule> ) =
        i.Value.CollectRoutes ()
        |> Seq.map ( updateModuleRoute i.Key )
        
    let getLocalRoutes () = routes |> Seq.map ( fun i -> i.Value )
    let getInnerRoutes () = modules |> Seq.collect collectModuleRoutes
    let preprocessRoutes ( r: RouteDef seq ) = r |> Seq.map preprocessRoute
    
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
        |> Seq.append ( getInnerRoutes () )
        |> preprocessRoutes
        |> Seq.iter addRoute
        
        result
    member this.Errors with set h = h |> errorHandlers.Add

type App ( defaultConfig: SystemDefaults ) =
    inherit AppModule ""
    let args = defaultConfig.Args
    let mutable host = None
    
    new () = App SystemDefaults.standard
    new ( args: string [] ) = App ( SystemDefaults.defaultWithArgs args )
    
    member val Services = SystemSetup defaultConfig
    member val Config = ConfigOverrides defaultConfig
    
    member private this.GetHostBuilder ( ?testServer: bool ) =
        let testServer = defaultArg testServer false
        
        this.Services.Routes <- this.CollectRoutes ()
        this.Services.Config <- this.Config
        
        if testServer then
            this.Services.CreateTestBuilder args
        else
            this.Services.CreateHostBuilder args
            
    member private this.BuildHost ( builder: IHostBuilder ) : IHost =
        let h = builder.Build ()
        host <- Some h
        h
            
    member private this.BuildTest () : IHost =
        this.GetHostBuilder true // Pass true to enable the test server
        |> this.BuildHost
            
    member this.GetServiceProvider () : IServiceProvider =
        let h: IHost =
            match host with
            | Some h -> h
            | None -> raise ( HostNotReadyException () )
            
        h.Services
    
    member this.Build () : IHost =
        this.GetHostBuilder ()
        |> this.BuildHost
        
    member this.Run () =
        let host = host |> Option.defaultValue ( this.Build () )
        host.Run ()
        
    member this.Run ( urls: string list ) =
        this.Config.[ "urls" ] <- urls |> String.concat ";"
        
        let host = this.Build ()
        host.Run ()
        
    member this.TestServer () =
        let host = this.BuildTest ()
        task {
            do! host.StartAsync ()
            return host
        }
    
    member val Log =
        let f = defaultConfig.LoggerHostFactory
        let name = defaultConfig |> SystemDefaults.getGlobalLoggerName
        Logger ( f, name )

module Error =
    let onTask<'T when 'T :> exn> ( e: ServicedTaskErrorHandler<'T> ) : TaskErrorHandler =
        fun ( configs: SystemDefaults ) ( ex: Exception ) ( c: HttpContext ) ->
            task {
                match ex with
                | :? 'T as ex ->
                    let! r = e ex ( RequestServices ( c, configs ) )
                    return Some r
                | _ ->
                    return None
            }
    let on<'T when 'T :> exn> ( e: ServicedErrorHandler<'T> ) : TaskErrorHandler =
        onTask ( fun ex serv -> task { return e ex serv } )
        
    let codeFor<'T when 'T :> exn> ( code: int ) : TaskErrorHandler =
        fun ( e: 'T ) ( serv: RequestServices ) -> task {
            let t = e.GetType ()
            serv.StatusCode <- code
            return serv.EndResponse $"{t.Name}: {e.Message}"
        }
        |> onTask
