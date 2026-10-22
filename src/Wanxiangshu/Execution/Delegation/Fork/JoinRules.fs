namespace Wanxiangshu.Execution.Delegation.Fork

open System
open Wanxiangshu.Change
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Join outcome records and the product rules behind them, host-neutral.
/// A host reaches them through its own join surface and renders the
/// `JoinRecord` itself; no host capability is assumed here.
module JoinRules =

    /// One joined child, reduced to the fields the join wire carries.
    type JoinRecord =
        { Name: string
          AgentId: string
          Role: string
          Status: string
          View: string }

    /// A completed run with no delivered view is not a completed delivery.
    let decideCompletedStatus (view: string) : string =
        if String.IsNullOrWhiteSpace view then
            "undelivered"
        else
            "completed"

    /// Join failure → the stable wire code the caller reports.
    let mapForkError (error: ForkError) : string =
        match error with
        | ForkError.NothingToJoin
        | ForkError.Empty -> "nothing-to-join"
        | ForkError.JoinInProgress -> "join-in-progress"
        | ForkError.Cancelled
        | ForkError.TimedOut -> "interrupted"
        | ForkError.Abandoned _ -> "abandoned"
        | ForkError.NotFound _
        | ForkError.TerminalMaterializationFailed _ -> "not-ready"

    /// One physical completion → the record the join wire carries.
    let mapJoinItem
        (resolveName: string -> string)
        (resolveRole: string -> Role option -> string)
        (resolveLabel: string -> string)
        (item: JoinItem)
        : JoinRecord =
        match item with
        | JoinItem.AgentItem(AgentCompletedItem payload) ->
            let view = if isNull payload.View then "" else payload.View

            { Name = resolveName payload.AgentId
              AgentId = payload.AgentId
              Role = Roles.roleLabel payload.Role
              Status = decideCompletedStatus view
              View = view }
        | JoinItem.AgentItem(AgentFailedItem payload) ->
            { Name = resolveName payload.AgentId
              AgentId = payload.AgentId
              Role = resolveRole payload.AgentId payload.Role
              Status = "failed"
              View = if isNull payload.Message then "" else payload.Message }
        | JoinItem.AgentItem(AgentAbandonedItem(agentId, reason)) ->
            { Name = resolveName agentId
              AgentId = agentId
              Role = resolveRole agentId None
              Status = if reason = "Voided" then "voided" else "abandoned"
              View = "" }
        | JoinItem.PtyItem(PtyExited payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "completed"
              View = if isNull payload.Outcome then "" else payload.Outcome }
        | JoinItem.PtyItem(PtyFailed payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "failed"
              View = if isNull payload.Message then "" else payload.Message }
        | JoinItem.PtyItem(PtyAborted payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "abandoned"
              View = if isNull payload.Message then "" else payload.Message }

    /// One orchestrator verdict → the record the join wire carries.
    let mapOrchestratorVerdict (resolveJobName: ManagerJobId -> string) (verdict: OrchestratorVerdict) : JoinRecord =
        match verdict with
        | OrchestratorVerdict.Published(jobId, head)
        | OrchestratorVerdict.PublishedPendingCleanup(jobId, head, _) ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "completed"
              View = CommitHash.value head }
        | OrchestratorVerdict.Cancelled jobId ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "abandoned"
              View = "" }
        | OrchestratorVerdict.RejectedDirty reason ->
            { Name = ""
              AgentId = ""
              Role = Roles.roleLabel Role.Manager
              Status = "failed"
              View = if isNull reason then "" else reason }
        | OrchestratorVerdict.IntegrationFailed(jobId, errorDetails) ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "failed"
              View = if isNull errorDetails then "" else errorDetails }
        | OrchestratorVerdict.Empty ->
            { Name = ""
              AgentId = ""
              Role = Roles.roleLabel Role.Manager
              Status = "abandoned"
              View = "" }
