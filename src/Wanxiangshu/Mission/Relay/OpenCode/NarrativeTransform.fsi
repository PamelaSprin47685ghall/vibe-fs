namespace Wanxiangshu.Mission.Relay.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentIteration
    | RetiredAttemptStopped

module RelayNarrativeTransform =
    val apply:
        journal: AgentJournal option ->
        acceptedHuman: bool ->
        interruptAttempt: (SessionId -> Task<unit>) ->
        sessionId: string option ->
        outObj: obj ->
            Task<RelayProjectionDisposition>
