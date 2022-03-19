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
open WebFrame.ServicesParts
open WebFrame.Services
open WebFrame.SystemConfig

type Hooks<'T> ( app: 'T ) =
    let mutable onStartHooks: ( 'T -> unit ) list = []
    let mutable onStopHooks: ( 'T -> unit ) list = []
    
    member this.AddOnStartHook ( hook: 'T -> unit ) = onStartHooks <- hook :: onStartHooks
    member _.AddOnStopHook ( hook: 'T -> unit ) = onStopHooks <- hook :: onStopHooks
    
    member _.ClearOnStartHooks () = onStartHooks <- []
    member _.ClearOnStopHooks () = onStopHooks <- []
    
    member internal this.RunOnStartHooks () = onStartHooks |> List.iter ( fun i -> i app )
    member internal _.RunOnStopHooks () = onStopHooks |> List.iter ( fun i -> i app )
    
type AppModule ( prefix: string ) =
    let routes = Routes ()
    let modules = Dictionary<string, AppModule> ()
    let errorHandlers = List<TaskErrorHandler> ()
    
    let addRoute ( route: RoutePattern ) ( handler: TaskServicedHandler ) =
        if routes.ContainsKey route then
            raise ( DuplicateRouteException ( route.ToString () ) )
            
        let routeDef =
            handler
            |> TaskServicedHandler.toTaskHttpHandler
            |> RouteDef.createWithHandler route

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
        
    member this.Connect
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ CONNECT ]
            value
            |> asTask
            |> addRoute pattern
    member this.Delete
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ DELETE ]
            value
            |> asTask
            |> addRoute pattern
    member this.Get
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ GET ]
            value
            |> asTask
            |> addRoute pattern
    member this.Head
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ HEAD ]
            value
            |> asTask
            |> addRoute pattern
    member this.Options
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ OPTIONS ]
            value
            |> asTask
            |> addRoute pattern
    member this.Patch
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ PATCH ]
            value
            |> asTask
            |> addRoute pattern
    member this.Post
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ POST ]
            value
            |> asTask
            |> addRoute pattern
    member this.Put
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ PUT ]
            value
            |> asTask
            |> addRoute pattern
    member this.Trace
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index [ TRACE ]
            value
            |> asTask
            |> addRoute pattern
    member this.Any
        with set ( index: string ) ( value: ServicedHandler ) =
            let pattern = RoutePattern.create index HttpMethod.Any
            value
            |> asTask
            |> addRoute pattern
        
    member this.ConnectTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ CONNECT ] )
    member this.DeleteTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ DELETE ] )
    member this.GetTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ GET ] )
    member this.HeadTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ HEAD ] )
    member this.OptionsTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ OPTIONS ] )
    member this.PatchTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ PATCH ] )
    member this.PostTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ POST ] )
    member this.PutTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ PUT ] )
    member this.TraceTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index [ TRACE ] )
    member this.AnyTask
        with set ( index: string ) ( value: TaskServicedHandler ) = value |> addRoute ( RoutePattern.create index HttpMethod.Any )
        
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
    member this.Route with set ( r: RouteDef ) =
        if routes.ContainsKey r.Pattern then
            raise ( DuplicateRouteException ( r.Pattern.ToString () ) )
        
        routes.[ r.Pattern ] <- r

type App ( defaultConfig: SystemDefaults ) as app =
    inherit AppModule ""
    let args = defaultConfig.Args
    let mutable host = None
    
    new () = App SystemDefaults.standard
    new ( args: string [] ) = App ( SystemDefaults.defaultWithArgs args )
    
    member val Services = SystemSetup defaultConfig
    member val Config = ConfigOverrides defaultConfig
    member val Hooks = Hooks app
    member val Defaults = defaultConfig
    
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
            
    member this.GetServiceProvider () : GenericServiceProvider =
        let h: IHost =
            match host with
            | Some h -> h
            | None -> raise ( HostNotReadyException () )
            
        h.Services |> GenericServiceProvider
    
    member this.Build () : IHost =
        this.GetHostBuilder ()
        |> this.BuildHost
        
    member this.Run () =
        let host = host |> Option.defaultValue ( this.Build () )
        try
            this.Hooks.RunOnStartHooks ()
            host.Run ()
        finally
            this.Hooks.RunOnStopHooks ()
        
    member this.Run ( urls: string list ) =
        this.Config.[ "urls" ] <- urls |> String.concat ";"
        let host = this.Build ()
        try
            this.Hooks.RunOnStartHooks ()
            host.Run ()
        finally
            this.Hooks.RunOnStopHooks ()
        
    member this.TestServer () =
        let host = this.BuildTest ()
        task {
            try
                this.Hooks.RunOnStartHooks ()
                do! host.StartAsync ()
                return host
            finally
                this.Hooks.RunOnStopHooks ()
        }
    
    member val Log =
        let f = defaultConfig.LoggerHostFactory
        let name = defaultConfig |> SystemDefaults.getGlobalLoggerName
        Logger ( lazy f, name )

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
            let exFilter = serv.Services.Required<IUserExceptionFilter> ()
            let message =
                if exFilter.ShowUserException then
                    let t = e.GetType ()
                    $"{t.Name}: {e.Message}"
                else
                    "Workflow Error"
            serv.StatusCode <- code
            return serv.EndResponse message
        }
        |> onTask
