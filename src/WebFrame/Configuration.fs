module WebFrame.Configuration

open System.Collections.Generic

open Microsoft.Extensions.Logging

type SystemDefaults = {
    SettingsPrefix: string
    LoggerPrefix: string
    LoggerGlobalName: string
    LoggerHostFactory: ILoggerFactory
    Args: string []
}

module SystemDefaults =
    let defaultLoggerFactory =
        LoggerFactory.Create ( fun logging ->
            logging.AddSimpleConsole () |> ignore
            () )
    let standard = {
        SettingsPrefix = "WebFrame"
        LoggerPrefix = "WebFrame"
        LoggerGlobalName = "Global"
        LoggerHostFactory = defaultLoggerFactory
        Args = [||]
    }
    let defaultWithArgs args = { standard with Args = args }
    let getLoggerNameForCategory ( name: string ) ( c: SystemDefaults ) =
        let prefix = if c.LoggerPrefix <> "" then $"{c.LoggerPrefix}." else ""
        $"{prefix}{name}"
    let getGlobalLoggerName ( c: SystemDefaults ) =
        c |> getLoggerNameForCategory c.LoggerGlobalName
    let getHostLogger ( c: SystemDefaults ) =
        let factory = c.LoggerHostFactory
        let loggerName = c |> getGlobalLoggerName
        factory.CreateLogger loggerName
    let toMap ( c: SystemDefaults ) =
        let prefix = if c.SettingsPrefix="" then "WebFrame" else c.SettingsPrefix
        Map [
            $"{prefix}:GlobalLogger:FullName", getGlobalLoggerName c
            $"{prefix}:GlobalLogger:Prefix", c.LoggerPrefix
        ]

type ConfigOverrides ( defaultConfig: SystemDefaults ) =
    let config = Dictionary<string, string> ()
    
    let get i =
        match config.TryGetValue i with
        | true, v -> v
        | _ -> ""
    let set i v = config.[ i ] <- v
    
    member _.Item with get i = get i and set i v = config.[ i ] <- v
    member _.ConnectionStrings with get i = get $"ConnectionStrings:{i}" and set i v = set $"ConnectionStrings:{i}" v
        
    member val Raw = config
