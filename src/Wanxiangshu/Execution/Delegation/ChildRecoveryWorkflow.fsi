namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module ChildRecoveryWorkflow =
    type Ports =
        { Journal: AgentJournal option
          ParentId: SessionId
          Snapshot: ISessionSnapshotPort option
          AgentId: string
          Handle: HandleId
          ChildSession: SessionId
          Role: Role
          Agent: string
          Observations: HostObservation list
          Pulse: (unit -> unit) option
          Clock: IClockPort }

    val commitJoinable:
        journal: AgentJournal option -> parentId: SessionId -> proof: JoinableCompletion -> Task<Result<unit, string>>

    val resolveAndCommit: ports: Ports -> Task<Result<ChildRecoveryResult, string>>
