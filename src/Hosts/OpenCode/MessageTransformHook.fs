module Wanxiangshu.Hosts.Opencode.MessageTransformHook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Opencode.MessageTransformPipeline
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Dyn

let private extractSessionIDFromMessages (messagesArr: obj array) : string =
    if messagesArr.Length > 0 then
        let first = messagesArr.[0]
        let info = Dyn.get first "info"

        if not (Dyn.isNullish info) then
            Dyn.str info "sessionID"
        else
            ""
    else
        ""

let private extractSessionID (input: obj) (messagesArr: obj array option) =
    let sid1 = Dyn.str input "sessionID"

    if sid1 <> "" then
        sid1
    else
        let sid2 = Dyn.str input "sessionId"

        if sid2 <> "" then
            sid2
        else
            let sid3 = Dyn.str input "session_id"

            if sid3 <> "" then
                sid3
            else
                match messagesArr with
                | Some arr -> extractSessionIDFromMessages arr
                | None -> ""

let private isCompactionSummaryRequest (runtimeScope: RuntimeScope) (sessionID: string) =
    if sessionID <> "" then
        match runtimeScope.TryFindKey("fallbackRuntime") with
        | Some obj ->
            let fr = unbox<FallbackRuntimeStore> obj
            fr.UpdateSessionReturning(sessionID, tryConsumeCompactionSummaryTransformReturning)
        | None -> false
    else
        false

/// Transform the messages array in-place: resolve session context, build the
/// transform plan, and run the host-messages transform pipeline.
/// Compaction summary requests are detected via the pending flag and bypassed.
let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: RuntimeScope)
    (reviewStore: ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let sessionID = extractSessionID input (Some messagesArr)
            let isCompactionSummary = isCompactionSummaryRequest runtimeScope sessionID

            if isCompactionSummary then
                ()
            else
                let! (plan, capsEpoch, isSub) =
                    resolveTransformParams registry directory runtimeScope client input messagesArr

                do!
                    runHostMessagesTransformExecution
                        registry
                        directory
                        runtimeScope
                        reviewStore
                        plan.SessionID
                        plan.Agent
                        messagesArr
                        plan.SembleInjectEnabled
                        capsEpoch
                        plan
    }
