namespace Wanxiangshu.Mission.Work.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentTenure
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

    /// 条款 4：本次请求不再放行时清空消息——LLM 看不到 deliver 回复。
    val clearRequestMessages: outObj: obj -> unit
