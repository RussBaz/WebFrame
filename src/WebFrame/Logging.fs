module WebFrame.Logging

open Microsoft.Extensions.Logging

type Logger ( logFactory: Lazy<ILoggerFactory>, name: string ) =
    let logger = lazy ( logFactory.Value.CreateLogger name )

    member _.Information ( message: string ) = logger.Value.LogInformation message
    member _.Warning ( message: string ) = logger.Value.LogWarning message
    member _.Error ( message: string ) = logger.Value.LogError message
    member _.Critical ( message: string ) = logger.Value.LogCritical message
    member _.Debug ( message: string ) = logger.Value.LogDebug message
    member _.Trace ( message: string ) = logger.Value.LogTrace message
