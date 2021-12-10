module WebFrame.ConfigParts

open Microsoft.AspNetCore.Hosting

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting

open WebFrame.Exceptions
open WebFrame.Converters

type RuntimeConfigs ( conf: Lazy<IConfiguration>, env: Lazy<IWebHostEnvironment> ) =
    member _.String ( name: string ) =
        let r = conf.Value.[ name ]
        
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
        |> this.String
        |> Option.bind convertTo<'T>
        
    member _.Raw = conf.Value
    member _.ApplicationName = env.Value.ApplicationName
    member _.EnvironmentName = env.Value.EnvironmentName
    member _.IsDevelopment = env.Value.IsDevelopment ()
    member _.IsStaging = env.Value.IsStaging ()
    member _.IsProduction = env.Value.IsProduction ()
    member _.IsEnvironment name = env.Value.IsEnvironment name
