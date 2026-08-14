namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

/// Restart recovery for linked children. Terminal path: ChildRecoveryWorkflow
/// → ChildRecoveryResult → recordCompletion → PulseAgentHandle. Fail closed on proof.
/// Clean-break: legacy abort blobs never publish; retired false terminals migrate once.
/// GREEN-4: returns HandleFamilyRecovery (query result), never option/missing-port.
/// EXEC-024: agent mailbox is wake-only (PulseAgentHandle); no agent completion payload path.
module HostForkRestart =

    let private ports
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (role: Role)
        (agent: string)
        : ChildRecoveryWorkflow.Ports =
        { Journal = journal
          ParentId = parentId
          Snapshot = snapshot
          AgentId = agentId
          Handle = HandleController.agentHandle agentId
          ChildSession = childSessionId
          Role = role
          Agent = agent
          // After Restore+Bind the child is live in this process. SessionActive makes
          // resolveChild → RecoveredActive (recovery work done; child continues).
          // Do not inject RecoveryInFlight — that forced RecoveryIncomplete and blocked permit.
          Observations = [ HostObservation.SessionActive ]
          Pulse = Some(fun () -> runtime.PulseAgentHandle(AgentHandleId.create agentId))
          Clock = PtyTiming.nodeClockPort () }

    /// Active handle: Domain recoverChild via production interpreter.
    let recoverChild
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (role: Role)
        (agent: string)
        : Task<ChildRecoveryResult> =
        task {
            runtime.Restore(agentId, role, agent)
            runtime.BindChildSession(agentId, childSessionId)

            let p = ports runtime snapshot journal parentId agentId childSessionId role agent

            match! ChildRecoveryWorkflow.resolveAndCommit p with
            | Ok result ->
                match result with
                | ChildRecoveryResult.RecoveredTerminal _
                | ChildRecoveryResult.RecoveredAbandoned _
                | ChildRecoveryResult.RecoveredActive _ -> return result
                | ChildRecoveryResult.RecoveryIncomplete _ ->
                    runtime.MarkInterrupted(agentId, "host restart: awaiting terminal evidence")
                    return result
                | ChildRecoveryResult.RecoveryBlocked blocks ->
                    let reason =
                        Wanxiangshu.Domain.ChildRecovery.NonEmpty.toList blocks
                        |> List.map (function
                            | ChildRecoveryBlock.Reason r -> r
                            | ChildRecoveryBlock.SnapshotUnreadable(_, r) -> r)
                        |> String.concat "; "

                    runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)
                    return result
            | Error reason ->
                runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)

                return
                    ChildRecoveryResult.RecoveryBlocked(
                        Wanxiangshu.Domain.ChildRecovery.NonEmpty.one (ChildRecoveryBlock.Reason reason)
                    )
        }

    /// Clean-break: retired handle whose last cell was a legacy abort → replacement once.
    let private migrateRetiredIfFalseAbort
        (journal: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : Task<unit> =
        task {
            match record.LastCompletion with
            | None -> ()
            | Some cell ->
                match cell.CompletionRef, cell.CompletionDigest with
                | Some blobRef, Some blobDigest ->
                    match! journal.Writer.BlobWriter.Read blobRef with
                    | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                        match HandleCompletionCodec.decodeBody body with
                        | LegacyFalseAbort _ ->
                            let! _ = JoinDrain.tryMigrateRetiredFalseAbort journal parentId record blobRef blobDigest
                            ()
                        | _ -> ()
                    | _ -> ()
                | _ -> ()
        }

    let private recoveredHandle (agentHandle: AgentHandleId) (child: SessionId) (kind: string) : RecoveredHandle =
        { Handle = agentHandle
          ChildSession = child
          Kind = kind }

    let private fromChildResult
        (agentHandle: AgentHandleId)
        (child: SessionId)
        (result: ChildRecoveryResult)
        : Choice<RecoveredHandle, HandleRecoveryWait, HandleRecoveryBlock> =
        match result with
        | ChildRecoveryResult.RecoveredTerminal _ -> Choice1Of3(recoveredHandle agentHandle child "terminal")
        | ChildRecoveryResult.RecoveredAbandoned _ -> Choice1Of3(recoveredHandle agentHandle child "abandoned")
        | ChildRecoveryResult.RecoveredActive _ -> Choice1Of3(recoveredHandle agentHandle child "active")
        | ChildRecoveryResult.RecoveryIncomplete dep ->
            let reason =
                match dep with
                | RecoveryDependency.AwaitingTerminalEvidence _ -> "awaiting terminal evidence"
                | RecoveryDependency.HostRestoreInFlight _ -> "host restore in flight"

            Choice2Of3
                { Handle = agentHandle
                  ChildSession = child
                  Reason = reason }
        | ChildRecoveryResult.RecoveryBlocked blocks ->
            let reason =
                Wanxiangshu.Domain.ChildRecovery.NonEmpty.toList blocks
                |> List.map (function
                    | ChildRecoveryBlock.Reason r -> r
                    | ChildRecoveryBlock.SnapshotUnreadable(_, r) -> r)
                |> String.concat "; "

            Choice3Of3
                { Handle = agentHandle
                  ChildSession = child
                  Reason = reason }

    /// EXEC-009 restart recovery: rebuild parent join mailbox from durable handles.
    /// Returns HandleFamilyRecovery for SessionRecovery RestoreHandles (GREEN-4).
    let restoreLinkedChildren
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (childCreatedDir: string -> SessionId -> string option -> unit)
        (directoryOf: string -> string option)
        : Task<HandleFamilyRecovery> =
        task {
            // GLORY-002 / SURFACE-006: HostOwnedHidden handles (the Finality
            // Reviewer) belong to the Host-owned workflow, not to this parent.
            // Restoring one into the parent's runtime would resurrect it inside
            // the parent's list/join/guard. The Finality workflow re-adopts its
            // own sessions from its durable enlistment facts instead.
            let records =
                AgentProjection.tryFind parentId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind (fun session -> session.Handles)
                |> Option.map HandleProjection.linkedChildren
                |> Option.map (
                    List.filter (fun record ->
                        match record.Ownership with
                        | Fact.HandleOwnership.DurableParentHandle -> true
                        | Fact.HandleOwnership.HostOwnedHidden -> false)
                )
                |> Option.defaultValue []

            let recovered = ResizeArray<RecoveredHandle>()
            let waiting = ResizeArray<HandleRecoveryWait>()
            let blocked = ResizeArray<HandleRecoveryBlock>()

            for record in records do
                match record.Lifecycle, HandleId.tryAgent record.Handle with
                | HandleLifecycle.Abandoned _, Some agentHandle ->
                    recovered.Add(recoveredHandle agentHandle record.ChildSessionId "abandoned")
                | HandleLifecycle.Abandoned _, None
                | _, None -> ()
                | HandleLifecycle.Retired, Some agentHandle ->
                    do! migrateRetiredIfFalseAbort journal parentId record
                    recovered.Add(recoveredHandle agentHandle record.ChildSessionId "retired")
                | HandleLifecycle.CompletedAwaitingJoin _, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle
                    let role = AgentRoleIdentity.ofRole record.CanonicalRole

                    children.[agentId] <- record.ChildSessionId
                    childCreatedDir agentId record.ChildSessionId (directoryOf agentId)
                    runtime.Restore(agentId, role, record.TargetAgent)
                    runtime.BindChildSession(agentId, record.ChildSessionId)

                    match! HandleCompletionCodec.tryReadBody journal record with
                    | Ok(Some body, Some blobRef, Some blobDigest) ->
                        match HandleCompletionCodec.decodeBody body with
                        | Current decoded ->
                            ignore (
                                JoinableCompletion.fromDecoded agentId record.Handle record.ChildSessionId decoded body
                            )

                            // GREEN-5: wake only; JoinDrain re-reads Journal for payload.
                            runtime.PulseAgentHandle agentHandle
                            recovered.Add(recoveredHandle agentHandle record.ChildSessionId "terminal")
                        | LegacyFalseAbort _ ->
                            match!
                                AgentJournal.appendAgent
                                    (StreamId.Session parentId)
                                    None
                                    (ExecutionFact.HandleFalseCompletionRejected
                                        {| ParentSessionId = parentId
                                           Handle = record.Handle
                                           ExpectedCompletionRef = blobRef
                                           ExpectedCompletionDigest = blobDigest
                                           Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                                    journal
                            with
                            | Ok _ ->
                                runtime.MarkInterrupted(agentId, "host restart: legacy false abort rejected")

                                waiting.Add
                                    { Handle = agentHandle
                                      ChildSession = record.ChildSessionId
                                      Reason = "legacy false abort rejected" }
                            | Error failure ->
                                runtime.MarkInterrupted(
                                    agentId,
                                    sprintf
                                        "host restart: false abort reject failed: %s"
                                        (JournalAppendFailure.describe failure)
                                )

                                blocked.Add
                                    { Handle = agentHandle
                                      ChildSession = record.ChildSessionId
                                      Reason = JournalAppendFailure.describe failure }
                        | Invalid _ ->
                            // EXEC-022: Invalid blob = wait (not hard block). Align JoinDrain
                            // (Invalid → None / no consume) and ChildRecovery Incomplete.
                            runtime.MarkInterrupted(agentId, "host restart: invalid completion blob; waiting")

                            waiting.Add
                                { Handle = agentHandle
                                  ChildSession = record.ChildSessionId
                                  Reason = "invalid completion blob" }
                    | Ok(Some _, _, _) ->
                        runtime.MarkInterrupted(agentId, "host restart: completion blob ref/digest pair is incomplete")

                        blocked.Add
                            { Handle = agentHandle
                              ChildSession = record.ChildSessionId
                              Reason = "completion blob ref/digest pair is incomplete" }
                    | Ok(None, _, _) ->
                        let! result =
                            recoverChild
                                runtime
                                snapshot
                                (Some journal)
                                parentId
                                agentId
                                record.ChildSessionId
                                role
                                record.TargetAgent

                        match fromChildResult agentHandle record.ChildSessionId result with
                        | Choice1Of3 h -> recovered.Add h
                        | Choice2Of3 w -> waiting.Add w
                        | Choice3Of3 b -> blocked.Add b
                    | Error reason ->
                        runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)

                        blocked.Add
                            { Handle = agentHandle
                              ChildSession = record.ChildSessionId
                              Reason = reason }
                | HandleLifecycle.Active, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle
                    let role = AgentRoleIdentity.ofRole record.CanonicalRole

                    children.[agentId] <- record.ChildSessionId
                    childCreatedDir agentId record.ChildSessionId (directoryOf agentId)

                    let! result =
                        recoverChild
                            runtime
                            snapshot
                            (Some journal)
                            parentId
                            agentId
                            record.ChildSessionId
                            role
                            record.TargetAgent

                    match fromChildResult agentHandle record.ChildSessionId result with
                    | Choice1Of3 h -> recovered.Add h
                    | Choice2Of3 w -> waiting.Add w
                    | Choice3Of3 b -> blocked.Add b

            // HandleFamilyRecovery carries Domain.SessionRecovery.NonEmpty.
            if recovered.Count = 0 && waiting.Count = 0 && blocked.Count = 0 then
                return HandleFamilyRecovery.NoLinkedHandles
            elif blocked.Count > 0 then
                match Wanxiangshu.Domain.SessionRecovery.NonEmpty.ofList (List.ofSeq blocked) with
                | Some ne -> return HandleFamilyRecovery.HandlesBlocked ne
                | None -> return HandleFamilyRecovery.NoLinkedHandles
            elif waiting.Count > 0 then
                match Wanxiangshu.Domain.SessionRecovery.NonEmpty.ofList (List.ofSeq waiting) with
                | Some ne -> return HandleFamilyRecovery.HandlesWaiting ne
                | None -> return HandleFamilyRecovery.NoLinkedHandles
            else
                match Wanxiangshu.Domain.SessionRecovery.NonEmpty.ofList (List.ofSeq recovered) with
                | Some ne -> return HandleFamilyRecovery.HandlesRecovered ne
                | None -> return HandleFamilyRecovery.NoLinkedHandles
        }

    /// Restore without a live ForkRuntime (journal-only parent, no in-process mailbox).
    /// Still walks durable handles and ChildRecoveryWorkflow for Active/incomplete cells.
    let restoreLinkedChildrenWithoutRuntime
        (snapshot: ISessionSnapshotPort)
        (journal: AgentJournal)
        (parentId: SessionId)
        : Task<HandleFamilyRecovery> =
        let runtime = ForkRuntime()
        let children = Dictionary<string, SessionId>()
        restoreLinkedChildren runtime (Some snapshot) journal parentId children (fun _ _ _ -> ()) (fun _ -> None)
