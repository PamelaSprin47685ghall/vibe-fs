namespace Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
type ChronicleExecution =
    | Completed of string
    | NoLiveCycle

[<RequireQualifiedAccess>]
module ChronicleExecution =

    [<Literal>]
    let EmptyTextError = "CHRONICLE_EMPTY_ENFORCER_061"

    [<Literal>]
    let NoLiveCycleError = "CHRONICLE_NO_LIVE_CYCLE"

    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    let decide hasLiveCycle completed =
        if hasLiveCycle then
            ChronicleExecution.Completed completed
        else
            ChronicleExecution.NoLiveCycle
