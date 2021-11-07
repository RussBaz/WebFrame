module WebFrame.Configuration

open System.Collections.Generic

type ConfigOverrides () =
    let config = Dictionary<string, string> ()
    
    let get i =
        match config.TryGetValue i with
        | true, v -> v
        | _ -> ""
    let set i v = config.[ i ] <- v
    
    member _.Item with get i = get i and set i v = config.[ i ] <- v
    member _.ConnectionStrings with get i = get $"ConnectionStrings:{i}" and set i v = set $"ConnectionStrings:{i}" v
    member _.Raw = config
