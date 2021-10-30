module WebFrame.ConfigParts

open Microsoft.Extensions.Configuration

open WebFrame.Exceptions
open WebFrame.Converters

type RuntimeConfigs ( conf: IConfiguration ) =
    member _.AsString ( name: string ) =
        let r = conf.[ name ]
        
        match box r with
        | null -> None
        | _ -> Some r
        
    member this.Get<'T> ( name: string ) ( d: 'T ) =
        name
        |> this.Optional
        |> Option.defaultValue d
        
    member this.Required<'T> ( name: string ) =
        name
        |> this.Optional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredConfigException name |> raise )
        
    member this.Optional<'T> ( name: string ) =
        name
        |> this.AsString
        |> Option.bind convertTo<'T>
        
    member _.Raw = conf
