namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// P0-RECOVERY-JOIN-001: Aborted is observation, not durable finality.
/// Join consumes only JoinableCompletion; HandleCompleted writers hold that proof.
module ChildRecovery =

    /// Proven business terminal of a child handle. No Aborted case.
    [<RequireQualifiedAccess>]
    type ChildFinality =
        /// Durable join payload body already encoded for the completion blob.
        | Succeeded of body: string
        /// Proven business failure (not transport abort-only).
        | Failed of body: string
        | Abandoned of HandleAbandonReason

    /// Evidence that a terminal business outcome exists. Private construction:
    /// only Domain pure functions may mint. No Aborted-only path.
    type TerminalEvidence =
        private
        | ProvenCompleted of
            agentId: string *
            handle: HandleId *
            childSession: SessionId *
            body: string
        | ProvenFailed of
            agentId: string *
            handle: HandleId *
            childSession: SessionId *
            body: string

    module TerminalEvidence =
        let completed
            (agentId: string)
            (handle: HandleId)
            (childSession: SessionId)
            (body: string)
            : TerminalEvidence =
            ProvenCompleted(agentId, handle, childSession, body)

        let failed
            (agentId: string)
            (handle: HandleId)
            (childSession: SessionId)
            (body: string)
            : TerminalEvidence =
            ProvenFailed(agentId, handle, childSession, body)

    /// Single-assignment completion cell proof. Private: only proveTerminal /
    /// tryFromProvenTerminal may construct. Carries kind + body so
    /// HandleController writes blob + HandleCompleted without external kind/body.
    type JoinableCompletion =
        private
            { AgentId: string
              Handle: HandleId
              ChildSession: SessionId
              Finality: ChildFinality
              Kind: HandleCompletionKind
              Body: string option }

    module JoinableCompletion =
        let agentId (c: JoinableCompletion) = c.AgentId
        let handle (c: JoinableCompletion) = c.Handle
        let childSession (c: JoinableCompletion) = c.ChildSession
        let finality (c: JoinableCompletion) = c.Finality
        let kind (c: JoinableCompletion) = c.Kind
        let body (c: JoinableCompletion) = c.Body

        /// Rebuild joinable completion already sealed as durable CompletedAwaitingJoin.
        /// Body may be absent for Cancelled / pre-blob journal lines.
        let tryFromDurableCompleted
            (agentId: string)
            (handle: HandleId)
            (childSession: SessionId)
            (kind: HandleCompletionKind)
            (body: string option)
            : Result<JoinableCompletion, string> =
            match kind, body with
            | HandleCompletionKind.Terminal, Some content when content <> "" ->
                Ok
                    { AgentId = agentId
                      Handle = handle
                      ChildSession = childSession
                      Finality = ChildFinality.Succeeded content
                      Kind = kind
                      Body = Some content }
            | HandleCompletionKind.SendFailure, Some content when content <> "" ->
                // Durable SendFailure is a proven business failure cell, not abort-only.
                Ok
                    { AgentId = agentId
                      Handle = handle
                      ChildSession = childSession
                      Finality = ChildFinality.Failed content
                      Kind = kind
                      Body = Some content }
            | HandleCompletionKind.Cancelled, _ ->
                Error "durable Cancelled is not joinable under P0-RECOVERY-JOIN-001"
            | HandleCompletionKind.Terminal, _
            | HandleCompletionKind.SendFailure, _ ->
                Error "durable completion missing body"

        /// Interpreter path: proven terminal only. No fromAborted.
        let tryFromProvenTerminal (evidence: TerminalEvidence) : Result<JoinableCompletion, string> =
            match evidence with
            | ProvenCompleted(agentId, handle, childSession, body) when body <> "" ->
                Ok
                    { AgentId = agentId
                      Handle = handle
                      ChildSession = childSession
                      Finality = ChildFinality.Succeeded body
                      Kind = HandleCompletionKind.Terminal
                      Body = Some body }
            | ProvenFailed(agentId, handle, childSession, body) when body <> "" ->
                Ok
                    { AgentId = agentId
                      Handle = handle
                      ChildSession = childSession
                      Finality = ChildFinality.Failed body
                      Kind = HandleCompletionKind.SendFailure
                      Body = Some body }
            | ProvenCompleted _
            | ProvenFailed _ -> Error "proven terminal body must be non-empty"

    /// Domain view of durable handle lifecycle (no Journal types).
    [<RequireQualifiedAccess>]
    type DurableHandleEvidence =
        | Unknown
        | Active
        | CompletedAwaitingJoin of JoinableCompletion
        | Abandoned of HandleAbandonReason
        | Retired

    /// Snapshot terminal evidence. Aborted-only never appears as SnapshotTerminal.
    [<RequireQualifiedAccess>]
    type ChildSnapshotEvidence =
        | Missing
        | Active
        | Terminal of TerminalEvidence
        | Unreadable of reason: string

    /// Host observations. AbortedObserved is never finality.
    /// RecoveryInFlight avoids dsl-ownership behaviour-bool tokens (*Pending/*Running).
    [<RequireQualifiedAccess>]
    type HostObservation =
        | AbortedObserved of reason: string
        | ParentCancelled
        | DeadlineExceeded
        | HostSessionGone
        | RecoveryInFlight
        | SessionActive

    [<RequireQualifiedAccess>]
    type ChildResolution =
        | Joinable of JoinableCompletion
        | Abandon of HandleAbandonReason
        | AwaitingEvidence
        | RunningAgain
        | Blocked of reason: string

    /// Pure resolution order (P0-RECOVERY-JOIN-001 §四):
    /// durable Abandoned → Abandon
    /// durable CompletedAwaitingJoin → Joinable (rebuild)
    /// snapshot legal terminal → Joinable
    /// session active / restore in flight → AwaitingEvidence | RunningAgain
    /// only AbortedObserved → AwaitingEvidence (never Joinable)
    /// ParentCancelled / DeadlineExceeded / HostSessionGone → Abandon
    /// conflict → Blocked
    let resolveChild
        (durable: DurableHandleEvidence)
        (snapshot: ChildSnapshotEvidence)
        (observations: HostObservation list)
        : ChildResolution =
        match durable with
        | DurableHandleEvidence.Abandoned reason -> ChildResolution.Abandon reason
        | DurableHandleEvidence.CompletedAwaitingJoin proof -> ChildResolution.Joinable proof
        | DurableHandleEvidence.Retired -> ChildResolution.Blocked "handle already retired"
        | DurableHandleEvidence.Unknown
        | DurableHandleEvidence.Active ->
            match snapshot with
            | ChildSnapshotEvidence.Terminal evidence ->
                match JoinableCompletion.tryFromProvenTerminal evidence with
                | Ok proof -> ChildResolution.Joinable proof
                | Error reason -> ChildResolution.Blocked reason
            | ChildSnapshotEvidence.Unreadable reason -> ChildResolution.Blocked reason
            | ChildSnapshotEvidence.Missing
            | ChildSnapshotEvidence.Active ->
                let hasAbortOnly =
                    observations
                    |> List.exists (function
                        | HostObservation.AbortedObserved _ -> true
                        | _ -> false)

                let abandonReason =
                    observations
                    |> List.tryPick (function
                        | HostObservation.ParentCancelled -> Some HandleAbandonReason.ParentCancelled
                        | HostObservation.DeadlineExceeded -> Some HandleAbandonReason.DeadlineExceeded
                        | HostObservation.HostSessionGone -> Some HandleAbandonReason.HostSessionGone
                        | _ -> None)

                let restoreInFlight =
                    observations
                    |> List.exists (function
                        | HostObservation.RecoveryInFlight -> true
                        | _ -> false)

                let sessionActive =
                    observations
                    |> List.exists (function
                        | HostObservation.SessionActive -> true
                        | _ -> false)

                match abandonReason with
                | Some reason -> ChildResolution.Abandon reason
                | None when restoreInFlight -> ChildResolution.AwaitingEvidence
                | None when sessionActive -> ChildResolution.RunningAgain
                | None when hasAbortOnly -> ChildResolution.AwaitingEvidence
                | None -> ChildResolution.AwaitingEvidence

    /// Closed AST (FLOW-002). Not persisted; not a coroutine.
    type ChildRecoveryProgram<'result> =
        | Return of 'result
        | ReadDurableHandle of HandleId * (DurableHandleEvidence -> ChildRecoveryProgram<'result>)
        | ReadChildSnapshot of SessionId * (ChildSnapshotEvidence -> ChildRecoveryProgram<'result>)
        | ObserveHostSignals of SessionId * (HostObservation list -> ChildRecoveryProgram<'result>)
        | ProveTerminal of TerminalEvidence * (JoinableCompletion -> ChildRecoveryProgram<'result>)
        | CommitCompletion of JoinableCompletion * (unit -> ChildRecoveryProgram<'result>)
        | CommitAbandonment of
            HandleId *
            HandleAbandonReason *
            (unit -> ChildRecoveryProgram<'result>)
        | KeepWaiting of reason: string * (unit -> ChildRecoveryProgram<'result>)
        | Block of reason: string

    type ChildRecoveryBuilder() =
        member _.Return(value: 'result) : ChildRecoveryProgram<'result> = Return value
        member _.ReturnFrom(program: ChildRecoveryProgram<'result>) = program
        member _.Zero() : ChildRecoveryProgram<unit> = Return()

        member _.Delay(f: unit -> ChildRecoveryProgram<'result>) : ChildRecoveryProgram<'result> = f ()

        member _.Bind
            (program: ChildRecoveryProgram<'a>, cont: 'a -> ChildRecoveryProgram<'b>)
            : ChildRecoveryProgram<'b> =
            let rec bind current =
                match current with
                | Return value -> cont value
                | ReadDurableHandle(handle, next) ->
                    ReadDurableHandle(handle, (fun evidence -> bind (next evidence)))
                | ReadChildSnapshot(sessionId, next) ->
                    ReadChildSnapshot(sessionId, (fun evidence -> bind (next evidence)))
                | ObserveHostSignals(sessionId, next) ->
                    ObserveHostSignals(sessionId, (fun signals -> bind (next signals)))
                | ProveTerminal(evidence, next) -> ProveTerminal(evidence, (fun proof -> bind (next proof)))
                | CommitCompletion(proof, next) -> CommitCompletion(proof, (fun () -> bind (next ())))
                | CommitAbandonment(handle, reason, next) ->
                    CommitAbandonment(handle, reason, (fun () -> bind (next ())))
                | KeepWaiting(reason, next) -> KeepWaiting(reason, (fun () -> bind (next ())))
                | Block reason -> Block reason

            bind program

    let childRecovery = ChildRecoveryBuilder()

    let readDurableHandle (handle: HandleId) : ChildRecoveryProgram<DurableHandleEvidence> =
        ReadDurableHandle(handle, Return)

    let readChildSnapshot (sessionId: SessionId) : ChildRecoveryProgram<ChildSnapshotEvidence> =
        ReadChildSnapshot(sessionId, Return)

    let observeHostSignals (sessionId: SessionId) : ChildRecoveryProgram<HostObservation list> =
        ObserveHostSignals(sessionId, Return)

    /// ProveTerminal instruction. Interpreter (or pure tryFromProvenTerminal) mints
    /// JoinableCompletion; Aborted cannot enter TerminalEvidence.
    let proveTerminal (evidence: TerminalEvidence) : ChildRecoveryProgram<JoinableCompletion> =
        ProveTerminal(evidence, Return)

    let commitCompletion (proof: JoinableCompletion) : ChildRecoveryProgram<unit> =
        CommitCompletion(proof, Return)

    let commitAbandonment (handle: HandleId) (reason: HandleAbandonReason) : ChildRecoveryProgram<unit> =
        CommitAbandonment(handle, reason, Return)

    let keepWaiting (reason: string) : ChildRecoveryProgram<unit> = KeepWaiting(reason, Return)

    let block (reason: string) : ChildRecoveryProgram<'result> = Block reason

    /// Recover one child: read durable → snapshot → observations → resolve → commit.
    let recoverChild
        (handle: HandleId)
        (childSession: SessionId)
        : ChildRecoveryProgram<ChildResolution> =
        childRecovery {
            let! durable = readDurableHandle handle
            let! snapshot = readChildSnapshot childSession
            let! signals = observeHostSignals childSession
            let resolution = resolveChild durable snapshot signals

            match resolution with
            | ChildResolution.Joinable proof ->
                do! commitCompletion proof
                return ChildResolution.Joinable proof
            | ChildResolution.Abandon reason ->
                do! commitAbandonment handle reason
                return ChildResolution.Abandon reason
            | ChildResolution.AwaitingEvidence ->
                do! keepWaiting "awaiting terminal evidence"
                return ChildResolution.AwaitingEvidence
            | ChildResolution.RunningAgain ->
                do! keepWaiting "child running again"
                return ChildResolution.RunningAgain
            | ChildResolution.Blocked reason -> return! block reason
        }

    [<RequireQualifiedAccess>]
    type ChildRecoveryTrace =
        | ReadDurableHandle of HandleId
        | ReadChildSnapshot of SessionId
        | ObserveHostSignals of SessionId
        | ProveTerminal
        | CommitCompletion of agentId: string
        | CommitAbandonment of HandleId * HandleAbandonReason
        | KeepWaiting of reason: string
        | Blocked of reason: string

    /// Trace interpreter (FLOW-003): pure walk, no effects.
    let rec trace (program: ChildRecoveryProgram<'result>) : ChildRecoveryTrace list =
        match program with
        | Return _ -> []
        | ReadDurableHandle(handle, next) ->
            ChildRecoveryTrace.ReadDurableHandle handle
            :: trace (next DurableHandleEvidence.Unknown)
        | ReadChildSnapshot(sessionId, next) ->
            ChildRecoveryTrace.ReadChildSnapshot sessionId
            :: trace (next ChildSnapshotEvidence.Missing)
        | ObserveHostSignals(sessionId, next) ->
            ChildRecoveryTrace.ObserveHostSignals sessionId :: trace (next [])
        | ProveTerminal(evidence, next) ->
            match JoinableCompletion.tryFromProvenTerminal evidence with
            | Ok proof -> ChildRecoveryTrace.ProveTerminal :: trace (next proof)
            | Error reason -> [ ChildRecoveryTrace.Blocked reason ]
        | CommitCompletion(proof, next) ->
            ChildRecoveryTrace.CommitCompletion(JoinableCompletion.agentId proof)
            :: trace (next ())
        | CommitAbandonment(handle, reason, next) ->
            ChildRecoveryTrace.CommitAbandonment(handle, reason) :: trace (next ())
        | KeepWaiting(reason, next) -> ChildRecoveryTrace.KeepWaiting reason :: trace (next ())
        | Block reason -> [ ChildRecoveryTrace.Blocked reason ]
