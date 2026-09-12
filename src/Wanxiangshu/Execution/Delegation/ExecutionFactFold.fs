namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module ExecutionFactFold =

    let private canonicalByname byname targetAgent =
        if System.String.IsNullOrWhiteSpace byname then
            targetAgent
        else
            byname

    /// A handle line whose transition may be refused. Replaying a completion,
    /// abandon or retirement is absorbed (durable terminals make those
    /// idempotent); the three impossible transitions fail closed with the fact
    /// label the durable report names.
    let private handleOutcome
        (factName: string)
        (parentSessionId: SessionId)
        (handle: HandleId)
        (priorState: DelegationSessionState option)
        (terminalChild: SessionId option)
        (result: Result<AgentLinkageProjection, HandleTransitionRejection>)
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        let priorHandles = priorState |> Option.bind (fun state -> state.Handles)

        let indexChange (handles: AgentLinkageProjection) =
            HandleProjection.tryFind handle handles
            |> Option.map (fun record -> IndexChildHandle(record.ChildSessionId, record))

        let terminalChanges =
            terminalChild |> Option.map TerminatedChildHandle |> Option.toList

        match result with
        | Ok updatedHandles ->
            let baseState = Option.defaultValue DelegationSessionState.empty priorState

            Ok
                [ ReplaceSessionState(
                      parentSessionId,
                      { baseState with
                          Handles = Some updatedHandles }
                  )
                  yield! Option.toList (indexChange updatedHandles)
                  yield! terminalChanges ]
        | Error AlreadyCompleted
        | Error AlreadyAbandoned
        | Error HandleIsRetired ->
            Ok
                [ yield! priorHandles |> Option.bind indexChange |> Option.toList
                  yield! terminalChanges ]
        | Error HandleIdentityConflict -> Error(HandleBindingConflict factName)
        | Error UnknownHandle -> Error(HandleNeverLinked factName)
        | Error NotCompleted -> Error(HandleCompletionMissing factName)

    let fold
        (sessionState: SessionId -> DelegationSessionState option)
        (fact: ExecutionFactCases)
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        // ── execution handles ───────────────────────────────────────────────
        match fact with
        | ExecutionFactCases.HandleLinked payload ->
            let byname = canonicalByname payload.Byname payload.TargetAgent
            let priorState = sessionState payload.ParentSessionId

            HandleProjection.linkNamed
                payload.Handle
                payload.ChildSessionId
                payload.TargetAgent
                byname
                payload.CanonicalRole
                payload.Ownership
                (priorState
                 |> Option.bind (fun s -> s.Handles)
                 |> Option.defaultValue HandleProjection.empty)
            |> handleOutcome "HandleLinked" payload.ParentSessionId payload.Handle priorState None

        | ExecutionFactCases.HandleCompleted payload ->
            let priorState = sessionState payload.ParentSessionId

            let terminalChild =
                priorState
                |> Option.bind (fun s -> s.Handles)
                |> Option.bind (HandleProjection.tryFind payload.Handle)
                |> Option.map (fun record -> record.ChildSessionId)

            HandleProjection.complete
                payload.Handle
                { Kind = payload.Kind
                  CompletionRef = payload.CompletionRef
                  CompletionDigest = payload.CompletionDigest }
                (priorState
                 |> Option.bind (fun s -> s.Handles)
                 |> Option.defaultValue HandleProjection.empty)
            |> handleOutcome "HandleCompleted" payload.ParentSessionId payload.Handle priorState terminalChild

        | ExecutionFactCases.HandleRetired payload ->
            let priorState = sessionState payload.ParentSessionId

            let terminalChild =
                priorState
                |> Option.bind (fun s -> s.Handles)
                |> Option.bind (HandleProjection.tryFind payload.Handle)
                |> Option.map (fun record -> record.ChildSessionId)

            HandleProjection.retire
                payload.Handle
                (priorState
                 |> Option.bind (fun s -> s.Handles)
                 |> Option.defaultValue HandleProjection.empty)
            |> handleOutcome "HandleRetired" payload.ParentSessionId payload.Handle priorState terminalChild

        | ExecutionFactCases.HandleAbandoned payload ->
            let priorState = sessionState payload.ParentSessionId

            HandleProjection.abandon
                payload.Handle
                payload.Reason
                (priorState
                 |> Option.bind (fun s -> s.Handles)
                 |> Option.defaultValue HandleProjection.empty)
            |> handleOutcome "HandleAbandoned" payload.ParentSessionId payload.Handle priorState None

        // Clean-break: false abort cell → Active only when ref/digest match.

        | ExecutionFactCases.HandleFalseCompletionRejected payload ->
            let priorState = sessionState payload.ParentSessionId

            HandleProjection.rejectFalseCompletion
                payload.Handle
                payload.ExpectedCompletionRef
                payload.ExpectedCompletionDigest
                (priorState
                 |> Option.bind (fun s -> s.Handles)
                 |> Option.defaultValue HandleProjection.empty)
            |> handleOutcome "HandleFalseCompletionRejected" payload.ParentSessionId payload.Handle priorState None

        // Clean-break: retired false terminal report. Projection keeps original
        // Retired tombstone; replacement is linked by a separate HandleLinked.

        | ExecutionFactCases.HandleFalseTerminalReported _ -> Ok []

        // Clean-break: parent correction notice. No handle lifecycle change.

        | ExecutionFactCases.ParentJoinCorrectionRequested _ -> Ok []

        // HostTurnObserved is a durable observation inbox fact. CompletionReactor
        // (later batch) consumes it; LinkageProjection has no fold effect yet.

        | ExecutionFactCases.HostTurnObserved _ -> Ok []
