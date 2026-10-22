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
          Returned: string list }

    let private plain text = if isNull text then [] else [ text ]

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
            { Name = resolveName payload.AgentId
              AgentId = payload.AgentId
              Role = Roles.roleLabel payload.Role
              Status = "completed"
              Returned = ReturnedMaterial.texts payload.View }
        | JoinItem.AgentItem(AgentFailedItem payload) ->
            { Name = resolveName payload.AgentId
              AgentId = payload.AgentId
              Role = resolveRole payload.AgentId payload.Role
              Status = "failed"
              Returned = plain payload.Message }
        | JoinItem.AgentItem(AgentAbandonedItem(agentId, reason)) ->
            { Name = resolveName agentId
              AgentId = agentId
              Role = resolveRole agentId None
              Status = if reason = "Voided" then "voided" else "abandoned"
              Returned = [] }
        | JoinItem.PtyItem(PtyExited payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "completed"
              Returned = plain payload.Outcome }
        | JoinItem.PtyItem(PtyFailed payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "failed"
              Returned = plain payload.Message }
        | JoinItem.PtyItem(PtyAborted payload) ->
            { Name = resolveLabel payload.PtyId
              AgentId = payload.PtyId
              Role = Roles.roleLabel Role.DevOps
              Status = "abandoned"
              Returned = plain payload.Message }

    /// One orchestrator verdict → the record the join wire carries.
    let mapOrchestratorVerdict (resolveJobName: ManagerJobId -> string) (verdict: OrchestratorVerdict) : JoinRecord =
        match verdict with
        | OrchestratorVerdict.Published(jobId, head)
        | OrchestratorVerdict.PublishedPendingCleanup(jobId, head, _) ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "completed"
              Returned = [ CommitHash.value head ] }
        | OrchestratorVerdict.Cancelled jobId ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "abandoned"
              Returned = [] }
        | OrchestratorVerdict.IntegrationFailed(jobId, errorDetails) ->
            { Name = resolveJobName jobId
              AgentId = ManagerJobId.value jobId
              Role = Roles.roleLabel Role.Manager
              Status = "failed"
              Returned = plain errorDetails }
        | OrchestratorVerdict.Empty ->
            { Name = ""
              AgentId = ""
              Role = Roles.roleLabel Role.Manager
              Status = "abandoned"
              Returned = [] }
