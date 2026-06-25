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

let replayReviewForMode (mode: ReviewReplayMode) (store: ReviewStore) (sessionID: string) (texts: string seq) : unit =
    match mode with
    | Always -> replayReviewAlwaysSync store sessionID texts
    | IfStoreEmpty -> replayReviewIfStoreEmpty store sessionID texts

let runHostMessagesTransform
    (reviewStore: ReviewStore)
    (sessionID: string)
    (reviewReplayMode: ReviewReplayMode)
    (replayTexts: unit -> string seq)
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (dedupFn: bool -> obj array -> obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (loadKgPrelude: unit -> JS.Promise<string option>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        replayReviewForMode reviewReplayMode reviewStore sessionID (replayTexts ())
        return!
            if plan.Cleaned.IsEmpty then Promise.lift [||]
            else
                runMessageTransformPipeline plan backlogOps encodeMessages dedupFn loadCaps loadKgPrelude buildCaps
    }