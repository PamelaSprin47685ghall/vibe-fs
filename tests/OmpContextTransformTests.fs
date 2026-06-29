module Wanxiangshu.Tests.OmpContextTransformTests

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Omp.MessageTransform
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter

[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private pathModule : obj = requireFn "path"
let private join (a: string) (b: string) = unbox<string> (pathModule?join(a, b))

[<Emit("$0[0].parts[0].text")>]
let private firstEntryTextFromOut (entries: obj array) : string = jsNative

let capsSynthUserPrepended () = promise {
    let reviewStore = createReviewStore ()
    let entries =
        [| createObj [
               "info", box(createObj [ "id", box "user-1"; "role", box "user" ])
               "parts", box [| createObj [ "type", box "text"; "text", box "hello" ] |]
           ] |]
    let! out = transformEntriesAsync reviewStore "/tmp/ws" "sess-1" (box entries)
    check "prepends at least caps synth user" (out.Length >= entries.Length + 1)
    let firstInfo = Dyn.get out.[0] "info"
    check "caps user id prefix" ((Dyn.str firstInfo "id").StartsWith "caps-synth-user-")
    let firstText = firstEntryTextFromOut out
    check "has think prelude" (firstText.Contains "铁律")
}

let capsReadToolsInContextTransform () = promise {
    let! root = mkdtempAsync "omp-caps-ctx-"
    do! writeFileAsync (join root "ARCH.md") "arch-in-context"
    let reviewStore = createReviewStore ()
    let entries =
        [| createObj [
               "info", box(createObj [ "id", box "user-1"; "role", box "user" ])
               "parts", box [| createObj [ "type", box "text"; "text", box "hello" ] |]
           ] |]
    let! out = transformEntriesAsync reviewStore root "caps-sess" (box entries)
    try
        check "prepends user and assistant for caps" (out.Length >= entries.Length + 2)
        let mutable foundRead = false
        for entry in out do
            let parts = Dyn.get entry "parts"
            if Dyn.isArray parts then
                for part in unbox<obj array> parts do
                    if Dyn.str part "type" = "tool" && Dyn.str part "tool" = "read" then
                        let state = Dyn.get part "state"
                        let outText = Dyn.str state "output"
                        if outText.Contains "arch-in-context" then foundRead <- true
        check "caps file surfaced as read tool output" foundRead
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}

let beforeAgentStartOmitsCapsXml () = promise {
    let! root = mkdtempAsync "omp-before-start-"
    do! writeFileAsync (join root "ARCH.md") "should-not-be-in-system"
    let! patch = beforeAgentStart root (box [| "line-one" |])
    try
        let sp = Dyn.get patch "systemPrompt"
        let arr = if Dyn.isArray sp then unbox<string array> sp else [| string sp |]
        let joined = String.concat "\n" arr
        check "system prompt has user line" (joined.Contains "line-one")
        check "system prompt omits caps-context xml" (not (joined.Contains "<caps-context"))
    with e ->
        do! rmAsync root
        raise e
    do! rmAsync root
}

let reviewReplayIfStoreEmptyOnTransform () = promise {
    let reviewStore = createReviewStore ()
    let sessionId = "omp-review-if-empty"
    reviewStore.activateReview(sessionId, "task A", 1L)
    let historyTaskB =
        frontMatterPrompt [ yamlField taskField "task B" ] "With-Review body from history"
    let entries =
        [| createObj [
               "info", box(createObj [ "id", box "user-hist"; "role", box "user" ])
               "parts", box [| createObj [ "type", box "text"; "text", box historyTaskB ] |]
           ] |]
    let! _ = transformEntriesAsync reviewStore "/tmp/ws" sessionId (box entries)
    equal "review replay IfStoreEmpty: store task unchanged when already active" (Some "task A") (reviewStore.getReviewTask sessionId)
}