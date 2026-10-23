namespace Wanxiangshu.Execution.Delegation.Fork

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
        {
            Name: string
            AgentId: string
            Role: string
            Status: string
            /// The main prose (report) the child returned, as raw text entries.
            Body: string list
            /// Structured carry material, separate from the report prose.
            Returned: string list
        }

    /// Join failure → the stable wire code the caller reports.
    val mapForkError: error: ForkError -> string

    /// One physical completion → the record the join wire carries.
    val mapJoinItem:
        resolveName: (string -> string) ->
        resolveRole: (string -> Role option -> string) ->
        resolveLabel: (string -> string) ->
        item: JoinItem ->
            JoinRecord

    /// One orchestrator verdict → the record the join wire carries.
    val mapOrchestratorVerdict: resolveJobName: (ManagerJobId -> string) -> verdict: OrchestratorVerdict -> JoinRecord
