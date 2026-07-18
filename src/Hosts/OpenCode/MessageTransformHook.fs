module Wanxiangshu.Hosts.Opencode.MessageTransformHook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Opencode.MessageTransformPipeline
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ChatTransformOutputCodec

/// Transform the messages array in-place: resolve session context, build the
/// transform plan, and run the host-messages transform pipeline.
let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: RuntimeScope)
    (backlogSession: BacklogSession)
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
            let! (plan, capsEpoch, isSub) =
                resolveTransformParams registry directory runtimeScope client input messagesArr

            do!
                runHostMessagesTransformExecution
                    registry
                    directory
                    runtimeScope
                    backlogSession
                    reviewStore
                    plan.SessionID
                    plan.Agent
                    messagesArr
                    plan.SembleInjectEnabled
                    capsEpoch
                    plan
    }
