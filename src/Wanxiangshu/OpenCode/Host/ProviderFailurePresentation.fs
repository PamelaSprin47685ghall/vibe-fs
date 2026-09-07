namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Failure

module ProviderFailurePresentation =
    [<RequireQualifiedAccess>]
    type Presentation =
        | Recovery of episodeId: string
        | Final of episodeId: string
        | Ignore

    let classify (decision: ExecutionFailureDecision) (episodeId: string) : Presentation =
        match decision.Resolution with
        | ExecutionFailureResolution.RetryFreshAttempt _ -> Presentation.Recovery episodeId
        | ExecutionFailureResolution.TerminalizeProviderStarted _ -> Presentation.Final episodeId
        | ExecutionFailureResolution.PreserveCurrentFact
        | ExecutionFailureResolution.AwaitAcceptanceReconciliation _
        | ExecutionFailureResolution.TerminalizeAcceptedPreProvider _ -> Presentation.Ignore

    let toPlain (presentation: Presentation) : obj =
        match presentation with
        | Presentation.Recovery episodeId ->
            box
                {| mode = "Recovery"
                   owner = "Wanxiangshu"
                   episodeId = episodeId
                   hasFinalPresentation = false |}
        | Presentation.Final episodeId ->
            box
                {| mode = "Final"
                   owner = "Wanxiangshu"
                   episodeId = episodeId
                   hasFinalPresentation = true |}
        | Presentation.Ignore ->
            box
                {| mode = "Ignore"
                   hasFinalPresentation = false |}

    let classifyPlain (decision: ExecutionFailureDecision) (episodeId: string) : obj =
        classify decision episodeId |> toPlain

    let classifyPolicyInput (value: obj) (episodeId: string) : obj =
        value
        |> Surface.inputOf
        |> ExecutionFailurePolicy.decide
        |> fun decision -> classifyPlain decision episodeId
