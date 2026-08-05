namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// P0-RECOVERY-JOIN-001 + clean-break: Aborted is observation, not durable finality.
/// Join consumes only JoinableCompletion from decoded v2 terminal; no raw JSON / kind+body.
module ChildRecovery =

    /// Non-empty list (local; no shared collection dependency).
    type NonEmpty<'a> = { Head: 'a; Tail: 'a list }

    module NonEmpty =
        let one (value: 'a) : NonEmpty<'a> = { Head = value; Tail = [] }

        let ofList (values: 'a list) : NonEmpty<'a> option =
            match values with
            | [] -> None
            | head :: tail -> Some { Head = head; Tail = tail }

        let toList (values: NonEmpty<'a>) : 'a list = values.Head :: values.Tail

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
        | ProvenCompleted of agentId: string * handle: HandleId * childSession: SessionId * body: string
        | ProvenFailed of agentId: string * handle: HandleId * childSession: SessionId * body: string

    module TerminalEvidence =
        let completed (agentId: string) (handle: HandleId) (childSession: SessionId) (body: string) : TerminalEvidence =
            ProvenCompleted(agentId, handle, childSession, body)

        let failed (agentId: string) (handle: HandleId) (childSession: SessionId) (body: string) : TerminalEvidence =
            ProvenFailed(agentId, handle, childSession, body)

    /// Completion blob v2 finality (schemaVersion=2). No aborted.
    type CompletedPayload =
        { RunId: string
          WorkRecord: string
          ChildSessionId: string
          AuthorityRoot: string
          ProviderRun: string
          Directory: string }

    type FailedPayload =
        { RunId: string
          Code: string
          Message: string
          ChildSessionId: string }

    /// Decoded current-schema agent completion. No Aborted case.
    type DurableAgentCompletionV2 =
        | CompletedV2 of CompletedPayload
        | FailedV2 of FailedPayload

    /// Legacy abort observation planted as if it were a terminal (status=aborted).
    type LegacyAbortPayload =
        { Status: string
          RunId: string
          Code: string
          Message: string
          ChildSessionId: string
          RawBody: string }

    [<RequireQualifiedAccess>]
    type CompletionDecodeError =
        | MissingSchemaVersion
        | UnknownSchemaVersion of version: string
        | UnknownFinality of finality: string
        | MissingFinality
        | InvalidJson of reason: string
        | IncompletePayload of reason: string

    /// First decode of a durable completion blob. JoinDrain branches on this.
    type DurableCompletionDecode =
        | Current of DurableAgentCompletionV2
        | LegacyFalseAbort of LegacyAbortPayload
        | Invalid of CompletionDecodeError

    /// Single-assignment completion cell proof. Private: only fromDecoded /
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

        /// Sole constructor from a decoded v2 terminal. Never touches raw JSON,
        /// HandleCompletionKind, or an arbitrary body string as proof.
        let fromDecoded
            (agentId: string)
            (handle: HandleId)
            (childSession: SessionId)
            (decoded: DurableAgentCompletionV2)
            (encodedBody: string)
            : JoinableCompletion =
            match decoded with
            | CompletedV2 _ ->
                { AgentId = agentId
                  Handle = handle
                  ChildSession = childSession
                  Finality = ChildFinality.Succeeded encodedBody
                  Kind = HandleCompletionKind.Terminal
                  Body = Some encodedBody }
            | FailedV2 _ ->
                { AgentId = agentId
                  Handle = handle
                  ChildSession = childSession
                  Finality = ChildFinality.Failed encodedBody
                  Kind = HandleCompletionKind.SendFailure
                  Body = Some encodedBody }

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

    /// Pure replacement handle id for a retired false-abort tombstone.
    /// recovery:<originalAgentId>:<bad-completion-digest> — repeat recovery never mints twice.
    module FalseTerminalMigration =
        let replacementAgentId (originalAgentId: string) (badDigest: BlobDigest) : string =
            sprintf "recovery:%s:%s" originalAgentId (BlobDigest.value badDigest)

        let replacementHandle (originalAgentId: string) (badDigest: BlobDigest) : HandleId =
            HandleId.Agent(AgentHandleId.create (replacementAgentId originalAgentId badDigest))

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

    /// Proven abandonment of a child handle (durable reason).
    type ProvenAbandonment =
        { Handle: HandleId
          Reason: HandleAbandonReason }

    /// Child still live after recovery work finished (not incomplete).
    type ActiveChildReceipt =
        { Handle: HandleId
          ChildSession: SessionId }

    /// Why recovery cannot finish yet (not a terminal, not blocked).
    type RecoveryDependency =
        | AwaitingTerminalEvidence of HandleId * SessionId
        | HostRestoreInFlight of HandleId * SessionId

    [<RequireQualifiedAccess>]
    type ChildRecoveryBlock =
        | Reason of string
        | SnapshotUnreadable of SessionId * reason: string

    /// Post-recovery child outcome (GREEN-4). RecoveredActive ≠ RecoveryIncomplete.
    /// Permit may issue only for RecoveredActive | RecoveredTerminal | RecoveredAbandoned.
    [<RequireQualifiedAccess>]
    type ChildRecoveryResult =
        | RecoveredActive of ActiveChildReceipt
        | RecoveredTerminal of JoinableCompletion
        | RecoveredAbandoned of ProvenAbandonment
        | RecoveryIncomplete of RecoveryDependency
        | RecoveryBlocked of NonEmpty<ChildRecoveryBlock>

    /// Pure resolution before commit. Maps 1:1 to ChildRecoveryResult after effects.
    [<RequireQualifiedAccess>]
    type ChildResolution =
        | RecoveredTerminal of JoinableCompletion
        | RecoveredAbandoned of HandleAbandonReason
        | RecoveryIncomplete
        | RecoveredActive
        | RecoveryBlocked of reason: string

    /// Pure resolution order (P0-RECOVERY-JOIN-001 §四 + GREEN-4 split):
    /// durable Abandoned → RecoveredAbandoned
    /// durable CompletedAwaitingJoin → RecoveredTerminal
    /// snapshot legal terminal → RecoveredTerminal
    /// snapshot Unreadable (true read error) → RecoveryIncomplete (wait; no permit; not hard block)
    /// session active → RecoveredActive (recovery work done; child continues)
    /// restore in flight / abort-only / unknown → RecoveryIncomplete (must not issue permit)
    /// ParentCancelled / DeadlineExceeded / HostSessionGone → RecoveredAbandoned
    /// conflict / retired → RecoveryBlocked
    let resolveChild
        (durable: DurableHandleEvidence)
        (snapshot: ChildSnapshotEvidence)
        (observations: HostObservation list)
        : ChildResolution =
        match durable with
        | DurableHandleEvidence.Abandoned reason -> ChildResolution.RecoveredAbandoned reason
        | DurableHandleEvidence.CompletedAwaitingJoin proof -> ChildResolution.RecoveredTerminal proof
        | DurableHandleEvidence.Retired -> ChildResolution.RecoveryBlocked "handle already retired"
        | DurableHandleEvidence.Unknown
        | DurableHandleEvidence.Active ->
            match snapshot with
            | ChildSnapshotEvidence.Terminal evidence ->
                match JoinableCompletion.tryFromProvenTerminal evidence with
                | Ok proof -> ChildResolution.RecoveredTerminal proof
                | Error reason -> ChildResolution.RecoveryBlocked reason
            // True GetMessages / decode failure: incomplete (wait), not definitive block.
            // Family permit must not issue; join / onTurn consumers treat as wait-not-hard-error.
            | ChildSnapshotEvidence.Unreadable _ -> ChildResolution.RecoveryIncomplete
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
                | Some reason -> ChildResolution.RecoveredAbandoned reason
                // Session active = child continues; recovery step for this handle is done.
                | None when sessionActive -> ChildResolution.RecoveredActive
                // Restore still running or only abort noise → incomplete (no permit).
                | None when restoreInFlight -> ChildResolution.RecoveryIncomplete
                | None when hasAbortOnly -> ChildResolution.RecoveryIncomplete
                | None -> ChildResolution.RecoveryIncomplete

    /// Closed AST (FLOW-002). Not persisted; not a coroutine.
    type ChildRecoveryProgram<'result> =
        | Return of 'result
        | ReadDurableHandle of HandleId * (DurableHandleEvidence -> ChildRecoveryProgram<'result>)
        | ReadChildSnapshot of SessionId * (ChildSnapshotEvidence -> ChildRecoveryProgram<'result>)
        | ObserveHostSignals of SessionId * (HostObservation list -> ChildRecoveryProgram<'result>)
        | ProveTerminal of TerminalEvidence * (JoinableCompletion -> ChildRecoveryProgram<'result>)
        | CommitCompletion of JoinableCompletion * (unit -> ChildRecoveryProgram<'result>)
        | CommitAbandonment of HandleId * HandleAbandonReason * (unit -> ChildRecoveryProgram<'result>)
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
                | ReadDurableHandle(handle, next) -> ReadDurableHandle(handle, (fun evidence -> bind (next evidence)))
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

    let commitCompletion (proof: JoinableCompletion) : ChildRecoveryProgram<unit> = CommitCompletion(proof, Return)

    let commitAbandonment (handle: HandleId) (reason: HandleAbandonReason) : ChildRecoveryProgram<unit> =
        CommitAbandonment(handle, reason, Return)

    let keepWaiting (reason: string) : ChildRecoveryProgram<unit> = KeepWaiting(reason, Return)

    let block (reason: string) : ChildRecoveryProgram<'result> = Block reason

    /// Map pure resolution + handle identity into ChildRecoveryResult (no effects).
    let toChildRecoveryResult
        (handle: HandleId)
        (childSession: SessionId)
        (resolution: ChildResolution)
        : ChildRecoveryResult =
        match resolution with
        | ChildResolution.RecoveredTerminal proof -> ChildRecoveryResult.RecoveredTerminal proof
        | ChildResolution.RecoveredAbandoned reason ->
            ChildRecoveryResult.RecoveredAbandoned { Handle = handle; Reason = reason }
        | ChildResolution.RecoveredActive ->
            ChildRecoveryResult.RecoveredActive
                { Handle = handle
                  ChildSession = childSession }
        | ChildResolution.RecoveryIncomplete ->
            ChildRecoveryResult.RecoveryIncomplete(RecoveryDependency.AwaitingTerminalEvidence(handle, childSession))
        | ChildResolution.RecoveryBlocked reason ->
            ChildRecoveryResult.RecoveryBlocked(NonEmpty.one (ChildRecoveryBlock.Reason reason))

    /// Recover one child: read durable → snapshot → observations → resolve → commit.
    /// Returns ChildRecoveryResult: RecoveredActive ≠ RecoveryIncomplete (GREEN-4).
    let recoverChild (handle: HandleId) (childSession: SessionId) : ChildRecoveryProgram<ChildRecoveryResult> =
        childRecovery {
            let! durable = readDurableHandle handle
            let! snapshot = readChildSnapshot childSession
            let! signals = observeHostSignals childSession
            let resolution = resolveChild durable snapshot signals

            match resolution with
            | ChildResolution.RecoveredTerminal proof ->
                do! commitCompletion proof
                return ChildRecoveryResult.RecoveredTerminal proof
            | ChildResolution.RecoveredAbandoned reason ->
                do! commitAbandonment handle reason

                return ChildRecoveryResult.RecoveredAbandoned { Handle = handle; Reason = reason }
            | ChildResolution.RecoveredActive ->
                // Recovery finished; child keeps running. No keepWaiting.
                return
                    ChildRecoveryResult.RecoveredActive
                        { Handle = handle
                          ChildSession = childSession }
            | ChildResolution.RecoveryIncomplete ->
                do! keepWaiting "awaiting terminal evidence"

                return
                    ChildRecoveryResult.RecoveryIncomplete(
                        RecoveryDependency.AwaitingTerminalEvidence(handle, childSession)
                    )
            | ChildResolution.RecoveryBlocked reason -> return! block reason
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
        | ObserveHostSignals(sessionId, next) -> ChildRecoveryTrace.ObserveHostSignals sessionId :: trace (next [])
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

    /// §九 join-recovery timeline events (pure observation log, not a program counter).
    /// agentId is the AgentHandle key string (same as JoinableCompletion.agentId).
    [<RequireQualifiedAccess>]
    type JoinRecoveryTrace =
        | RawAbortObserved of SessionId
        | ChildRecoveryStarted of SessionId
        | TerminalProofIssued of agentId: string
        | HandleCompletionCommitted of agentId: string
        | JoinReturned of agentId: string * ChildFinality

    /// Pure invariant over a join-recovery timeline:
    /// ∀ JoinReturned(agent) ∃ TerminalProofIssued(agent) before HandleCompletionCommitted(agent)
    /// before that JoinReturned; RawAbortObserved never adjacent to HandleCompletionCommitted
    /// or JoinReturned (abort is observation, not a completion step).
    let joinReturnedImpliesProofBeforeCommit (events: JoinRecoveryTrace list) : bool =
        let adjacentForbidden a b =
            match a, b with
            | JoinRecoveryTrace.RawAbortObserved _, JoinRecoveryTrace.HandleCompletionCommitted _
            | JoinRecoveryTrace.RawAbortObserved _, JoinRecoveryTrace.JoinReturned _
            | JoinRecoveryTrace.HandleCompletionCommitted _, JoinRecoveryTrace.RawAbortObserved _
            | JoinRecoveryTrace.JoinReturned _, JoinRecoveryTrace.RawAbortObserved _ -> true
            | _ -> false

        let rec adjacencyOk =
            function
            | []
            | [ _ ] -> true
            | a :: b :: rest ->
                if adjacentForbidden a b then
                    false
                else
                    adjacencyOk (b :: rest)

        let orderForJoinReturns =
            events
            |> List.indexed
            |> List.forall (fun (i, event) ->
                match event with
                | JoinRecoveryTrace.JoinReturned(agentId, _) ->
                    let before = List.take i events

                    let proofIdx =
                        before
                        |> List.tryFindIndex (function
                            | JoinRecoveryTrace.TerminalProofIssued id when id = agentId -> true
                            | _ -> false)

                    let commitIdx =
                        before
                        |> List.tryFindIndex (function
                            | JoinRecoveryTrace.HandleCompletionCommitted id when id = agentId -> true
                            | _ -> false)

                    match proofIdx, commitIdx with
                    | Some p, Some c -> p < c
                    | _ -> false
                | _ -> true)

        adjacencyOk events && orderForJoinReturns
