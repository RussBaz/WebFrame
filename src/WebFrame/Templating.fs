module WebFrame.Templating

open System.IO
open System.Collections.Generic

open Microsoft.AspNetCore.Hosting

open Microsoft.Extensions.Logging

open DotLiquid
open DotLiquid.FileSystems

open WebFrame
open WebFrame.Http
open WebFrame.Exceptions

type TemplateCache ( logger: ILogger, env: IWebHostEnvironment, path: string ) =
    let mutable template: Template option = None
    
    let update ( value: string ) =
        let v = value |> Template.Parse
        template <- Some v
        v

    let load () =
        let fileProvider = env.ContentRootFileProvider
        let file = fileProvider.GetFileInfo path
        
        if not file.Exists then
            logger.LogError $"DotLiquid template was not found at: {file.PhysicalPath}"
            raise ( MissingTemplateException path )
        
        use stream = file.CreateReadStream ()
        use reader = new StreamReader ( stream )
        
        reader.ReadToEnd ()
        |> update
        
    member this.Path = path
    member this.Value =
        match template with
        | Some v -> v
        | None -> load ()

type DotLiquidTemplateService ( app: App ) =
    let mutable env = None
    let mutable logger = None
    let templates = Dictionary<string, TemplateCache> ()
    
    let setup () =
        let loggerPrefix = app.Defaults.LoggerPrefix |> fun i -> if i.Length > 0 then $"{i}." else ""
        let loggerName = $"{loggerPrefix}DotLiquidTemplateService"
        let serviceProvider = app.GetServiceProvider ()
        let e = serviceProvider.Required<IWebHostEnvironment> ()
        let lf = serviceProvider.Required<ILoggerFactory> ()
        Template.FileSystem <- LocalFileSystem e.ContentRootPath
        env <- Some e
        logger <- loggerName |> lf.CreateLogger |> Some
        e
        
    let getEnv () =
        match env with
        | Some e -> e
        | None -> setup ()
        
    let getLogger () =
        if logger.IsNone then setup () |> ignore
        logger.Value

    member _.Load ( path: string ) =
        match templates.TryGetValue path with
        | true, v -> v
        | _ ->
            let t = TemplateCache ( getLogger(), getEnv (), path )
            templates.[ path ] <- t
            t

    member this.Render ( path: string ) ( o: obj ) =
        let c = this.Load path
        let t = c.Value
        
        o |> Hash.FromAnonymousObject |> t.Render |> HtmlResponse

module DotLiquidProvider =
    let register ( app: App ) = DotLiquidTemplateService app
