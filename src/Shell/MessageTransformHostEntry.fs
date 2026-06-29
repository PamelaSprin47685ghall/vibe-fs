module Wanxiangshu.Shell.MessageTransformHostEntry

open Fable.Core
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ReviewReplaySync
open Wanxiangshu.Shell.ReviewRuntime

type ReviewReplayMode =
    | IfStoreEmpty
    | Always

let runHostMessagesTransform
    (reviewStore: ReviewStore)
    (sessionID: string)
    (reviewReplayMode: ReviewReplayMode)
    (replayTexts: unit -> JS.Promise<string seq>)
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: bool -> obj array -> JS.Promise<obj array>)
    (dedupFn: bool -> obj array -> obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let shouldReplay =
            sessionID <> "" &&
            match reviewReplayMode with
            | Always -> true
            | IfStoreEmpty -> reviewStore.getReviewState sessionID |> Option.isNone
        if shouldReplay then
            let! texts = replayTexts ()
            syncReviewFromTexts reviewStore sessionID texts
        return!
            if plan.Cleaned.IsEmpty then Promise.lift [||]
            else
                runMessageTransformPipeline plan backlogOps encodeMessages injectFn dedupFn loadCaps buildCaps
    }
