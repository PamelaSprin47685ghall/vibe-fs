module Wanxiangshu.Hosts.Opencode.MessageTransformPipeline

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.JsArrayMutate

open Wanxiangshu.Hosts.Opencode.MessageTransformPipelineHelper

open Wanxiangshu.Runtime.MessageTransform.HostEntry

let runHostMessagesTransformExecution
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: RuntimeScope)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionID: string)
    (agent: string)
    (messagesArr: obj array)
    (sembleInjectEnabled: bool)
    (capsEpoch: string)
    (plan: MessageTransformPlan)
    : JS.Promise<unit> =
    promise {
        let injectFn = buildInjectionFn directory agent sessionID runtimeScope

        let loadCaps = buildLoadCaps registry runtimeScope sessionID plan

        let buildCaps = buildCapsFn capsEpoch directory

        let! final =
            Wanxiangshu.Runtime.MessageTransform.HostEntry.runHostMessagesTransform
                reviewStore
                sessionID
                plan
                MessagingCodec.encodeMessages
                injectFn
                loadCaps
                buildCaps

        replaceArrayInPlace messagesArr final
    }

/// Resolve all transform parameters from raw input and return the plan,
/// capsEpoch, and isSub flag (forwarded to helper).
let resolveTransformParams
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: RuntimeScope)
    (client: obj)
    (input: obj)
    (messagesArr: obj array)
    : JS.Promise<MessageTransformPlan * string * bool> =
    MessageTransformPipelineHelper.resolveTransformParams registry directory runtimeScope client input messagesArr
