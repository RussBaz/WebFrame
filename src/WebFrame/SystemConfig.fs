module WebFrame.SystemConfig

open System
open System.Globalization
open System.IO
open System.Threading.Tasks
open System.Collections.Generic

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.StaticFiles
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

type TemplateConfiguration = SystemDefaults -> string -> IServiceProvider -> ITemplateRenderer

type ExceptionSetup ( _defaultConfig: SystemDefaults ) =
    member val ShowInputExceptionsByDefault = true with get, set
    member val ShowServerExceptionsByDefault = false with get, set
    member val ShowUserExceptionsByDefault = true with get, set
    
    // Environment Name -> true - show / false - hide
    member val InputExceptionFilter: Map<string, bool> = Map [] with get, set
    member val ServerExceptionFilter: Map<string, bool> =
        Map [
            Environments.Development, true
        ] with get, set
        
    member val UserExceptionFilter: Map<string, bool> = Map [] with get, set
        
    member internal this.ShowFullInputExceptionFor ( envName: string ) =
        this.InputExceptionFilter
        |> Map.tryFind envName
        |> Option.defaultValue this.ShowInputExceptionsByDefault
    member internal this.ShowFullServerExceptionFor ( envName: string ) =
        this.ServerExceptionFilter
        |> Map.tryFind envName
        |> Option.defaultValue this.ShowServerExceptionsByDefault
    member internal this.ShowFullUserExceptionFor ( envName: string ) =
        this.UserExceptionFilter
        |> Map.tryFind envName
        |> Option.defaultValue this.ShowUserExceptionsByDefault
        
    member internal this.ConfigureServices: ServiceSetup = fun env _ services ->
        services.AddSingleton<IUserExceptionFilter> {
            new IUserExceptionFilter with
                member _.ShowUserException = this.ShowFullUserExceptionFor env.EnvironmentName
        }

type GlobalizationSetup ( _defaultConfig: SystemDefaults ) =
    let mutable cultures = set [ CultureInfo.CurrentCulture.Name ]
    member val DefaultCulture = CultureInfo.CurrentCulture with get, set
    member _.AllowedCultures
        with get () = cultures |> Seq.map CultureInfo |> List.ofSeq
        and set ( c: CultureInfo list ) = cultures <- c |> Seq.map ( fun i -> i.Name ) |> set
        
    member internal this.ConfigureServices: ServiceSetup = fun _ _ services ->
        services.AddSingleton<IGlobalizationConfig> {
            new IGlobalizationConfig with
                member _.DefaultCulture = this.DefaultCulture
                member _.AllowedCultures = this.AllowedCultures
        }

type JsonSerializationSetup ( _defaultConfig: SystemDefaults ) =
    let defaultSettings () =
        let s =
            JsonSerializerSettings (
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor )
        s
        
    member this.Default with get () = defaultSettings ()
    member val Settings = defaultSettings () with get, set
        
type JsonDeserializationService ( settings: JsonSerializerSettings ) =
    interface IJsonDeserializationService with
        member this.Settings = settings
    
type JsonDeserializationSetup ( _defaultConfig: SystemDefaults ) =
    let defaultSettings () =
        let s =
            JsonSerializerSettings (
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                ContractResolver = RequireAllPropertiesContractResolver () )
        s
    
    member this.Default with get () = defaultSettings ()
    member val Settings = defaultSettings () with get, set
    member internal this.ConfigureServices: ServiceSetup = fun _ _ services ->
        let settingsProvider = JsonDeserializationService this.Settings
        services.AddSingleton<IJsonDeserializationService> settingsProvider
        
type JsonSetup ( defaultConfig: SystemDefaults ) =
    member val Serialization = JsonSerializationSetup defaultConfig
    member val Deserialization = JsonDeserializationSetup defaultConfig

type TemplatingSetup ( defaultConfig: SystemDefaults ) =
    let defaultSetup: TemplateConfiguration = fun defaultConfig root i ->
        let loggerFactory = i.GetService<ILoggerFactory> ()
        let env = i.GetService<IWebHostEnvironment> ()
        
        DotLiquidTemplateService ( defaultConfig, root, loggerFactory, env )
    let mutable userSetup: TemplateConfiguration option = None
    let getSetup ( defaultConfig: SystemDefaults ) ( root: string ) =
        let setup = userSetup |> Option.defaultValue defaultSetup
        setup defaultConfig root
    
    member val DefaultRenderer = defaultSetup
    member val TemplateRoot = "." with get, set
    member val Enabled = true with get, set
    member _.CustomConfiguration with set ( s: TemplateConfiguration ) = userSetup <- Some s
    
    member internal this.ConfigureServices: ServiceSetup = fun env _ services ->
        if this.Enabled then
            let templateRoot = Path.Combine ( env.ContentRootPath, this.TemplateRoot ) |> Path.GetFullPath
            let setup = fun i -> getSetup defaultConfig templateRoot i :> obj
            let t = typeof<ITemplateRenderer>
            services.AddSingleton ( t, setup )
        else
            services
    
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
    let jsonSetup = JsonSetup defaultConfig
    let globalizationSetup = GlobalizationSetup defaultConfig
    let exceptionSetup = ExceptionSetup defaultConfig
    
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
                            | FileResponse f ->
                                let path = $"{env.ContentRootPath}/{f}"
                                if String.IsNullOrEmpty context.Response.ContentType then
                                    let provider = FileExtensionContentTypeProvider ()
                                    let ct =
                                        match provider.TryGetContentType path with
                                        | true, v -> v
                                        | false, _ -> "text/plain"
                                    context.Response.ContentType <- ct
                                context.Response.SendFileAsync path
                            | JsonResponse data ->
                                let output = JsonConvert.SerializeObject ( data, jsonSetup.Serialization.Settings )
                                if String.IsNullOrEmpty context.Response.ContentType then
                                    context.Response.ContentType <- "application/json; charset=utf-8"
                                context.Response.WriteAsync output
                    with
                    // Catching unhandled exceptions with default handlers
                    | :? InputException as exn ->
                        let message =
                            if exceptionSetup.ShowFullInputExceptionFor env.EnvironmentName then
                                let t = exn.GetType ()
                                $"{t.Name}: {exn.Message}"
                            else
                                "Input Validation Error"
                        context.Response.StatusCode <- 400
                        return! context.Response.WriteAsync message
                    | :? ServerException as exn ->
                        let message =
                            if exceptionSetup.ShowFullServerExceptionFor env.EnvironmentName then
                                let t = exn.GetType ()
                                $"{t.Name}: {exn.Message}"
                            else
                                "Server Error"
                        context.Response.StatusCode <- 500
                        return! context.Response.WriteAsync message
                } :> Task
                
            RequestDelegate handle

        let createEndpoint r =
            let handler = r.HttpHandler |> prepareDelegate r.ErrorHandlers
            let p = r.Pattern.Path
            r.Pattern.Methods
            |> List.ofSeq
            |> List.sort
            |> List.map ( function
                | CONNECT ->
                    endpoints.MapMethods ( p, [ HttpMethods.Connect ], handler )
                | DELETE ->
                    endpoints.MapMethods ( p, [ HttpMethods.Delete ], handler )
                | GET ->
                    endpoints.MapMethods ( p, [ HttpMethods.Get ], handler )
                | HEAD ->
                    endpoints.MapMethods ( p, [ HttpMethods.Head ], handler )
                | OPTIONS ->
                    endpoints.MapMethods ( p, [ HttpMethods.Options ], handler )
                | PATCH ->
                    endpoints.MapMethods ( p, [ HttpMethods.Patch ], handler )
                | POST ->
                    endpoints.MapMethods ( p, [ HttpMethods.Post ], handler )
                | PUT ->
                    endpoints.MapMethods ( p, [ HttpMethods.Put ], handler )
                | TRACE ->
                    endpoints.MapMethods ( p, [ HttpMethods.Trace ], handler )
            )
                
        let setupAuth ( endpoints: IEndpointConventionBuilder list ) =
            endpoints
            |> List.map (
                fun e ->
                    match route.Auth with
                    | NoneAuth -> e
                    | AnonAuth -> e.AllowAnonymous ()
                    | DefaultAuth -> e.RequireAuthorization ()
                    | PolicyAuth p -> p |> Array.ofList |> e.RequireAuthorization
                    | DataAuth d -> d |> Array.ofList |> e.RequireAuthorization
            )
            
        let setupCORS ( endpoints: IEndpointConventionBuilder list ) =
            endpoints
            |> List.map (
                fun e ->
                    match route.CORS with
                    | NoneCORS -> e
                    | PolicyCORS p -> e.RequireCors p
                    | NewPolicyCORS b -> e.RequireCors b
            )
            
        let setupHosts ( endpoints: IEndpointConventionBuilder list ) =
            endpoints
            |> List.map (
                fun e ->
                    match route.Host with
                    | [] -> e
                    | h -> e.RequireHost ( h |> Array.ofList )
            )
            
        let setupMetadata ( endpoints: IEndpointConventionBuilder list ) =
            let descriptor = RouteDescription route :> Object
            let metadata = descriptor :: route.Metadata |> Array.ofList
            
            endpoints |> List.map ( fun e -> e.WithMetadata metadata )
        
        let setup =
            createEndpoint
            >> List.map route.PreConfig
            >> setupAuth
            >> setupCORS
            >> setupHosts
            >> setupMetadata
            >> List.map route.PostConfig
            >> ignore
        
        setup route
        
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
            ( env, config, serv ) |||> jsonSetup.Deserialization.ConfigureServices |> ignore
            ( env, config, serv ) |||> globalizationSetup.ConfigureServices |> ignore
            ( env, config, serv ) |||> exceptionSetup.ConfigureServices |> ignore
            
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
    member val Json = jsonSetup
    member val Globalization = globalizationSetup
    member val Exceptions = exceptionSetup
    member _.ContentRoot with set value = contentRoot <- value
