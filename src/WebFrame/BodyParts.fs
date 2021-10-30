module WebFrame.BodyParts

open System
open System.IO
open System.Text
open Microsoft.AspNetCore.Http

open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Newtonsoft.Json
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
    let mutable unknownEncoding = false
    
    let jsonSettings = JsonSerializerSettings ( MissingMemberHandling = MissingMemberHandling.Error )
    
    let jsonCharset =
        match MediaTypeHeaderValue.TryParse ( StringSegment req.ContentType ) with
        | true, v ->
            if v.MediaType.Equals ( "application/json", StringComparison.OrdinalIgnoreCase ) then
                Some v.Charset
            elif v.Suffix.Equals ( "json", StringComparison.OrdinalIgnoreCase ) then
                Some v.Charset
            else
                None
        | _ ->
            None
            
    let jsonEncoding =
        match jsonCharset with
        | Some c ->
            try
                if c.Equals ( "utf-8", StringComparison.OrdinalIgnoreCase ) then
                    Encoding.UTF8 |> Some
                elif c.HasValue then
                    Encoding.GetEncoding c.Value |> Some
                else
                    None
            with
            | _ ->
                unknownEncoding <- true
                None
        | None -> None
        
    let notJsonContentType = jsonCharset.IsNone || unknownEncoding
            
    member private _.ReadJson<'T> () = task {
        if notJsonContentType then raise ( MissingRequiredJsonException () )
        
        let en = jsonEncoding |> Option.defaultValue Encoding.UTF8        
        
        use br = new StreamReader ( req.Body, en )
                
        let! body = br.ReadToEndAsync ()
        
        try
            return JsonConvert.DeserializeObject<'T> ( body, jsonSettings )
        with
        | :? JsonSerializationException -> return raise ( MissingRequiredJsonException () )
    }
    
    member this.Required<'T> () = this.ReadJson<'T> ()
            
    member this.Optional<'T> () = task {
        try
            let! r = this.ReadJson<'T> ()
            return Some r
        with
        | :? MissingRequiredJsonException -> return None
    }
    member this.Raw = if this.IsPresent then Some req.Body else None
    member val IsPresent = not notJsonContentType
    
type Body ( req: HttpRequest ) =
    member val Form = FormEncodedBody req
    member val Json = JsonEncodedBody req
    member val Raw = req.Body
    member val RawPipe = req.BodyReader
