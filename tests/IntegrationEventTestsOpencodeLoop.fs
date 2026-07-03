module Wanxiangshu.Tests.IntegrationEventTestsOpencodeLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let private loopAnchor task = frontMatterPrompt [ yamlField taskField task ] "With-Review Mode is active."

let private assistantMessage agent text completed =
    box
        {| info = box {| role = "assistant"; agent = agent; finish = "stop"; time = box {| completed = completed |} |}
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let opencodeLoopNudgeSpec () = promise {
    let sessionID = "opencode-loop-nudge-ws"
    let promptCalls = ResizeArray<obj>()
    let mutable messages : obj array = [| assistantMessage "manager" (loopAnchor "Ship the fix") 1 |]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-loop-nudge-"
    do! seedLoopActivated workspaceDir sessionID "Ship the fix"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    let nudgeText =
        if promptCalls.Count = 0 then ""
        else getPartsText (get (get promptCalls.[0] "body") "parts")
    check "with-review idle emits loop nudge" (promptCalls.Count = 1 && nudgeText = loopNudgePrompt)
    do! rmAsync workspaceDir
}

let opencodeFreshChatMessageRearmsLoopNudgeSpec () = promise {
    let sessionID = "opencode-fresh-chat-ws"
    let promptCalls = ResizeArray<obj>()
    let mutable messages : obj array =
        [| assistantMessage "manager" (loopAnchor "Ship the fix") 1 |]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-fresh-chat-"
    do! seedLoopActivated workspaceDir sessionID "Ship the fix"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let chatHook = get p "chat.message"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    let textOf i = if promptCalls.Count <= i then "" else getPartsText (get (get promptCalls.[i] "body") "parts")
    check "first with-review idle emits loop nudge" (promptCalls.Count = 1 && textOf 0 = loopNudgePrompt)
    do! chatHook $ (createObj [ "sessionID", box sessionID; "agent", box "manager" ], createObj [ "parts", box [| box {| ``type`` = "text"; text = "still working on it" |} |] ]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    messages <- Array.append messages [| assistantMessage "manager" "still working on it" 2 |]
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "new assistant turn in history re-arms loop nudge on next idle" (promptCalls.Count = 2 && textOf 1 = loopNudgePrompt)
    do! rmAsync workspaceDir
}

let opencodeBrowserSubsessionHistoryDoesNotLoopNudgeSpec () = promise {
    let sessionID = "opencode-browser-child"
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                let reviewerPrompt = Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewerPrompt "ship feature" "" []
                promise { return box {| data = [| assistantMessage "browser" reviewerPrompt 1 |] |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-browser-child-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "reviewer-style child history must not trigger loop nudge" (promptCalls.Count = 0)
    do! rmAsync workspaceDir
}
