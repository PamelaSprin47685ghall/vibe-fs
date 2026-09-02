namespace Wanxiangshu.Mission.Manager

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Vocabulary: durable Orchestrator evidence owns the turn → Manager completes and exits.
module ManagerJobHandoff =

    /// Result of checking whether Manager business ownership has transferred.
    [<RequireQualifiedAccess>]
    type HandoffOutcome =
        /// Orchestrator durable evidence consumed the observation; Manager must stop.
        | Transferred
        /// Manager still owns the turn; caller continues Manager sequencing.
        | ManagerOwnsTurn

    /// If durable Orchestrator evidence already owns this Manager turn, complete the agent
    /// and report Transferred; otherwise leave ownership with the Manager.
    val completeIfTransferred:
        eventPort: IEventObservationPort -> journal: AgentJournal option -> turn: ReconciledTurn -> Task<HandoffOutcome>
