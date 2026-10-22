namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession

module ChildRecoveryWorkflow =
    type DeliveryViewReader = SessionId -> ProviderRunIdentity -> AuthorityRootUserMessageId -> string option

    type Ports =
        { Journal: AgentJournalPort option
          ReadDeliveryView: DeliveryViewReader option
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

    val resolveAndCommit: ports: Ports -> Task<Result<ChildRecoveryResult, string>>
