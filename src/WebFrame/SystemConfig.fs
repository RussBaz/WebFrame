module WebFrame.SystemConfig

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.TestHost

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Newtonsoft.Json

open WebFrame.Configuration
open WebFrame.Exceptions
open WebFrame.Http
open WebFrame.RouteTypes
open WebFrame.Templating

type ServiceSetup = IWebHostEnvironment -> IConfiguration -> IServiceCollection -> IServiceCollection
type AppSetup = IWebHostEnvironment -> IConfiguration -> IApplicationBuilder -> IApplicationBuilder
type EndpointSetup = IEndpointRouteBuilder -> IEndpointRouteBuilder

type TemplatingSetup ( defaultConfig: SystemDefaults ) =
    let defaultSetup ( defaultConfig: SystemDefaults ) ( root: string ) ( i: IServiceProvider ) =
        let loggerFactory = i.GetService<ILoggerFactory> ()
        let env = i.GetService<IWebHostEnvironment> ()
        
        DotLiquidTemplateService ( defaultConfig, root, loggerFactory, env )
        :> obj
    let mutable userSetup = None
    let getSetup ( defaultConfig: SystemDefaults ) ( root: string ) =
        let setup = userSetup |> Option.defaultValue defaultSetup
        setup defaultConfig root
    
    member val DefaultRenderer = defaultSetup
    member val TemplateRoot = "." with get, set
    member _.Custom with set ( s: SystemDefaults -> string -> IServiceProvider -> obj ) = userSetup <- Some s
    
    member internal this.ConfigureServices: ServiceSetup = fun env _ services ->
        let templateRoot = Path.Combine ( env.ContentRootPath, this.TemplateRoot ) |> Path.GetFullPath
        let setup = getSetup defaultConfig templateRoot
        let t = typeof<ITemplateRenderer>
        services.AddSingleton ( t, setup )
    
type SimpleRouteDescriptor ( routes: Routes ) =
    interface IRouteDescriptorService with
        member this.All () = routes |> Seq.map ( fun i -> i.Value ) |> List.ofSeq
        member this.TryGet v =
            match routes.TryGetValue v with
            | true, v -> Some v
            | _ -> None

type InMemoryConfigSetup ( defaultConfig: SystemDefaults ) =
    let configStorage = Dictionary<string, string> ()
    
    member _.Item
        with set ( key: string ) ( value: string ) = configStorage.[ key ] <- value
    member val WebFrameSettingsPrefix = defaultConfig.SettingsPrefix
    member internal this.SetupWith ( v: ConfigOverrides ) =
        for i in v.Raw do this.[ i.Key ] <- i.Value
        
    member internal _.Builder = fun ( _: HostBuilderContext ) ( config: IConfigurationBuilder ) ->        
        config.AddInMemoryCollection configStorage |> ignore

type StaticFilesSetup ( _defaultConfig: SystemDefaults ) =
    member val Options: StaticFileOptions option = None with get, set
    member val BrowsingOptions: DirectoryBrowserOptions option = None with get, set
    member val Route = "" with get, set
    member val WebRoot = "wwwroot" with get, set
    member val Enabled = false with get, set
    member val AllowBrowsing = false with get, set
    member internal this.ConfigureApp: AppSetup = fun _env _conf app ->
        let app =
            if this.Enabled then
                match this.Options with
                | Some o ->
                    if this.Route.Length > 0 then o.RequestPath <- this.Route |> PathString
                    app.UseStaticFiles o
                | None ->
                    if this.Route.Length > 0 then
                        app.UseStaticFiles this.Route
                    else
                        app.UseStaticFiles ()
            else
                app
               
        if this.AllowBrowsing then
            match this.BrowsingOptions with
            | Some o -> app.UseDirectoryBrowser o
            | None -> app.UseDirectoryBrowser ()
        else
            app
            
    member internal this.ConfigureServices: ServiceSetup = fun _ _ services ->
        if this.AllowBrowsing then
            services.AddDirectoryBrowser ()
        else
            services

type SystemSetup ( defaultConfig: SystemDefaults ) =
    let beforeServiceSetup = List<ServiceSetup> ()
    let afterServiceSetup = List<ServiceSetup> ()
    let beforeAppSetup = List<AppSetup> ()
    let beforeRoutingSetup = List<AppSetup> ()
    let beforeEndpointSetup = List<AppSetup> ()
    let afterEndpointSetup = List<AppSetup> ()
    let afterAppSetup = List<AppSetup> ()
    let endpointSetup = List<EndpointSetup> ()
    let staticFilesSetup = StaticFilesSetup defaultConfig
    let configSetup = InMemoryConfigSetup defaultConfig
    let templatingSetup = TemplatingSetup defaultConfig
    
    let mutable routes = Routes ()
    let mutable contentRoot = ""
    
    let configureRoute ( env: IWebHostEnvironment ) ( _conf: IConfiguration ) ( route: RouteDef ) ( endpoints: IEndpointRouteBuilder ) =
        let prepareDelegate ( eh: TaskErrorHandler list ) ( h: TaskHttpHandler ) =
            // Trying to find matching Error Handler
            let rec handleExceptions ex ( context: HttpContext ) ( h: TaskErrorHandler list ) = task {
                match h with
                | [] -> return None
                | head::tail ->
                    match! head defaultConfig ex context with
                    | Some r -> return Some r
                    | None -> return! handleExceptions ex context tail
            }
            
            // Calling a handler and trying to catch expected exceptions
            let callHandlerWith ( context: HttpContext ) = task {
                try
                    return! h defaultConfig context
                with
                | ex ->
                    let eh = eh |> List.rev
                    let! w = handleExceptions ex context eh
                    return w |> Option.defaultWith ( fun () -> raise ex )
            }
            
            let handle ( context: HttpContext ) =
                task {
                    try
                        let! workload = callHandlerWith context
                        return!
                            match workload with
                            | EndResponse -> context.Response.CompleteAsync ()
                            | TextResponse t ->
                                if String.IsNullOrEmpty context.Response.ContentType then
                                    context.Response.ContentType <- "text/plain"
                                context.Response.WriteAsync t
                            | HtmlResponse t ->
                                if String.IsNullOrEmpty context.Response.ContentType then
                                    context.Response.ContentType <- "text/html"
                                context.Response.WriteAsync t
                            | FileResponse f -> context.Response.SendFileAsync f
                            | JsonResponse data ->
                                let output = JsonConvert.SerializeObject data
                                context.Response.ContentType <- "application/json; charset=utf-8"
                                context.Response.WriteAsync output
                    with
                    // Catching unhandled exceptions with default handlers
                    | :? InputException as exn ->
                        let t = exn.GetType ()
                        context.Response.StatusCode <- 400
                        return! context.Response.WriteAsync $"{t.Name}: {exn.Message}"
                    | :? ServerException as exn ->
                        let message =
                            if env.IsDevelopment () then
                                let t = exn.GetType ()
                                $"{t.Name}: {exn.Message}"
                            else
                                "Server Error"
                        context.Response.StatusCode <- 500
                        return! context.Response.WriteAsync message
                } :> Task
                
            RequestDelegate handle

        let createEndpoint () =
            let handler = route.HttpHandler |> prepareDelegate route.ErrorHandlers
            match route.Pattern with
            | Connect p ->
                endpoints.MapMethods ( p, [ HttpMethods.Connect ], handler )
            | Delete p ->
                endpoints.MapMethods ( p, [ HttpMethods.Delete ], handler )
            | Get p ->
                endpoints.MapMethods ( p, [ HttpMethods.Get ], handler )
            | Head p ->
                endpoints.MapMethods ( p, [ HttpMethods.Head ], handler )
            | Options p ->
                endpoints.MapMethods ( p, [ HttpMethods.Options ], handler )
            | Patch p ->
                endpoints.MapMethods ( p, [ HttpMethods.Patch ], handler )
            | Post p ->
                endpoints.MapMethods ( p, [ HttpMethods.Post ], handler )
            | Put p ->
                endpoints.MapMethods ( p, [ HttpMethods.Put ], handler )
            | Trace p ->
                endpoints.MapMethods ( p, [ HttpMethods.Trace ], handler )
                
        let setupAuth ( endpoint: IEndpointConventionBuilder ) =
            match route.Auth with
            | NoneAuth -> endpoint
            | AnonAuth -> endpoint.AllowAnonymous ()
            | DefaultAuth -> endpoint.RequireAuthorization ()
            | PolicyAuth p -> p |> Array.ofList |> endpoint.RequireAuthorization
            | DataAuth d -> d |> Array.ofList |> endpoint.RequireAuthorization
            
        let setupCORS ( endpoint: IEndpointConventionBuilder ) =
            match route.CORS with
            | NoneCORS -> endpoint
            | PolicyCORS p -> endpoint.RequireCors p
            | NewPolicyCORS b -> endpoint.RequireCors b
            
        let setupHosts ( endpoint: IEndpointConventionBuilder ) =
            match route.Host with
            | [] -> endpoint
            | h -> endpoint.RequireHost ( h |> Array.ofList )
            
        let setupMetadata ( endpoint: IEndpointConventionBuilder ) =
            let descriptor = RouteDescription route :> Object
            let metadata = descriptor :: route.Metadata |> Array.ofList
            endpoint.WithMetadata metadata
        
        let setup =
            createEndpoint
            >> route.PreConfig
            >> setupAuth
            >> setupCORS
            >> setupHosts
            >> setupMetadata
            >> route.PostConfig
            >> ignore
        
        setup ()
        
        endpoints
    let getServiceSetup ( data: List<ServiceSetup> ) : ServiceSetup =
        match data.Count with
        | 0 -> fun _ _ -> id
        | _ -> data |> Seq.reduce ( fun a b -> fun env conf app -> a env conf app |> b env conf )
    let getAppSetup ( data: List<AppSetup> ) : AppSetup =
        match data.Count with
        | 0 -> fun _ _ -> id
        | _ -> data |> Seq.reduce ( fun a b -> fun env conf app -> a env conf app |> b env conf )
    let getEndpointSetup ( data: List<EndpointSetup> ) : EndpointSetup =
        match data.Count with
        | 0 -> id
        | _ -> data |> Seq.reduce ( >> )
    let configureServices ( webBuilder: IWebHostBuilder ) =
        let c = fun ( ctx: WebHostBuilderContext ) ( serv: IServiceCollection ) ->
            let env = ctx.HostingEnvironment
            let config = ctx.Configuration
            
            // Slot for custom services
            ( env, config, serv ) |||> getServiceSetup beforeServiceSetup |> ignore
            
            // Add Route Collection Service
            serv.AddSingleton<IRouteDescriptorService, SimpleRouteDescriptor> (
                fun _ -> SimpleRouteDescriptor routes )
            |> ignore
            
            // Setting up the templating service
            ( env, config, serv ) |||> templatingSetup.ConfigureServices |> ignore
            
            ( env, config, serv ) |||> staticFilesSetup.ConfigureServices |> ignore
            
            // Slot for custom services
            ( env, config, serv ) |||> getServiceSetup afterServiceSetup |> ignore
        
        webBuilder.ConfigureServices ( Action<WebHostBuilderContext, IServiceCollection> c )
    let configureEndpoints ( env: IWebHostEnvironment ) ( conf: IConfiguration ) ( app: IApplicationBuilder ) =
        let c = fun ( endpoints: IEndpointRouteBuilder ) ->
            let configureRoute = configureRoute env conf
            
            for route in routes do
                let r = route.Value
                endpoints |> configureRoute r |> ignore
            // Slot for manual endpoints
            endpoints |> getEndpointSetup endpointSetup |> ignore
        app.UseEndpoints ( Action<IEndpointRouteBuilder> c ) |> ignore
        
    let configureApp ( webBuilder: IWebHostBuilder ) =
        let c = fun ( ctx: WebHostBuilderContext ) ( app: IApplicationBuilder ) ->
            let env = ctx.HostingEnvironment
            let config = ctx.Configuration
            
            // Slot for custom app configurations
            ( env, config, app ) |||> getAppSetup beforeAppSetup |> ignore
            
            if env.IsDevelopment () then
                app.UseDeveloperExceptionPage () |> ignore
            
            // Slot for custom app configurations
            ( env, config, app ) |||> getAppSetup beforeRoutingSetup |> ignore
            
            // Applying static files service configuration if enabled
            ( env, config, app ) |||> staticFilesSetup.ConfigureApp |> ignore
            
            app.UseRouting () |> ignore
            
            // Slot for custom app configurations
            ( env, config, app ) |||> getAppSetup beforeEndpointSetup |> ignore
            
            ( env, config, app ) |||> configureEndpoints
            
            // Slot for custom app configurations
            ( env, config, app ) |||> getAppSetup afterEndpointSetup |> ignore
            
            // Slot for custom app configurations
            ( env, config, app ) |||> getAppSetup afterAppSetup |> ignore
            
        webBuilder.Configure ( Action<WebHostBuilderContext, IApplicationBuilder> c )
    
    let configureWebRoot ( webBuilder: IWebHostBuilder ) =
        if staticFilesSetup.Enabled then
            webBuilder.UseWebRoot staticFilesSetup.WebRoot
        else
            webBuilder
            
    let configureContentRoot ( webBuilder: IWebHostBuilder ) =
        if contentRoot.Length > 0 then
            webBuilder.UseContentRoot contentRoot
        else
            webBuilder
        
    let configureConfiguration ( hostBuilder: IHostBuilder ) =
        hostBuilder.ConfigureAppConfiguration ( Action<HostBuilderContext, IConfigurationBuilder> configSetup.Builder )
        
    let configureWebHostDefaults ( c: IWebHostBuilder->unit ) ( hostBuilder: IHostBuilder ) =
        hostBuilder.ConfigureWebHostDefaults ( Action<IWebHostBuilder> c )
        
    let configureHost ( webBuilder: IWebHostBuilder ) =
        webBuilder
        |> configureContentRoot
        |> configureWebRoot
        |> configureServices
        |> configureApp
        |> ignore
        
    let configureTestHost =
        fun ( webBuilder: IWebHostBuilder ) -> webBuilder.UseTestServer ()
        >> configureHost

    let createHostBuilder ( c: IWebHostBuilder->unit ) args =
        Host.CreateDefaultBuilder args
        |> configureConfiguration
        |> configureWebHostDefaults c
        
    member internal _.CreateHostBuilder args = createHostBuilder configureHost args
    member internal _.CreateTestBuilder args = createHostBuilder configureTestHost args
    member internal _.Routes with set value = routes <- value
    member internal _.Config with set value = configSetup.SetupWith value
    member _.BeforeServices with set value = value |> beforeServiceSetup.Add
    member _.AfterServices with set value = value |> afterServiceSetup.Add
    member _.BeforeApp with set value = value |> beforeAppSetup.Add
    member _.BeforeRouting with set value = value |> beforeRoutingSetup.Add
    member _.BeforeEndpoints with set value = value |> beforeEndpointSetup.Add
    member _.AfterEndpoints with set value = value |> afterEndpointSetup.Add
    member _.AfterApp with set value = value |> afterAppSetup.Add
    member _.Endpoint with set value = value |> endpointSetup.Add
    member val StaticFiles = staticFilesSetup
    member val Templating = templatingSetup
    member _.ContentRoot with set value = contentRoot <- value
