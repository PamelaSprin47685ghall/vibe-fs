module Wanxiangshu.Tests.IntegrationEventTestsMuxForceStop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn

[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

let muxForceStopTodoNudgeSpec () =
    promise {
        let! tempDir = mkdtempAsync "mux-force-stop-"
        let sessionID = "force-stop-ws"

        let mutable history =
            [| muxTextMessage "force-assistant-1" "assistant" "working on it" |]

        let nudges = ResizeArray<string>()

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

        let mutable nudgeCount = 0

        let helpers todoList =
            createObj
                [ "getTodos",
                  box (System.Func<obj, JS.Promise<obj>>(fun _ -> (promise { return box (todoList |> List.toArray) })))
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                          promise {
                              nudges.Add(string msg)
                              nudgeCount <- nudgeCount + 1
                              return true
                          })
                  ) ]

        let hook = get reg "eventHook"

        let streamAbort =
            createObj [ "type", box "stream-abort"; "workspaceId", box sessionID ]

        let streamEnd ws parts =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box ws
                  "properties", box (createObj [ "parts", box parts ]) ]

        let textPart t = box {| ``type`` = "text"; text = t |}
        do! hook $ (streamAbort, helpers [ "pending" ]) |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()

        do!
            hook
            $ (streamEnd sessionID [| textPart "working on it" |], helpers [ "pending" ])
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "force-stop must not send todo nudge" (nudges.Count = 0)
        do! rmAsync tempDir
    }

let nudgeWithoutChatHistoryButEventCarriesTextSpec () =
    promise {
        let! tempDir = mkdtempAsync "mux-no-chat-history-"
        let sessionID = "no-chat-history-ws-" + System.Guid.NewGuid().ToString()
        do! seedLoopActivated tempDir sessionID "Implement feature X"
        let nudges = ResizeArray<string>()

        let reg =
            createRegistration (
                createObj
                    [ "directory", box tempDir
                      "loadConfigOrDefault", box (fun () -> createObj [])
                      "findWorkspaceEntry",
                      box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                      "resolveAgentFrontmatter",
                      box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj []))) ]
            )

        let helpers =
            createObj
                [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] }))
                  "nudge",
                  box (
                      System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                          promise {
                              nudges.Add(string msg)
                              return true
                          })
                  ) ]

        let hook = get reg "eventHook"
        let textPart t = box {| ``type`` = "text"; text = t |}

        let streamEnd ws parts =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box ws
                  "properties", box (createObj [ "parts", box parts ]) ]

        do!
            hook
            $ (streamEnd sessionID [| textPart "finished first step successfully" |], helpers)
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        check
            "loop nudge fires from event-carried text when getChatHistory is absent"
            (nudges.Count = 1
             && (nudges.[0].Contains "submit_review" || nudges.[0].Contains "With-Review"))

        do! rmAsync tempDir
    }
