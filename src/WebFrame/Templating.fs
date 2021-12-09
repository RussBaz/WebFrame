module WebFrame.Templating

open Microsoft.Extensions.FileProviders

open DotLiquid

open WebFrame

type TemplateCache ( contentRoot: string, path: string ) =
    let mutable value: string option = None
    let load () =
        use fileProvider = new PhysicalFileProvider ( contentRoot )
        ()
    member this.Path = path
    member this.Update ( v: string ) = value <- Some v

type DotLiquidTemplateService () =
    member _.Load ( path: string ) =
        ()

    member _.Render ( path: string ) ( o: obj ) =
        ()

module DotLiquid =
    let create ( app: App ) =
        let h = app.GetServiceProvider ()
        DotLiquidTemplateService ()
