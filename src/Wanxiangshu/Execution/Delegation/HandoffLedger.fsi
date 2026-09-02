namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationHandoffLedger =
    val prepare:
        workRecord: DelegationWorkRecordCapability ->
        journal: AgentJournal ->
        parent: SessionId ->
        route: DelegationHandoffRoute ->
            Task<PreparedDelegationHandoff>

    val checkpointCompleted:
        journal: AgentJournal -> parent: SessionId -> handoff: PreparedDelegationHandoff -> Task<Result<unit, string>>

    val port: workRecord: DelegationWorkRecordCapability -> journal: AgentJournal -> ReusableHandoffPort
