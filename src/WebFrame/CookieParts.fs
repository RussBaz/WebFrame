module WebFrame.CookieParts

open Microsoft.AspNetCore.Http

open WebFrame.Exceptions

type Cookies ( req: HttpRequest, res: HttpResponse ) =
    let cIn = req.Cookies
    let cOut = res.Cookies
    
    member this.Required ( k: string ) = k |> this.Optional |> Option.defaultWith ( fun _ -> MissingRequiredCookieException k |> raise )
    member this.Get ( k: string ) ( d: string ) = k |> this.Optional |> Option.defaultValue d
    member this.Optional ( k: string ) =
        match cIn.TryGetValue k with
        | true, v -> Some v
        | _ -> None
        
    member this.Set ( k: string ) ( v: string ) = cOut.Append ( k, v )
    member this.SetWithOptions ( k: string ) ( v: string ) ( o: CookieOptions ) = cOut.Append ( k, v, o )
    member this.Delete ( k: string ) = cOut.Delete k
    member this.DeleteWithOptions ( k: string ) ( o: CookieOptions ) = cOut.Delete ( k, o )
    
    member val Raw = {| In = cIn; Out = cOut |}
