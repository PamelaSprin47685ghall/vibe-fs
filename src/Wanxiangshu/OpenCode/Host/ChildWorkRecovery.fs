namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: a restart never replays interrupted work and
/// the tool call that was in flight stays failed in visible history. That leaves
/// child work — the road's fixed DevOps and any forked Engineer child — with a
/// logical run no live execution can ever satisfy: the previous runtime is gone,
/// so `ChatExecutionRecovery` treats its physical evidence as stale and writes
/// nothing, and the child authority stays open forever. The next handoff is then
/// refused with `ActiveRunIdentityConflict` while the parent's join waits on a
/// handle that will never complete.
///
/// The explicit reset happens here, once, at load: every child work run still
/// active at startup is voided — the durable evidence that closes the child
/// authority through its exact admitted work while
/// leaving the handle itself untouched, so nothing appears as an unreported
/// delivery. The child's transcript is untouched, and restarting the work
/// remains the manager's own explicit decision.
module ChildWorkRecovery =

    type OrphanedChildRun =
        private
        | Admitted of parentSessionId: SessionId * work: AdmittedWork
        | Historical of parentSessionId: SessionId * childSessionId: SessionId

    let private isChildWorkRun (run: PromptAuthority.AuthorityExecutionProfile) : bool =
        run.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        && run.CanonicalRole <> Role.Manager

    let private activeChildRuns (projections: AgentProjectionSet) =
        projections.Sessions
        |> Map.toList
        |> List.choose (fun (sessionId, session) ->
            session.PromptAuthority
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
            |> Option.filter isChildWorkRun
            |> Option.map (fun run -> sessionId, run))

    let private scopedRun
        parentSessionId
        handle
        (handles: AgentLinkageProjection)
        (run: PromptAuthority.AuthorityExecutionProfile)
        =
        handles.Works
        |> Map.toList
        |> List.tryPick (fun (work, record) ->
            HandleProjection.tryAdmittedWork work handles
            |> Result.toOption
            |> Option.filter (fun admitted ->
                work.Handle = handle
                && work.ChildSessionId = run.SessionId
                && work.AuthorityRoot = run.AuthorityRootUserMessageId
                && AdmittedWork.logicalRunId admitted = run.LogicalRunId
                && record.LogicalRunId = run.LogicalRunId)
            |> Option.map (fun admitted -> Admitted(parentSessionId, admitted)))

    let private runForHandle
        parentSessionId
        handle
        (handles: AgentLinkageProjection)
        (run: PromptAuthority.AuthorityExecutionProfile)
        =
        let scopedChild =
            handles.Works |> Map.exists (fun work _ -> work.ChildSessionId = run.SessionId)

        if scopedChild then
            scopedRun parentSessionId handle handles run
        else
            Some(Historical(parentSessionId, run.SessionId))

    let private handleFor (projections: AgentProjectionSet) (childSessionId, run) : OrphanedChildRun option =
        projections.HandleByChildSession
        |> Map.tryFind childSessionId
        |> Option.filter (fun record ->
            match record.Lifecycle with
            | HandleLifecycle.Active -> true
            | _ -> false)
        |> Option.bind (fun record ->
            projections.Sessions
            |> Map.toList
            |> List.tryPick (fun (parentSessionId, session) ->
                match session.Handles with
                | Some handles when HandleProjection.tryFind record.Handle handles |> Option.isSome ->
                    runForHandle parentSessionId record.Handle handles run
                | _ -> None))

    /// The child work runs a fresh process can no longer execute, with the parent
    /// handle that has to carry their settlement.
    let orphanedChildRuns (projections: AgentProjectionSet) : OrphanedChildRun list =
        activeChildRuns projections |> List.choose (handleFor projections)

    /// The interrupted run produced nothing, so it owes nothing: closing the
    /// child's logical run is the whole settlement. Marking a completion here
    /// would leave an unreported delivery that horizon never shows and join is
    /// asked to collect — the child stays reusable and both views stay empty,
    /// exactly as a fresh process should look.
    let settlementFact (orphaned: OrphanedChildRun) : ExecutionFactCases =
        match orphaned with
        | Admitted(parentSessionId, work) ->
            ExecutionFactCases.ChildWorkVoided
                {| ParentSessionId = parentSessionId
                   Work = AdmittedWork.id work |}
        | Historical(parentSessionId, childSessionId) ->
            ExecutionFactCases.ChildRunVoided
                {| ParentSessionId = parentSessionId
                   ChildSessionId = childSessionId |}

    let private parentOf =
        function
        | Admitted(parentSessionId, _)
        | Historical(parentSessionId, _) -> parentSessionId

    let settleOrphanedChildRuns (journal: AgentJournal) : Task<unit> =
        task {
            let projections = (AgentJournal.snapshot journal).AgentProjections

            for orphaned in orphanedChildRuns projections do
                match!
                    AgentJournal.appendAgent
                        (StreamId.Session(parentOf orphaned))
                        None
                        (AgentFact.Execution(settlementFact orphaned))
                        journal
                with
                | Ok _ -> ()
                | Error failure -> raise (JournalAppendException failure)

            return ()
        }
