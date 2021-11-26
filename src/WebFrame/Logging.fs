module WebFrame.Logging

open Microsoft.Extensions.Logging

type Logger ( logFactory: ILoggerFactory, name: string ) =
    let logger = logFactory.CreateLogger name

    member _.Information ( message: string ) = logger.LogInformation message
    member _.Warning ( message: string ) = logger.LogWarning message
    member _.Error ( message: string ) = logger.LogError message
    member _.Critical ( message: string ) = logger.LogCritical message
    member _.Debug ( message: string ) = logger.LogDebug message
    member _.Trace ( message: string ) = logger.LogTrace message
