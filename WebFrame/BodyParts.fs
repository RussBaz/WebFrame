module WebFrame.BodyParts

open System.Threading.Tasks
open Microsoft.AspNetCore.Http

open WebFrame.Converters
open WebFrame.Exceptions

type FormEncodedBody ( req: HttpRequest ) =
    let mutable form = None
    let getForm () =
        match form with
        | Some v -> v
        | None ->
            if req.HasFormContentType then
                form <- Some req.Form
            else
                raise ( MissingRequiredFormException () )
                
            form |> Option.get
    member private _.InnerAsString ( name: string ) =
        let form = getForm ()
        match form.TryGetValue name with
        | true, v -> Some v
        | _ -> None
        |> Option.map ( fun i -> i.ToArray () |> List.ofArray )
        
    member this.AsString ( name: string ) =
        try
            name |> this.InnerAsString
        with
        | :? MissingRequiredFormException -> None
        
    member private this.InnerOptional<'T when 'T : equality> ( name: string ) =
        name
        |> this.InnerAsString
        |> Option.map ( List.map convertTo<'T> )
        |> Option.bind ( fun i -> if i |> List.contains None then None else Some i )
        |> Option.map ( List.map Option.get )
        
    member this.Optional<'T when 'T : equality> ( name: string ) : 'T list option =
        try
            name |> this.InnerOptional
        with
        | :? MissingRequiredFormException -> None
        
    member this.Required<'T when 'T : equality> ( name: string ) =
        name
        |> this.InnerOptional<'T>
        |> Option.defaultWith ( fun _ -> MissingRequiredFormFieldException name |> raise )
        
    member this.First<'T when 'T : equality> ( name: string ) =
        name
        |> this.Required<'T>
        |> List.tryHead
        |> Option.defaultWith ( fun _ -> MissingRequiredFormFieldException name |> raise )
            
    member this.All<'T when 'T : equality> ( name: string ) =
        name
        |> this.Optional<'T>
        |> Option.defaultValue []
        
    member this.Get<'T when 'T : equality> ( name: string ) ( d: 'T ) =
        name
        |> this.Optional<'T>
        |> Option.defaultValue []
        |> List.tryHead
        |> Option.defaultValue d
        
    member _.Raw with get () = try getForm () |> Some with | :? MissingRequiredFormException -> None
    member val IsPresent = req.HasFormContentType
    
type JsonEncodedBody ( req: HttpRequest ) =
    member _.Required<'T> () =
        if req.HasJsonContentType () then
            req.ReadFromJsonAsync<'T> ()
        else
            raise ( MissingRequiredJsonException () )
            
    member _.Optional<'T> () =
        if req.HasJsonContentType () then
            try
                task {
                    let! j = req.ReadFromJsonAsync<'T> ()
                    return Some j
                }
                |> ValueTask<'T option>
            with
            | :? MissingRequiredJsonException -> ValueTask.FromResult None
        else
            ValueTask.FromResult None
    member this.Raw = if this.IsPresent then Some req.Body else None
    member val IsPresent = req.HasJsonContentType ()
    
type Body ( req: HttpRequest ) =
    member val Form = FormEncodedBody req
    member val Json = JsonEncodedBody req
    member val Raw = req.Body
    member val RawPipe = req.BodyReader
