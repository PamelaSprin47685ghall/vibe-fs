namespace Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
type ChronicleExecution =
    | Completed of string
    | NoLiveCycle

[<RequireQualifiedAccess>]
module ChronicleExecution =

    let decide hasLiveCycle completed =
        if hasLiveCycle then
            ChronicleExecution.Completed completed
        else
            ChronicleExecution.NoLiveCycle
