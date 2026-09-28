namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.OpenCode
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module SessionBindingRecovery =

    /// The execution agent is the handle's `TargetAgent` (the Host-facing agent a
    /// managed child was dispatched with — `engineer` for a forked Engineer
    /// child), never its logical `Byname` (`decision-record-readonly`), which is
    /// only the reuse address of the handle. Binding the byname would make the
    /// next dispatch fail closed on participant drift.
    let private agentNameOf (record: HandleRecord) : string option =
        let target =
            if isNull record.TargetAgent then
                ""
            else
                record.TargetAgent.Trim()

        let byname = if isNull record.Byname then "" else record.Byname.Trim()

        if not (System.String.IsNullOrWhiteSpace target) then
            Some target
        elif not (System.String.IsNullOrWhiteSpace byname) then
            Some byname
        else
            None

    let private isLive (lifecycle: HandleLifecycle) : bool =
        match lifecycle with
        | HandleLifecycle.Active
        | HandleLifecycle.CompletedAwaitingJoin _ -> true
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> false

    let private restoreParent (parentSessionId: SessionId) (handles: AgentLinkageProjection) : unit =
        handles.Handles
        |> Map.iter (fun _ record ->
            if isLive record.Lifecycle then
                SessionExecutionBinding.restore parentSessionId record.ChildSessionId (agentNameOf record))

    let restoreFromProjection (projections: AgentProjectionSet) : unit =
        projections.Sessions
        |> Map.iter (fun parentSessionId session ->
            match session.Handles with
            | Some handles -> restoreParent parentSessionId handles
            | None -> ())

    let restoreFromDurable (journal: AgentJournal) : unit =
        (AgentJournal.snapshot journal).AgentProjections |> restoreFromProjection
