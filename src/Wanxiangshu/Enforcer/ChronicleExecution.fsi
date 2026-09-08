namespace Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
type ChronicleExecution =
    | Completed of string
    | NoLiveCycle

module ChronicleExecution =

    [<Literal>]
    val EmptyTextError: string = "CHRONICLE_EMPTY_ENFORCER_061"

    [<Literal>]
    val NoLiveCycleError: string = "CHRONICLE_NO_LIVE_CYCLE"

    val tryCanonicalText: rawText: string -> Result<string, string>

    val decide: bool -> string -> ChronicleExecution
