module WebFrame.Extensions

open System
        
type Guid with
    static member FromString ( data: string ) =
        match Guid.TryParse data with
        | true, g -> Some g
        | _ -> None
