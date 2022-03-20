module WebFrame.Exceptions

open System

// Base exception classes
type InputException ( msg: string ) = inherit Exception ( msg )
type AuthException ( msg: string ) = inherit Exception ( msg )
type ServerException ( msg: string ) = inherit Exception ( msg )

// Exceptions raised in response to incorrect user input
// Should result in 400 responses    
type MissingRequiredRouteParameterException ( parameterName: string ) =
    inherit InputException $"Could not retrieve a required parameter named '%s{parameterName}' from the route values."
    
type MissingRequiredQueryParameterException ( parameterName: string ) =
    inherit InputException $"Could not retrieve a required parameter named '%s{parameterName}' from the query values."
    
type MissingRequiredHeaderException ( headerName: string ) =
    inherit InputException $"Could not retrieve a required header named '%s{headerName}' from the request."
    
type MissingRequiredCookieException ( cookieName: string ) =
    inherit InputException $"Could not retrieve a required cookie named '%s{cookieName}' from the request."
    
type MissingRequiredFormFieldException ( fieldName: string ) =
    inherit InputException $"Could not retrieve a required form field named '%s{fieldName}' from the request."
    
type MissingRequiredFormFileException ( fileName: string ) =
    inherit InputException $"Could not retrieve a required form file named '%s{fileName}' from the request."
    
type MissingRequiredFormException () =
    inherit InputException $"Could not retrieve a form body from the request."
    
type MissingRequiredJsonFieldException ( fieldName: string ) =
    inherit InputException $"Could not retrieve a required json field named '%s{fieldName}' from the request."
    
type MissingRequiredJsonException () =
    inherit InputException $"Could not retrieve an expected json body from the request."

// Unspecified input validation exception
type GenericInputException ( msg: string ) = inherit InputException ( msg )

type NotAuthneticatedException () = inherit AuthException $"The user was not authenticated"

// Exceptions raised in response to an incorrect app setup
// Generally, should result in an app breaking on startup
// However, it is not always possible
type MissingRequiredDependencyException ( dependencyName: string ) =
    inherit ServerException $"Could not retrieve an object of type '%s{dependencyName}' from ASP.NET Core's dependency container."
    
type MissingAuthenticationException () =
    inherit ServerException "Could not find authentication methods"
    
type MissingRequiredConfigException ( configName: string ) =
    inherit ServerException $"Could not read a property named '%s{configName}' from ASP.NET Core's configuration container."
    
type DuplicateRouteException ( routePattern: string ) =
    inherit ServerException $"Could not register a route '{routePattern}'. The route is already registered."
    
type DuplicateModuleException ( name: string ) =
    inherit ServerException $"Could not register a module '{name}'. The module with such a name is already registered."
    
type HostNotReadyException () =
    inherit ServerException $"Cannot access the IHost before it is built."
    
type MissingTemplateException ( path: string ) =
    inherit ServerException $"The template at \"{path}\" was not found."

/// Unspecified server setup exception
type GenericServerException ( msg: string ) = inherit ServerException ( msg )
