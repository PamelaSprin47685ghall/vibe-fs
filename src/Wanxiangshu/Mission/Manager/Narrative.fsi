namespace Wanxiangshu.Mission.Manager

module ManagerNarrative =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val T1Revelation: string = "lifecycle/manager/t1-revelation"

    val wrapT1AcceptedResult: t1RevelationInstructions: string list -> todoWriteResult: string -> string
