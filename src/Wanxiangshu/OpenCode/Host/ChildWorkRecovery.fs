namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: orphaned child runs maintain visible obligations
/// across restart. Active child runs are not voided into silence; they remain
/// active so unfulfilled obligations remain observable to the parent road.
/// Diagnostics are emitted for visibility, and Fission groups are settled as failed.
module ChildWorkRecovery =

    [<Literal>]
    let OrphanedFissionReason = "process_interrupted"

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

    /// Orphaned child runs maintain active obligations across restart without being voided.
    /// settlementFact returns None so no ChildWorkVoided/ChildRunVoided fact is written.
    let settlementFact (orphaned: OrphanedChildRun) : ExecutionFactCases option = None

    let private parentOf =
        function
        | Admitted(parentSessionId, _)
        | Historical(parentSessionId, _) -> parentSessionId

    let orphanedFissionGroups (projections: AgentProjectionSet) : FissionGroupProjection list =
        projections.Fission.ActiveByOwner
        |> Map.toList
        |> List.choose (fun (_, groupId) -> Map.tryFind groupId projections.Fission.Groups)
        |> List.filter (fun group ->
            match group.Terminal with
            | FissionGroupTerminal.Open -> true
            | _ -> false)

    let fissionSettlementFact (group: FissionGroupProjection) : FissionFactCases =
        FissionFactCases.FissionFailed
            {| GroupId = group.GroupId
               OwnerSessionId = group.OwnerSessionId
               Reason = OrphanedFissionReason |}

    let private drainAdmittedPtys (drainPtys: AdmittedWork -> Task<unit>) =
        function
        | Admitted(_, work) -> drainPtys work
        | Historical _ -> Task.FromResult(())

    let settleOrphanedChildRuns (drainPtys: AdmittedWork -> Task<unit>) (journal: AgentJournal) : Task<unit> =
        task {
            let projections = (AgentJournal.snapshot journal).AgentProjections

            for orphaned in orphanedChildRuns projections do
                do! drainAdmittedPtys drainPtys orphaned
                let parent = parentOf orphaned

                Diagnostic.emit
                    "child-work-orphaned"
                    [ "session_id", SessionId.value parent
                      "result", sprintf "orphaned child work remains active: %A" orphaned ]

            for group in orphanedFissionGroups projections do
                match!
                    AgentJournal.appendAgent
                        (StreamId.Session group.OwnerSessionId)
                        None
                        (AgentFact.Fission(fissionSettlementFact group))
                        journal
                with
                | Ok _ -> FissionRuntime.clearOwner group.OwnerSessionId
                | Error failure -> raise (JournalAppendException failure)

            return ()
        }
