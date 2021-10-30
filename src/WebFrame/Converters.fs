module WebFrame.Converters

open System

open WebFrame.Extensions

let canParse<'T> () =    
    match typeof<'T> with
    | t when t.IsPrimitive -> true
    | t when t = typeof<string> -> true
    | t when t = typeof<decimal> -> true
    | t when t = typeof<Guid> -> true
    | _ -> false

let convertTo<'T> ( data: string ) =
    try
        let t = typeof<'T>
        if canParse<'T> () then
            match t with
            | t when t = typeof<Guid> -> Guid.FromString data |> Option.map ( fun i -> i :> obj :?> 'T )
            | t -> Convert.ChangeType ( data, t ) :?> 'T |> Some
        else None
    with
    | :? InvalidCastException
    | :? FormatException
    | :? OverflowException
    | :? ArgumentNullException -> None

let orElse<'T> ( defaultValue: 'T ) ( data: 'T option ) =
    data |> Option.defaultValue defaultValue
