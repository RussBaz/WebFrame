module WebFrame.HeaderParts

open Microsoft.AspNetCore.Http

open WebFrame.Exceptions

type Headers ( req: HttpRequest, res: HttpResponse ) =
    let hIn = req.Headers
    let hOut = res.Headers
    
    member _.All ( k: string ) = k |> hIn.GetCommaSeparatedValues |> List.ofArray
    
    member this.Get ( k: string ) ( d: string ) = k |> this.Optional |> Option.defaultValue d
    member this.Optional ( k: string ) = k |> this.All |> List.tryHead
    member this.Required ( k: string ) = k |> this.Optional |> Option.defaultWith ( fun _ -> MissingRequiredHeaderException k |> raise )
    
    member this.Set ( k: string ) ( v: string list ) = hOut.SetCommaSeparatedValues ( k, Array.ofList v )
    member this.Append ( k: string ) ( v: string ) = hOut.AppendCommaSeparatedValues ( k, [| v |] )
    member this.Delete ( k: string ) = hOut.Remove k |> ignore
    
    member this.Count ( k: string ) = k |> this.All |> List.length 
    member val Raw = {| In = hIn; Out = hOut |}
