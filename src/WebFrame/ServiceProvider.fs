module WebFrame.ServiceProvider

open System

open WebFrame.Exceptions

type GenericServiceProvider ( provider: IServiceProvider ) =
    member _.Optional<'T> () =
        let t = typeof<'T>
        match provider.GetService t with
        | null -> None
        | v ->
            try
                v :?> 'T |> Some
            with
            | :? InvalidCastException -> None
            
    member this.Required<'T> () =
        let t = typeof<'T>
        match this.Optional<'T> () with
        | Some v -> v
        | None -> raise ( MissingRequiredDependencyException t.Name )
        
    member this.Get<'T> ( d: unit -> 'T ) = this.Optional<'T> () |> Option.defaultWith d
