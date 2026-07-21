module Wanxiangshu.Runtime.MessageTransform.Pipeline

open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.RuntimeScope

open Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity
open Wanxiangshu.Runtime.MessageTransform.ParallelHintStage
open Wanxiangshu.Runtime.MessageTransform.CapsStage

type MessageTransformPlan = Wanxiangshu.Runtime.MessageTransform.Plan.MessageTransformPlan

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let afterPrompt =
                match plan.ParallelHintPolicy with
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude -> plan.Cleaned
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include ->
                    tryInjectParallelToolPrompt plan.SessionID plan.Cleaned

            let encodedAfterPrompt = encodeMessages afterPrompt
            let! injected = injectFn encodedAfterPrompt
            return! prependCapsWithState plan.Scope plan.SessionID plan injected loadCaps buildCaps
    }
