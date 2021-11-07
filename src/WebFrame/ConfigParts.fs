module WebFrame.ConfigParts

open Microsoft.AspNetCore.Hosting

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting

open WebFrame.Exceptions
open WebFrame.Converters

type RuntimeConfigs ( conf: IConfiguration, env: IWebHostEnvironment ) =
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
    member val ApplicationName = env.ApplicationName
    member val EnvironmentName = env.EnvironmentName
    member _.IsDevelopment = env.IsDevelopment ()
    member _.IsStaging = env.IsStaging ()
    member _.IsProduction = env.IsProduction ()
    member _.IsEnvironment name = env.IsEnvironment name
