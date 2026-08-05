namespace Wanxiangshu.Session

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// Restart recovery for linked children. Terminal path: ChildRecoveryInterpreter
/// → JoinableCompletion → recordCompletion → PublishCompletion. Fail closed on proof.
/// Clean-break: legacy abort blobs never publish; retired false terminals migrate once.
module HostForkRestart =

    let private ports
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (role: AgentRole)
        (agent: string)
        : ChildRecoveryInterpreter.Ports =
        { Journal = journal
          ParentId = parentId
          Snapshot = snapshot
          AgentId = agentId
          Handle = HandleController.agentHandle agentId
          ChildSession = childSessionId
          Role = role
          Agent = agent
          Observations = [ HostObservation.RecoveryInFlight ]
          Publish = Some runtime.PublishCompletion }

    /// Active handle: Domain recoverChild via production interpreter.
    /// CommitCompletion → recordCompletion; then mailbox Publish. No bare cell write.
    let recoverChild
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (role: AgentRole)
        (agent: string)
        : Task<unit> =
        task {
            runtime.Restore(agentId, role, agent)
            runtime.BindChildSession(agentId, childSessionId)

            let p = ports runtime snapshot journal parentId agentId childSessionId role agent

            match! ChildRecoveryInterpreter.resolveAndCommit p with
            | Ok(ChildResolution.Joinable _) -> ()
            | Ok(ChildResolution.Abandon _) -> ()
            | Ok ChildResolution.AwaitingEvidence
            | Ok ChildResolution.RunningAgain ->
                // Cell stays open (pending). Interrupt busy work only; no finality.
                runtime.MarkInterrupted(agentId, "host restart: awaiting terminal evidence")
            | Ok(ChildResolution.Blocked reason)
            | Error reason -> runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)
        }

    /// Clean-break: retired handle whose last cell was a legacy abort → replacement once.
    let private migrateRetiredIfFalseAbort
        (journal: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : unit =
        match record.LastCompletion with
        | None -> ()
        | Some cell ->
            match cell.CompletionRef, cell.CompletionDigest with
            | Some blobRef, Some blobDigest ->
                match journal.Writer.BlobWriter.Read blobRef with
                | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                    match HandleCompletionCodec.decodeBody body with
                    | LegacyFalseAbort _ ->
                        ignore (
                            JoinDrain.tryMigrateRetiredFalseAbort journal parentId record blobRef blobDigest
                        )
                    | _ -> ()
                | _ -> ()
            | _ -> ()

    /// EXEC-009 restart recovery: rebuild this parent's join mailbox from the
    /// durable handle records.
    ///
    /// Retired handles: clean-break may mint a deterministic replacement when
    /// LastCompletion blob is LegacyFalseAbort. Otherwise tombstone stays permanent.
    ///
    /// PTY and ManagerJob handles are skipped: this rebuilds agent children, and
    /// a PTY is re-owned by `PtyPort`, not by a transcript replay.
    let restoreLinkedChildren
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (childCreatedDir: string -> SessionId -> string option -> unit)
        (directoryOf: string -> string option)
        : Task =
        task {
            let records =
                AgentProjection.tryFind parentId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind (fun session -> session.Handles)
                |> Option.map HandleProjection.linkedChildren
                |> Option.defaultValue []

            for record in records do
                match record.Lifecycle, HandleId.tryAgent record.Handle with
                | HandleLifecycle.Abandoned _, _
                | _, None -> ()
                | HandleLifecycle.Retired, Some _ -> migrateRetiredIfFalseAbort journal parentId record
                | HandleLifecycle.CompletedAwaitingJoin _, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle
                    let role = AgentRoleIdentity.ofRole record.CanonicalRole

                    children.[agentId] <- record.ChildSessionId
                    childCreatedDir agentId record.ChildSessionId (directoryOf agentId)
                    runtime.Restore(agentId, role, record.TargetAgent)
                    runtime.BindChildSession(agentId, record.ChildSessionId)

                    // Durable blob: decode first. LegacyFalseAbort → reject, no publish.
                    match HandleCompletionCodec.tryReadBody journal record with
                    | Ok(Some body, Some blobRef, Some blobDigest) ->
                        match HandleCompletionCodec.decodeBody body with
                        | Current decoded ->
                            let completion =
                                HandleCompletionCodec.tryMaterialiseRunCompletion record agentId decoded

                            ignore (
                                JoinableCompletion.fromDecoded
                                    agentId
                                    record.Handle
                                    record.ChildSessionId
                                    decoded
                                    body
                            )

                            runtime.PublishCompletion completion
                        | LegacyFalseAbort _ ->
                            match
                                AgentJournal.appendAgent
                                    (StreamId.Session parentId)
                                    None
                                    (AgentFact.HandleFalseCompletionRejected
                                        {| ParentSessionId = parentId
                                           Handle = record.Handle
                                           ExpectedCompletionRef = blobRef
                                           ExpectedCompletionDigest = blobDigest
                                           Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                                    journal
                            with
                            | Ok _ ->
                                runtime.MarkInterrupted(agentId, "host restart: legacy false abort rejected")
                            | Error failure ->
                                runtime.MarkInterrupted(
                                    agentId,
                                    sprintf
                                        "host restart: false abort reject failed: %s"
                                        (JournalAppendFailure.describe failure)
                                )
                        | Invalid _ ->
                            runtime.MarkInterrupted(agentId, "host restart: invalid completion blob")
                    | Ok(None, _, _) ->
                        do!
                            recoverChild
                                runtime
                                snapshot
                                (Some journal)
                                parentId
                                agentId
                                record.ChildSessionId
                                role
                                record.TargetAgent
                    | Error reason -> runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)
                | HandleLifecycle.Active, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle
                    let role = AgentRoleIdentity.ofRole record.CanonicalRole

                    children.[agentId] <- record.ChildSessionId
                    childCreatedDir agentId record.ChildSessionId (directoryOf agentId)

                    do!
                        recoverChild
                            runtime
                            snapshot
                            (Some journal)
                            parentId
                            agentId
                            record.ChildSessionId
                            role
                            record.TargetAgent
        }
