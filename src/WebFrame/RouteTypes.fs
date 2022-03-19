module WebFrame.RouteTypes

open System

open System.Collections.Generic
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure

open WebFrame.Http

type RoutePattern = {
    Path: string
    Methods: Set<HttpMethod>
}
    with
    static member ( + ) ( v1: RoutePattern, v2: string ) =
        { v1 with Path = v1.Path + v2 }
    static member ( + ) ( v1: string, v2: RoutePattern ) =
        { v2 with Path = v1 + v2.Path }
    override this.ToString () =
        let s = this.Methods |> Seq.map ( fun i -> i.ToString () ) |> Seq.sort |> String.concat "; "
        $"[ {s} ]"

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

module RoutePattern =
    let create ( path: string ) ( methods: HttpMethod list ) =
        {
            Path = path
            Methods = methods |> Set.ofList
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
    let generate ( path: string ) ( methods: HttpMethod list ) =
        methods
        |> RoutePattern.create path
    let apply ( f: RouteDef -> RouteDef ) ( d: ( RoutePattern * RouteDef ) list ) =
        d
        |> List.map ( fun ( p, d ) -> p, f d )
    
type Routes = Dictionary<RoutePattern, RouteDef>

type RouteDescription ( route: RouteDef ) =
    member val Name = route.Name
    member val Pattern = route.Pattern
    member val Description = route.Description

type IRouteDescriptorService =
    abstract member All: unit -> RouteDef list
    abstract member TryGet: RoutePattern -> RouteDef option
