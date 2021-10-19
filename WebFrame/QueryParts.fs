module WebFrame.QueryParts

open Microsoft.AspNetCore.Http

open WebFrame.Exceptions
open WebFrame.Converters

type QueryParameters ( req: HttpRequest ) =
    member _.AsString ( name: string ) =
        match req.Query.TryGetValue name with
        | true, v -> Some v
        | _ -> None
        |> Option.map ( fun i -> i.ToArray () |> List.ofArray )
        
    member this.Optional<'T when 'T : equality> ( name: string ) =
        name
        |> this.AsString
        |> Option.map ( List.map convertTo<'T> )
        |> Option.bind ( fun i -> if i |> List.contains None then None else Some i )
        |> Option.map ( List.map Option.get )
        
    member this.Required<'T when 'T : equality> ( name: string ) =
        name
        |> this.Optional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredQueryParameterException name |> raise )
            
    member this.All<'T when 'T : equality> ( name: string ) =
        name
        |> this.Optional<'T>
        |> Option.defaultValue []
        
    member this.Get<'T when 'T : equality> ( name: string ) ( d: 'T ) =
        name
        |> this.All<'T>
        |> List.tryHead
        |> Option.defaultValue d
        
    member val Raw = req.Query
