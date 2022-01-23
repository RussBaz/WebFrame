module WebFrame.Services

open System.Threading.Tasks

open Microsoft.AspNetCore.Http

open Microsoft.Extensions.Logging

open WebFrame.ConfigParts
open WebFrame.Configuration
open WebFrame.Http
open WebFrame.BodyParts
open WebFrame.CookieParts
open WebFrame.HeaderParts
open WebFrame.Logging
open WebFrame.QueryParts
open WebFrame.RouteParts
open WebFrame.RouteTypes
open WebFrame.ServicesParts
open WebFrame.Templating
    
type RequestServices ( ctx: HttpContext, defaults: SystemDefaults ) as this =
    let endpoint = ctx.GetEndpoint ()
    let metadata = endpoint.Metadata
    let routeDescription = metadata.GetMetadata<RouteDescription> ()
    
    member val Context = ctx
    member val Path = RequestPathProperties ctx.Request    
    member val Route = RouteParameters ctx.Request
    member val Query = QueryParameters ctx.Request
    member val Headers = Headers ( ctx.Request, ctx.Response )
    member val Cookies = Cookies ( ctx.Request, ctx.Response )
    member val Body = Body ( ctx.Request, lazy ( this.Services.Required () ) )
    member val Services: GenericServiceProvider = GenericServiceProvider ctx.RequestServices
    
    member val AppRoutes = AllRoutes ( lazy ( this.Services.Required () ) )
        
    member _.Redirect ( url, permanent ) =
        ctx.Response.Redirect ( url, permanent )
        EndResponse
        
    member val Config = RuntimeConfigs ( lazy ( this.Services.Required () ), lazy ( this.Services.Required () ) )
    member this.Redirect url = this.Redirect ( url, false )
    member _.EndResponse () = EndResponse
    member _.EndResponse ( t: string ) = TextResponse t
    member _.EndResponse ( j: obj ) = JsonResponse j
    member _.File n = FileResponse n
    member this.File ( name: string, contentType: string ) =
        this.ContentType <- contentType
        this.File name
    member this.Page ( path: string ) ( o: obj ) =
        let renderer = this.Services.Required<ITemplateRenderer> ()
        renderer.Render path o
    member val Endpoint = endpoint
    member val RouteDescription = routeDescription
    member _.StatusCode with set v = ctx.Response.StatusCode <- v
    member this.ContentType
        with get () = this.Headers.Get "Content-Type" ""
        and set v = this.Headers.Set "Content-Type" [ v ]
    member val Log =
        let category = defaults |> SystemDefaults.getLoggerNameForCategory routeDescription.Name
        Logger ( lazy ( this.Services.Required () ), category )
    member this.LoggerFor ( categoryName: string ) =
        let f = this.Services.Required<ILoggerFactory> ()
        f.CreateLogger categoryName
    member _.EnableBuffering () = ctx.Request.EnableBuffering ()
        
type ServicedHandler = RequestServices -> HttpWorkload
type TaskServicedHandler = RequestServices -> Task<HttpWorkload>

type HandlerSetup = ServicedHandler -> HttpHandler
type TaskHandlerSetup = TaskServicedHandler -> TaskHttpHandler

type ServicedErrorHandler<'T when 'T :> exn> = 'T -> RequestServices -> HttpWorkload
type ServicedTaskErrorHandler<'T when 'T :> exn> = 'T -> RequestServices -> Task<HttpWorkload>

type ErrorHandlerSetup<'T when 'T :> exn> = ServicedErrorHandler<'T> -> ErrorHandler
type TaskErrorHandlerSetup<'T when 'T :> exn> = ServicedTaskErrorHandler<'T> -> TaskErrorHandler
