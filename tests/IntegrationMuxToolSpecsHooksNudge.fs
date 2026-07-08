module Wanxiangshu.Tests.IntegrationMuxToolSpecsHooksNudge

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

/// `stream-end` with `metadata.muxStopReason = "tool_use_error"` must flow through
/// the Mux `eventHook` → `NudgeRuntime.HandleEvent` chain: when the session has
/// pending todos, dispatch must emit a nudge prompting the model to recover and
/// continue.  The test constructs the event, captures `helpers.nudge` to a
/// closed-over `ResizeArray`, fires the hook through the Bridge, yields one
/// microtask for the async chain to flush, and asserts nudge fired with a
/// recovery-shaped prompt.
let muxStreamEndToolUseErrorTriggersNudgeSpec () =
    promise {
        let sessionID = "mux-tool-use-error-recover-session"
        let textPart t = box {| ``type`` = "text"; text = t |}

        let assistantText =
            "Let me read the file: <tool_call><name>file_read</name><parameter name=\"path\">src/x.ts</parameter></tool_call>"

        let chatHistory =
            [| box
                   {| role = "assistant"
                      parts = [| textPart assistantText |] |} |]

        let! tmpDir = mkdtempAsync "mux-tooluse-error-nudge-"
        let deps = muxDepsWithChatHistory sessionID chatHistory
        let deps = Dyn.withKey deps "directory" (box tmpDir)
        let reg = createRegistration deps
        let nudges = ResizeArray<string>()

        let helpers =
            createObj
                [ "getTodos",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          promise { return box [| "investigate tool_use_error and resume work" |] })
                  )
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ msg ->
                          promise {
                              nudges.Add(string msg)
                              return true
                          })
                  ) ]

        let eventHook = get reg "eventHook"
        check "eventHook exists for tool_use_error nudge" (not (isNullish eventHook))

        let event =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "metadata", box (createObj [ "muxStopReason", box "tool_use_error" ])
                            "parts", box [| textPart "incomplete response" |] ]
                  ) ]

        do! (eventHook $ (event, helpers)) |> unbox<JS.Promise<unit>>
        do! Promise.sleep 50
        check "tool_use_error stream-end triggers a nudge dispatch" (nudges.Count > 0)

        check
            "nudge prompt carries recovery / continue signal"
            (nudges
             |> Seq.exists (fun n -> n.Contains "continue" || n.Contains "remaining" || n.Contains "incomplete"))

        do! rmAsync tmpDir
    }

let muxStreamEndToolCallsDoesNotTriggerNudgeSpec () =
    promise {
        let sessionID = "mux-tool-calls-nudge-session"
        let textPart t = box {| ``type`` = "text"; text = t |}

        let assistantText =
            "Let me read the file: <tool_call><name>file_read</name><parameter name=\"path\">src/x.ts</parameter></tool_call>"

        let chatHistory =
            [| box
                   {| role = "assistant"
                      parts = [| textPart assistantText |] |} |]

        let! tmpDir = mkdtempAsync "mux-toolcalls-nudge-"
        let deps = muxDepsWithChatHistory sessionID chatHistory
        let deps = Dyn.withKey deps "directory" (box tmpDir)
        let reg = createRegistration deps
        let nudges = ResizeArray<string>()

        let helpers =
            createObj
                [ "getTodos",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          promise { return box [| "investigate tool_use_error and resume work" |] })
                  )
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ msg ->
                          promise {
                              nudges.Add(string msg)
                              return true
                          })
                  ) ]

        let eventHook = get reg "eventHook"

        let event =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "metadata", box (createObj [ "muxStopReason", box "tool_calls" ])
                            "parts", box [| textPart "incomplete response" |] ]
                  ) ]

        do! (eventHook $ (event, helpers)) |> unbox<JS.Promise<unit>>
        do! Promise.sleep 50
        check "tool_calls stream-end does NOT trigger a nudge dispatch" (nudges.Count = 0)
        do! rmAsync tmpDir
    }
