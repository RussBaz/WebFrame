module WebFrame.Templating

open System.IO
open System.Collections.Generic

open Microsoft.AspNetCore.Hosting

open Microsoft.Extensions.Logging

open DotLiquid
open DotLiquid.FileSystems

open WebFrame.Configuration
open WebFrame.Http
open WebFrame.Exceptions

type ITemplateCache =
    abstract member Path: string
    abstract member Value: Template

type ITemplateRenderer =
    abstract member Load: path: string -> unit
    abstract member Render: path: string -> o: obj -> HttpWorkload

type TemplateCache ( logger: ILogger, env: IWebHostEnvironment, path: string ) =
    let mutable template: Template option = None

    let load () =
        let fileProvider = env.ContentRootFileProvider
        let file = fileProvider.GetFileInfo path
        
        if not file.Exists then
            logger.LogError $"DotLiquid template was not found at: {file.PhysicalPath}"
            raise ( MissingTemplateException path )
        
        use stream = file.CreateReadStream ()
        use reader = new StreamReader ( stream )
        
        let t = reader.ReadToEnd () |> Template.Parse
        template <- Some t
        t
        
    interface ITemplateCache with
        member this.Path = path
        member this.Value =
            match template with
            | Some v -> v
            | None -> load ()

type DotLiquidTemplateService ( defaults: SystemDefaults, rootPath: string, loggerFactory: ILoggerFactory, env: IWebHostEnvironment ) =
    let logger =
        let loggerName = defaults |> SystemDefaults.getLoggerNameForCategory "DotLiquidTemplateService"
        loggerFactory.CreateLogger loggerName
    let templates = Dictionary<string, TemplateCache> ()
    
    let load ( path: string ): ITemplateCache =
        match templates.TryGetValue path with
            | true, v -> v
            | _ ->
                let t = TemplateCache ( logger, env, path )
                templates.[ path ] <- t
                t
    
    do
        logger.LogInformation $"Template Root Set to {rootPath}"
        Template.FileSystem <- LocalFileSystem rootPath

    interface ITemplateRenderer with
        member _.Load ( path: string ) = load path |> ignore
        member this.Render ( path: string ) ( o: obj ) =
            let c = load path
            let t = c.Value
            
            o |> Hash.FromAnonymousObject |> t.Render |> HtmlResponse
