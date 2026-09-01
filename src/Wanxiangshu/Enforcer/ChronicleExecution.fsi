namespace Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
type ChronicleExecution =
    | Completed of string
    | NoLiveCycle


module ChronicleExecution =

    val decide: bool -> string -> ChronicleExecution
