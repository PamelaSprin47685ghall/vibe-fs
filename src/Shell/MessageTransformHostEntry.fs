module Wanxiangshu.Shell.MessageTransformHostEntry

open Fable.Core
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ReviewRuntime

type ReviewReplayMode =
    | IfStoreEmpty
    | Always

type TransformFingerprint =
    | NoInput
    | ArrayRef of int
    | ArrayCopy of obj array

type SessionTransformCache =
    { mutable InputFingerprint: TransformFingerprint
      mutable OutputFingerprint: TransformFingerprint
      mutable OutputArray: obj array }

let mutable pipelineRunCount = 0

let private fingerprintEqual (a: TransformFingerprint) (b: TransformFingerprint) : bool =
    match a, b with
    | NoInput, NoInput -> true
    | ArrayRef x, ArrayRef y -> x = y
    | ArrayCopy arr1, ArrayCopy arr2 ->
        if System.Object.ReferenceEquals(arr1, arr2) then
            true
        elif arr1.Length <> arr2.Length then
            false
        else
            let mutable i = 0
            let mutable eq = true

            while i < arr1.Length && eq do
                if not (obj.Equals(arr1.[i], arr2.[i])) then
                    eq <- false

                i <- i + 1

            eq
    | _ -> false

let private computeFingerprint (raw: obj array) : TransformFingerprint = ArrayCopy raw

let private sessionTransformCaches =
    System.Collections.Generic.Dictionary<string, SessionTransformCache>()

let private getSessionCache
    (dict: System.Collections.Generic.Dictionary<string, SessionTransformCache>)
    (sessionID: string)
    =
    match dict.TryGetValue(sessionID) with
    | true, c -> c
    | false, _ ->
        let c =
            { InputFingerprint = NoInput
              OutputFingerprint = NoInput
              OutputArray = [||] }

        dict.[sessionID] <- c
        c

let runHostMessagesTransform
    (_reviewStore: ReviewStore)
    (sessionID: string)
    (_reviewReplayMode: ReviewReplayMode)
    (_replayTexts: unit -> JS.Promise<string seq>)
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: bool -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let raw =
                match plan.RawArray with
                | Some a -> a
                | None -> [||]

            let cache = getSessionCache sessionTransformCaches sessionID
            let currentFingerprint = computeFingerprint raw

            if cache.InputFingerprint = currentFingerprint then
                return cache.OutputArray
            elif
                cache.OutputFingerprint = currentFingerprint
                || System.Object.ReferenceEquals(cache.OutputArray, raw)
            then
                return raw
            else
                pipelineRunCount <- pipelineRunCount + 1

                let! result =
                    runMessageTransformPipeline plan backlogOps encodeMessages injectFn loadCaps buildCaps

                cache.InputFingerprint <- currentFingerprint
                cache.OutputFingerprint <- computeFingerprint result
                cache.OutputArray <- result
                return result
    }
