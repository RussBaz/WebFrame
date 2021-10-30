module WebFrame.RouteParts

open Microsoft.AspNetCore.Http

open WebFrame.Exceptions
open WebFrame.Converters
open WebFrame.RouteTypes


             
type RequestPathProperties ( req: HttpRequest ) =
    member val Method = req.Method
    member val Protocol = req.Protocol
    member val Scheme = req.Scheme
    member val Host = req.Host.Host
    member val Port = req.Host.Port |> Option.ofNullable
    member val PathBase = req.PathBase.Value
    member val Path = req.Path.Value
    member val QueryString = req.QueryString.Value
    member val IsHttps = req.IsHttps
    
type RouteParameters ( req: HttpRequest ) =
    member _.AsString ( name: string ) =
        match req.RouteValues.TryGetValue name with
        | true, v -> Some v
        | _ -> None
        |> Option.map ( fun i -> i :?> string )
        
    member this.Get<'T> ( name: string ) ( d: 'T ) =
        name
        |> this.Optional
        |> Option.defaultValue d
        
    member this.Required<'T> ( name: string ) =
        name
        |> this.Optional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredRouteParameterException name |> raise )
        
    member this.Optional<'T> ( name: string ) =
        name
        |> this.AsString
        |> Option.bind convertTo<'T>
        
    member val Raw = req.RouteValues
    
type AllRoutes ( init: unit -> IRouteDescriptorService ) =
    let mutable rootService = None
    member _.All () =
        match rootService with
        | None ->
            let v = init ()
            rootService <- Some v
            v.All ()
        | Some v ->
            v.All ()
            
    member _.TryGet name =
        match rootService with
        | None ->
            let v = init ()
            rootService <- Some v
            v.TryGet name
        | Some v ->
            v.TryGet name
