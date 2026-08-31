namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ChatExecutionFactFold =

    let private invalidFactRejection reason : FoldRejection =
        { Fact = "ChatExecution"
          Reason = "InvalidFact: " + reason }

    let private invalidFact reason : Result<'value, FoldRejection> = Error(invalidFactRejection reason)

    let private unsupportedSchema schemaVersion : Result<'value, FoldRejection> =
        FoldRejection.reject "ChatExecution" (sprintf "UnsupportedSchema: expected 1, received %d" schemaVersion)

    let private replace execution projection =
        { projection with
            ByKey = Map.add execution.Key execution projection.ByKey }

    let private validateAcceptedKey (key: ChatExecutionKey) (evidence: AcceptedChatExecutionEvidence) =
        if evidence.SessionId <> key.SessionId then
            invalidFact "Accepted evidence session does not match its execution key"
        elif evidence.PhysicalUserMessageId <> key.PhysicalUserMessageId then
            invalidFact "Accepted evidence physical user message does not match its execution key"
        else
            Ok()

    let private admitAccepted key evidence projection =
        match ChatExecutionProjection.byKey key projection with
        | None ->
            Ok(
                replace
                    { Key = key
                      Evidence = evidence
                      ProviderStarted = None
                      TerminalEvidence = None
                      Lifecycle = ChatExecutionLifecycle.Accepted }
                    projection
            )
        | Some current when current.Evidence = evidence -> Ok projection
        | Some _ -> invalidFact "Accepted conflicts with the execution's established evidence"

    let private executionOrInvalid reason key projection =
        match ChatExecutionProjection.byKey key projection with
        | Some current -> Ok current
        | None -> invalidFact reason

    let private projectTransition projection transition =
        match transition with
        | Some updated -> replace updated projection
        | None -> projection

    let private advanceProviderStarted current evidence =
        match current.Lifecycle with
        | ChatExecutionLifecycle.Terminal _ -> invalidFact "ProviderStarted cannot follow a Terminal fact"
        | ChatExecutionLifecycle.Accepted when current.Evidence = evidence.Accepted ->
            Ok(
                Some
                    { current with
                        ProviderStarted = Some evidence
                        Lifecycle = ChatExecutionLifecycle.ProviderStarted }
            )
        | ChatExecutionLifecycle.ProviderStarted when current.ProviderStarted = Some evidence -> Ok None
        | ChatExecutionLifecycle.Accepted
        | ChatExecutionLifecycle.ProviderStarted ->
            invalidFact "ProviderStarted evidence does not match Accepted evidence or established provider run"

    let private settleTerminal current evidence disposition =
        match current.Lifecycle, evidence with
        | ChatExecutionLifecycle.Accepted, ChatExecutionTerminalEvidence.PreProvider accepted when
            current.Evidence = accepted
            && disposition <> ChatExecutionTerminalDisposition.Completed
            ->
            Ok(
                Some
                    { current with
                        TerminalEvidence = Some evidence
                        Lifecycle = ChatExecutionLifecycle.Terminal disposition }
            )
        | ChatExecutionLifecycle.Accepted, ChatExecutionTerminalEvidence.PreProvider _ ->
            invalidFact "Pre-provider Terminal evidence mismatches Accepted or attempts Completed"
        | ChatExecutionLifecycle.Accepted, ChatExecutionTerminalEvidence.AfterProviderStart _ ->
            invalidFact "After-provider Terminal has no preceding ProviderStarted fact"
        | ChatExecutionLifecycle.ProviderStarted, ChatExecutionTerminalEvidence.AfterProviderStart started when
            current.ProviderStarted = Some started
            ->
            Ok(
                Some
                    { current with
                        TerminalEvidence = Some evidence
                        Lifecycle = ChatExecutionLifecycle.Terminal disposition }
            )
        | ChatExecutionLifecycle.ProviderStarted, ChatExecutionTerminalEvidence.AfterProviderStart _ ->
            invalidFact "Terminal evidence does not match ProviderStarted evidence"
        | ChatExecutionLifecycle.ProviderStarted, ChatExecutionTerminalEvidence.PreProvider _ ->
            invalidFact "Pre-provider Terminal cannot follow ProviderStarted"
        | ChatExecutionLifecycle.Terminal established, _ when
            established = disposition && current.TerminalEvidence = Some evidence
            ->
            Ok None
        | ChatExecutionLifecycle.Terminal _, _ ->
            invalidFact "Terminal conflicts with the execution's established evidence or disposition"

    let private validateTerminalEvidence =
        function
        | ChatExecutionTerminalEvidence.PreProvider accepted -> AcceptedChatExecutionEvidence.validate accepted
        | ChatExecutionTerminalEvidence.AfterProviderStart started -> ProviderStartedEvidence.validate started

    let applyAccepted
        (key: ChatExecutionKey)
        (evidence: AcceptedChatExecutionEvidence)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        AcceptedChatExecutionEvidence.validate evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () -> validateAcceptedKey key evidence)
        |> Result.bind (fun () -> admitAccepted key evidence projection)

    let applyProviderStarted
        (key: ChatExecutionKey)
        (evidence: ProviderStartedEvidence)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        ProviderStartedEvidence.validate evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () -> executionOrInvalid "ProviderStarted has no preceding Accepted fact" key projection)
        |> Result.bind (fun current -> advanceProviderStarted current evidence)
        |> Result.map (projectTransition projection)

    let applyTerminal
        (key: ChatExecutionKey)
        (evidence: ChatExecutionTerminalEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        validateTerminalEvidence evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () -> executionOrInvalid "Terminal has no preceding Accepted fact" key projection)
        |> Result.bind (fun current -> settleTerminal current evidence disposition)
        |> Result.map (projectTransition projection)

    let fold
        (projection: ChatExecutionProjectionState)
        (fact: ChatExecutionFactCases)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        match fact with
        | Accepted payload when payload.SchemaVersion = 1 -> applyAccepted payload.Key payload.Evidence projection
        | ProviderStarted payload when payload.SchemaVersion = 1 ->
            applyProviderStarted payload.Key payload.Evidence projection
        | Terminal payload when payload.SchemaVersion = 1 ->
            applyTerminal payload.Key payload.Evidence payload.Disposition projection
        | Accepted payload -> unsupportedSchema payload.SchemaVersion
        | ProviderStarted payload -> unsupportedSchema payload.SchemaVersion
        | Terminal payload -> unsupportedSchema payload.SchemaVersion
