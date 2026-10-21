namespace Wanxiangshu.OpenCode

module ProviderFailurePresentation =
    [<RequireQualifiedAccess>]
    type Presentation =
        | Recovery of episodeId: string
        | Final of episodeId: string
        | Ignore

    val classify: decision: Wanxiangshu.Execution.Failure.ExecutionFailureDecision -> episodeId: string -> Presentation

    val classifyPolicyInput: value: obj -> episodeId: string -> obj
