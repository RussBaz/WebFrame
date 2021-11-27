module WebFrame.RouteTypes

open System

open System.Collections.Generic
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure

open WebFrame.Http

type RoutePattern =
    | Connect of string
    | Delete of string
    | Get of string
    | Head of string
    | Options of string
    | Patch of string
    | Post of string
    | Put of string
    | Trace of string
    with
    static member ( + ) ( v1: RoutePattern, v2: string ) =
        match v1 with
        | Connect v -> v + v2 |> Connect
        | Delete v -> v + v2 |> Delete
        | Get v -> v + v2 |> Get
        | Head v -> v + v2 |> Head
        | Options v -> v + v2 |> Options
        | Patch v -> v + v2 |> Patch
        | Post v -> v + v2 |> Post
        | Put v -> v + v2 |> Put
        | Trace v -> v + v2 |> Trace
    static member ( + ) ( v1: string, v2: RoutePattern ) =
        match v2 with
        | Connect v -> v1 + v |> Connect
        | Delete v -> v1 + v |> Delete
        | Get v -> v1 + v |> Get
        | Head v -> v1 + v |> Head
        | Options v -> v1 + v |> Options
        | Patch v -> v1 + v |> Patch
        | Post v -> v1 + v |> Post
        | Put v -> v1 + v |> Put
        | Trace v -> v1 + v |> Trace
    static member value = function
        | Connect v -> v
        | Delete v -> v
        | Get v -> v
        | Head v -> v
        | Options v -> v
        | Patch v -> v
        | Post v -> v
        | Put v -> v
        | Trace v -> v
    static member method = function
        | Connect _ -> CONNECT
        | Delete _ -> DELETE
        | Get _ -> GET
        | Head _ -> HEAD
        | Options _ -> OPTIONS
        | Patch _ -> PATCH
        | Post _ -> POST
        | Put _ -> PUT
        | Trace _ -> TRACE
    override this.ToString () =
        match this with
        | Connect p -> $"CONNECT {p}"
        | Delete p -> $"DELETE {p}"
        | Get p -> $"GET {p}"
        | Head p -> $"HEAD {p}"
        | Options p -> $"OPTIONS {p}"
        | Patch p -> $"PATCH {p}"
        | Post p -> $"POST {p}"
        | Put p -> $"PUT {p}"
        | Trace p -> $"TRACE {p}"

type AuthorizationDef =
    | NoneAuth
    | AnonAuth
    | DefaultAuth
    | PolicyAuth of string list
    | DataAuth of IAuthorizeData list
    
type CORSDef =
    | NoneCORS
    | PolicyCORS of string
    | NewPolicyCORS of ( CorsPolicyBuilder->unit )
    
type FieldContent =
    | ValueContent
type FieldDef =
    | RequiredField
    | OptionalField
    | DefaultField
    
type RouteParamDef = {
    Name: string
    ExpectedType: Type
    Description: string
    Constraints: string
    Required: bool
}

type QueryParamDef = {
    Name: string
    ExpectedType: Type
    Description: string
    Constraints: string
    Required: bool
    List: bool
}

type HeaderDef = {
    Name: string
    Description: string
    Constraints: string
    Required: bool
    List: bool
}

type CookieDef = {
    Name: string
    Description: string
    Constraints: string
    Required: bool
}

type FormFieldDef = {
    Name: string
    ExpectedType: Type
    Description: string
    Constraints: string
    Required: bool
    List: bool
}

type JsonBodyDef = {
    ExpectedType: Type
}

type BodyDef =
    | FormBodyExpected of FormFieldDef list
    | JsonBodyExpected
    | BinaryBodyExpected
    
type RouteDef = {
    Name: string
    Pattern: RoutePattern
    Description: string
    Auth: AuthorizationDef
    CORS: CORSDef
    Host: string list
    PreConfig: IEndpointConventionBuilder->IEndpointConventionBuilder
    PostConfig: IEndpointConventionBuilder->IEndpointConventionBuilder
    HttpHandler: TaskHttpHandler
    ErrorHandlers: TaskErrorHandler list
    Metadata: obj list
}

module RouteDef =
    let create path = {
        Name = ""
        Pattern = path
        Description = ""
        Auth = NoneAuth
        CORS = NoneCORS
        Host = []
        PreConfig = id
        PostConfig = id
        HttpHandler = fun _ _ -> task { return EndResponse }
        ErrorHandlers = []
        Metadata = [] }
    
    let name ( n: string ) ( r: RouteDef ) = { r with Name = n }
    let prefixWith ( p: string ) ( r: RouteDef ) = { r with Pattern = p + r.Pattern }
    let description ( d: string ) ( r: RouteDef ) = { r with Description = d }
    let authorization ( a: AuthorizationDef ) ( r: RouteDef ) = { r with Auth = a }
    let cors ( c: CORSDef ) ( r: RouteDef ) = { r with CORS = c }
    let host ( h: string list ) ( r: RouteDef ) = { r with Host = h }
    let preConfig ( c: IEndpointConventionBuilder->IEndpointConventionBuilder ) ( r: RouteDef ) =
        { r with PreConfig = c }
    let postConfig ( c: IEndpointConventionBuilder->IEndpointConventionBuilder ) ( r: RouteDef ) =
        { r with PostConfig = c }
    let handler ( h: TaskHttpHandler ) ( r: RouteDef ) = { r with HttpHandler = h }
    let onError ( h: TaskErrorHandler) ( r: RouteDef ) = { r with ErrorHandlers = h :: r.ErrorHandlers }
    let onErrors ( h: TaskErrorHandler list ) ( r: RouteDef ) =
        let h = h |> List.rev
        let h = List.append h r.ErrorHandlers
        { r with ErrorHandlers = h }
    let metadata ( d: obj ) ( r: RouteDef ) = { r with Metadata = d :: r.Metadata }
    let createWithHandler p h = p |> create |> handler h |> name ( p.ToString () )
    
type Routes = Dictionary<RoutePattern, RouteDef>

type RouteDescription ( route: RouteDef ) =
    member val Name = route.Name
    member val Pattern = route.Pattern
    member val Description = route.Description

type IRouteDescriptorService =
    abstract member All: unit -> RouteDef list
    abstract member TryGet: RoutePattern -> RouteDef option
