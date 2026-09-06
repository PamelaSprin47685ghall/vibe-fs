namespace Wanxiangshu.OpenCode

module ProviderFailurePresentation =
    [<RequireQualifiedAccess>]
    type Presentation =
        | Recovery of episodeId: string
        | Final of episodeId: string
        | Ignore

    val toPlain: presentation: Presentation -> obj

    val classify: decision: Wanxiangshu.Execution.Failure.ExecutionFailureDecision -> episodeId: string -> Presentation

    val classifyPlain: decision: Wanxiangshu.Execution.Failure.ExecutionFailureDecision -> episodeId: string -> obj
    val classifyPolicyInput: value: obj -> episodeId: string -> obj
