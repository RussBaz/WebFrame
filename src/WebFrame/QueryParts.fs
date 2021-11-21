module WebFrame.QueryParts

open Microsoft.AspNetCore.Http

open WebFrame.Exceptions
open WebFrame.Converters

type QueryParameters ( req: HttpRequest ) =       
    member this.String ( name: string ) : string option =
        name
        |> this.AllString
        |> Option.bind List.tryHead
        
    member this.Optional<'T when 'T : equality> ( name: string ) : 'T option =
        name
        |> this.String
        |> Option.bind convertTo<'T>
        
    member this.Required<'T when 'T : equality> ( name: string ) : 'T =
        name
        |> this.Optional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredQueryParameterException name |> raise )
        
    member this.Get<'T when 'T : equality> ( name: string ) ( d: 'T ) : 'T =
        name
        |> this.Optional<'T>
        |> Option.defaultValue d
            
    member this.All<'T when 'T : equality> ( name: string ) : 'T list =
        name
        |> this.AllOptional<'T>
        |> Option.defaultValue []
        
    member this.AllString<'T when 'T : equality> ( name: string ) : string list option =
        match req.Query.TryGetValue name with
        | true, v -> Some v
        | _ -> None
        |> Option.map ( fun i -> i.ToArray () |> List.ofArray )
        
    member this.AllOptional<'T when 'T : equality> ( name: string ) : 'T list option =
        name
        |> this.AllString
        |> Option.map ( List.map convertTo<'T> )
        |> Option.bind ( fun i -> if i |> List.contains None then None else Some i )
        |> Option.map ( List.map Option.get )
        |> Option.bind ( fun i -> if i.Length = 0 then None else Some i )
        
    member this.AllRequired<'T when 'T : equality> ( name: string ) : 'T list =
        name
        |> this.AllOptional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredQueryParameterException name |> raise )
        
    member this.Count<'T when 'T : equality> ( name: string ) : int =
        name
        |> this.AllString
        |> Option.map List.length
        |> Option.defaultValue 0

    member val Raw = req.Query
