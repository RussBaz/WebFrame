module WebFrame.Tests.Helpers

open System

type EnvVarAction =
    | SetVarTo of string
    | DeleteVar

type EnvVar ( name: string, value: string ) =
    let action =
        match Environment.GetEnvironmentVariable name with
        | null -> DeleteVar
        | v -> SetVarTo v
            
    let restore () =
        match action with
        | SetVarTo v -> Environment.SetEnvironmentVariable ( name, v )
        | DeleteVar -> Environment.SetEnvironmentVariable ( name, null )
    
    do Environment.SetEnvironmentVariable ( name, value )
    interface IDisposable with
        member _.Dispose() = restore ()
