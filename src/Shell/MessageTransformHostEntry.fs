module VibeFs.Shell.MessageTransformHostEntry

open Fable.Core
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Messaging
open VibeFs.Shell.MessageTransformCore
open VibeFs.Shell.MessageTransformPipeline
open VibeFs.Shell.ReviewReplaySync
open VibeFs.Shell.ReviewRuntime

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
    (dedupFn: bool -> obj array -> obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (loadKgPrelude: unit -> JS.Promise<string option>)
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
                runMessageTransformPipeline plan backlogOps encodeMessages dedupFn loadCaps loadKgPrelude buildCaps
    }
