namespace Wanxiangshu.Execution.Session.ChatExecution

[<RequireQualifiedAccess>]
type ChatExecutionLifecycle =
    | Accepted
    | ProviderStarted
    | Terminal of ChatExecutionTerminalDisposition

type ChatExecutionState =
    { Key: ChatExecutionKey
      Evidence: AcceptedChatExecutionEvidence
      ProviderStarted: ProviderStartedEvidence option
      TerminalEvidence: ChatExecutionTerminalEvidence option
      Lifecycle: ChatExecutionLifecycle }

type ChatExecutionProjectionState =
    { ByKey: Map<ChatExecutionKey, ChatExecutionState> }

[<RequireQualifiedAccess>]
module ChatExecutionProjection =

    let empty: ChatExecutionProjectionState = { ByKey = Map.empty }

    let current (projection: ChatExecutionProjectionState) : ChatExecutionState list =
        projection.ByKey |> Map.toList |> List.map snd

    let byKey (key: ChatExecutionKey) (projection: ChatExecutionProjectionState) : ChatExecutionState option =
        Map.tryFind key projection.ByKey

    let nonTerminal (projection: ChatExecutionProjectionState) : ChatExecutionState list =
        current projection
        |> List.filter (fun execution ->
            match execution.Lifecycle with
            | ChatExecutionLifecycle.Accepted
            | ChatExecutionLifecycle.ProviderStarted -> true
            | ChatExecutionLifecycle.Terminal _ -> false)
