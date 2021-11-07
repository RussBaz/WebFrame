module WebFrame.Http

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http

type HttpMethod =
    | CONNECT
    | DELETE
    | GET
    | HEAD
    | OPTIONS
    | PATCH
    | POST
    | PUT
    | TRACE
    with
    static member FromString ( s: string ) =
        match s with
        | "CONNECT" -> CONNECT
        | "DELETE" -> DELETE
        | "GET" -> GET
        | "HEAD" -> HEAD
        | "OPTIONS" -> OPTIONS
        | "PATCH" -> PATCH
        | "POST" -> POST
        | "PUT" -> PUT
        | "TRACE" -> TRACE
        | s -> failwith $"Unknown HTTP Method {s}"

type HttpWorkload =
    | EndResponse
    | TextResponse of string
    | FileResponse of string
    | JsonResponse of obj
    
type HttpHandler = HttpContext -> HttpWorkload
type TaskHttpHandler = HttpContext -> Task<HttpWorkload>

type ErrorHandler = Exception -> HttpContext -> HttpWorkload option
type TaskErrorHandler = Exception -> HttpContext -> Task<HttpWorkload option>
