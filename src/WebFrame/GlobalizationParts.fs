module WebFrame.GlobalizationParts

open System
open System.Globalization
open Microsoft.AspNetCore.Http
open WebFrame.Configuration

type Globalization ( ctx: HttpContext, conf: Lazy<IGlobalizationConfig> ) =
    member _.RequestCulture with get () =
        let t = ctx.Request.GetTypedHeaders ()
        let l = t.AcceptLanguage
        let d = conf.Value.DefaultCulture
        let allowed = conf.Value.AllowedCultures |> List.map ( fun i -> i.Name ) |> set
        
        if l.Count < 1 then
            d
        else
            l
            |> Seq.map ( fun i -> i.Value.Value, i.Quality |> Option.ofNullable |> Option.defaultValue 1 )
            |> Seq.groupBy snd
            |> Seq.sortByDescending fst
            |> Seq.head
            |> snd
            |> Seq.head
            |> fun ( a, _ ) ->
                try
                    if allowed |> Set.contains a then
                        CultureInfo a
                    else
                        d
                with
                | :? ArgumentNullException -> d
                | :? CultureNotFoundException -> d
