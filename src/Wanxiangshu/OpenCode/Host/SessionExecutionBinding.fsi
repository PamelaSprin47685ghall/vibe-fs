namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Provider-step boundary of one managed chat execution. It owns no identity:
/// managed-ness, participant and model all come from durable `Accepted` evidence
/// and the `ModelRouting` lease for the exact execution key.
module SessionExecutionBinding =
    /// host-boundary-008: durable managed-execution evidence for one exact key.
    val isManagedExecution: durable: AgentJournal option -> key: ChatExecutionKey -> bool

    /// HOST-004: begin the provider step of the physical message this transform
    /// request answers. Durable evidence decides whether a step is entered.
    val beginPhysicalProviderAttemptForTransform:
        journal: AgentJournal option ->
        beginQuiescence: (SessionId -> unit) ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task<unit>
