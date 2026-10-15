namespace Wanxiangshu.Mission.Work.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentIteration
    | RetiredAttemptStopped

module RelayNarrativeTransform =
    val isStaleRetiredAttempt:
        journal: AgentJournal option ->
        acceptedRequest: bool ->
        sessionId: string option ->
        lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option ->
        outObj: obj ->
            bool

    val apply:
        journal: AgentJournal option ->
        acceptedRequest: bool ->
        interruptAttempt: (SessionId -> Task<unit>) ->
        sessionId: string option ->
        lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option ->
        outObj: obj ->
            Task<RelayProjectionDisposition>
