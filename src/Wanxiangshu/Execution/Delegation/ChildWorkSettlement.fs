namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: a fresh process cannot execute the child
/// work the previous process left behind, so that obligation is settled by a
/// void: the work becomes Abandoned(Voided) — reported once by join, then
/// retired.
module ChildWorkSettlement =

    type OrphanedChildRun =
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

    /// The exact settlement fact for one orphaned child run: a scoped (admitted)
    /// child work is voided as ChildWorkVoided; an unscoped historical child run
    /// is voided as ChildRunVoided.
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

    /// Settle every orphaned child run once, by appending its exact settlement
    /// fact to the parent session stream. Load Phase; no dispatch, no provider
    /// call, no command replay.
    let settleOrphanedChildRuns (journal: AgentJournal) : Task<unit> =
        task {
            let projections = (AgentJournal.snapshot journal).AgentProjections

            for orphaned in orphanedChildRuns projections do
                let parent = parentOf orphaned

                Diagnostic.emit
                    "child-work-orphaned"
                    [ "session_id", SessionId.value parent
                      "result", sprintf "orphaned child work is voided: %A" orphaned ]

                match!
                    AgentJournal.appendAgent
                        (StreamId.Session parent)
                        None
                        (AgentFact.Execution(settlementFact orphaned))
                        journal
                with
                | Ok _ -> ()
                | Error failure -> raise (JournalAppendException failure)

            return ()
        }
