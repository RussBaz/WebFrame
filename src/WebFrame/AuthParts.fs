module WebFrame.AuthParts

open WebFrame.Exceptions

type IAuthenticationProvider<'SP, 'T> =
    abstract member onAuthenticate: ( 'SP -> 'T option ) with get, set

type AuthenticationProvider<'SP, 'T> () =
    interface IAuthenticationProvider<'SP, 'T> with
        member val onAuthenticate = raise ( MissingAuthenticationException () ) with get, set

type UserManager<'SP, 'T> ( provider: Lazy<IAuthenticationProvider<'SP, 'T>>, serv: 'SP ) =
    member this.Required () = this.Optional () |> Option.defaultWith ( fun () -> raise ( NotAuthneticatedException () ) )
    member this.Optional () = provider.Value.onAuthenticate serv

type Auth<'SP, 'T> ( authProvider: Lazy<IAuthenticationProvider<'SP, 'T>>, serv: 'SP ) =
    member val User = UserManager ( authProvider, serv )
