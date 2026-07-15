module Wanxiangshu.Tests.IntegrationEventTestsMuxReview

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace

open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn

[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

let private loopAnchor task =
    frontMatterPrompt [ yamlField taskField task ] "With-Review Mode is active."

let reviewerReviseRenudgesLoopSpec () =
    promise {
        let! tempDir = mkdtempAsync "mux-review-revise-"
        let sessionID = "review-revise-ws"
        do! seedLoopActivated tempDir sessionID "Implement feature X"

        let mutable history =
            [| muxTextMessage "review-loop-anchor" "assistant" (loopAnchor "Implement feature X")
               muxTextMessage "review-assistant-1" "assistant" "implemented first pass" |]

        let reg =
            createRegistration (
                createObj
                    [ "directory", box tempDir
                      "loadConfigOrDefault", box (fun () -> createObj [])
                      "findWorkspaceEntry",
                      box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                      "resolveAgentFrontmatter",
                      box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                      "getChatHistory",
                      box (
                          System.Func<string, JS.Promise<obj array>>(fun workspaceId ->
                              promise { return if workspaceId = sessionID then history else [||] })
                      ) ]
            )

        let nudges = ResizeArray<string>()
        let mutable nudgeCount = 0

        let helpers =
            createObj
                [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] }))
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                          promise {
                              nudges.Add(string msg)
                              nudgeCount <- nudgeCount + 1

                              history <-
                                  Array.append
                                      history
                                      [| muxTextMessage ($"review-nudge-{nudgeCount}") "user" (string msg) |]

                              return true
                          })
                  ) ]

        let hook = get reg "eventHook"

        let streamEnd text =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties", box (createObj [ "parts", box [| box {| ``type`` = "text"; text = text |} |] ]) ]

        do! hook $ (streamEnd "implemented first pass", helpers) |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "active review emits loop nudge" (nudges.Count = 1 && nudges.[0].Contains(loopNudgePromptProse))

        history <-
            Array.append
                history
                [| muxTextMessage "review-assistant-2" "assistant" "verdict: needs_revision\nfeedback: needs rework" |]

        do!
            hook $ (streamEnd "verdict: needs_revision\nfeedback: needs rework", helpers)
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        check
            "reviewer reject reopens loop nudge on fresh assistant output"
            (nudges.Count = 2 && nudges.[1].Contains(loopNudgePromptProse))

        do! rmAsync tempDir
    }

let muxSubmitReviewWipDoesNotSuppressLoopNudgeSpec () =
    promise {
        let! tempDir = mkdtempAsync "mux-review-wip-nudge-"
        let sessionID = "review-wip-nudge-ws"
        do! seedLoopActivated tempDir sessionID "Implement feature X"

        let mutable history =
            [| muxTextMessage "review-wip-loop-anchor" "assistant" (loopAnchor "Implement feature X")
               muxTextMessage "review-wip-assistant-1" "assistant" "implemented first pass" |]

        let reg =
            createRegistration (
                createObj
                    [ "directory", box tempDir
                      "loadConfigOrDefault", box (fun () -> createObj [])
                      "findWorkspaceEntry",
                      box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                      "resolveAgentFrontmatter",
                      box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                      "getChatHistory",
                      box (
                          System.Func<string, JS.Promise<obj array>>(fun workspaceId ->
                              promise { return if workspaceId = sessionID then history else [||] })
                      ) ]
            )

        let nudges = ResizeArray<string>()
        let mutable nudgeCount = 0

        let helpers =
            createObj
                [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] }))
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                          promise {
                              nudges.Add(string msg)
                              nudgeCount <- nudgeCount + 1

                              history <-
                                  Array.append
                                      history
                                      [| muxTextMessage ($"review-wip-nudge-{nudgeCount}") "user" (string msg) |]

                              return true
                          })
                  ) ]

        let hook = get reg "eventHook"

        let streamEnd text =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties", box (createObj [ "parts", box [| box {| ``type`` = "text"; text = text |} |] ]) ]

        do! hook $ (streamEnd "implemented first pass", helpers) |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "active review emits first loop nudge" (nudges.Count = 1 && nudges.[0].Contains(loopNudgePromptProse))

        history <-
            Array.append
                history
                [| muxDynamicToolMessage
                       "review-wip-tool"
                       "submit_review"
                       "wip-call"
                       (createObj [])
                       (box (formatWipAcknowledgment "Implement feature X")) |]

        do!
            hook $ (streamEnd "continued after wip report", helpers)
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        check
            "wip submit_review does not permanently suppress loop nudge"
            (nudges.Count = 2 && nudges.[1].Contains(loopNudgePromptProse))

        do! rmAsync tempDir
    }
