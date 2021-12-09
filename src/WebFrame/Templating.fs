module WebFrame.Templating

open WebFrame

type DotLiquidTemplateService () =
    member _.Render ( path: string ) ( o: obj ) =
        ()

module DotLiquid =
    let create ( app: App ) =
        DotLiquidTemplateService ()
