namespace Wanxiangshu.Session

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// Restart recovery for linked children. Terminal path: ChildRecoveryInterpreter
/// → JoinableCompletion → recordCompletion → PublishCompletion. Fail closed on proof.
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

    /// EXEC-009 restart recovery: rebuild this parent's join mailbox from the
    /// durable handle records.
    ///
    /// Retired handles are skipped. EXEC-009 makes the tombstone permanent and
    /// forbids a retired id degrading back into a fork target, so restoring one
    /// would put a consumed completion back on the mailbox and let `join` return it
    /// a second time.
    ///
    /// PTY and ManagerJob handles are skipped too: this rebuilds agent children, and
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
                | HandleLifecycle.Retired, _
                | HandleLifecycle.Abandoned _, _
                | _, None -> ()
                | HandleLifecycle.CompletedAwaitingJoin _, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle
                    let role = AgentRoleIdentity.ofRole record.CanonicalRole

                    children.[agentId] <- record.ChildSessionId
                    childCreatedDir agentId record.ChildSessionId (directoryOf agentId)
                    runtime.Restore(agentId, role, record.TargetAgent)
                    runtime.BindChildSession(agentId, record.ChildSessionId)

                    // Durable blob already sealed: mailbox only after proof. No re-record.
                    match record.Lifecycle, HandleCompletionCodec.tryRead journal record agentId with
                    | HandleLifecycle.CompletedAwaitingJoin cell, Ok(Some completion) ->
                        let body = HandleCompletionCodec.encodeOutcome completion.RunId completion.Outcome

                        match
                            JoinableCompletion.tryFromDurableCompleted
                                agentId
                                record.Handle
                                record.ChildSessionId
                                cell.Kind
                                (Some body)
                        with
                        | Ok _ -> runtime.PublishCompletion completion
                        | Error reason ->
                            runtime.MarkInterrupted(agentId, sprintf "host restart: proof failed: %s" reason)
                    | _, Ok None ->
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
                    | _, Error reason -> runtime.MarkInterrupted(agentId, sprintf "host restart: %s" reason)
                    | _, Ok(Some _) -> ()
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
