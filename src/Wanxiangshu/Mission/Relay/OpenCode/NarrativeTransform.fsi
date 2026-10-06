namespace Wanxiangshu.Mission.Relay.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentIteration
    | RetiredAttemptStopped

module RelayNarrativeTransform =
    /// Whether the request currently being transformed is the successor of the
    /// road's latest retirement: a fresh external physical request (a new
    /// authority root the road has not consumed, the claimed manager-loop gate
    /// continuation, or a post-cut message admitted into ChatExecutions).
    /// Single source of truth for successor identity — the stale-attempt
    /// classification and the Accepted-road re-open gate both derive from it
    /// (relay-retirement-008).
    val isCurrentRequestSuccessor:
        journal: AgentJournal ->
        sessionId: SessionId ->
        road: RoadView ->
        retirement: RetirementSummary ->
        acceptedRequest: bool ->
        messages: obj list ->
            bool

    val apply:
        journal: AgentJournal option ->
        acceptedRequest: bool ->
        interruptAttempt: (SessionId -> Task<unit>) ->
        sessionId: string option ->
        outObj: obj ->
            Task<RelayProjectionDisposition>
