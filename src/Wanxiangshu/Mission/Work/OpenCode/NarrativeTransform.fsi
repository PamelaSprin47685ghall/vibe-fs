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
    val apply:
        journal: AgentJournal option ->
        acceptedRequest: bool ->
        interruptAttempt: (SessionId -> Task<unit>) ->
        sessionId: string option ->
        lifecycle: LifecycleWork option ->
        outObj: obj ->
            Task<RelayProjectionDisposition>
