module Wanxiangshu.Tests.IntegrationEventTestsMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace

open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn

let repeatedTodoNudgeSpec () =
    promise {
        let! tempDir = mkdtempAsync "mux-todo-nudge-"
        let mutable history = [| muxTextMessage "repeat-assistant-1" "assistant" "first" |]
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
                              promise { return if workspaceId = "repeat-ws" then history else [||] })
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

                              history <-
                                  Array.append
                                      history
                                      [| muxTextMessage ($"repeat-nudge-{nudgeCount}") "user" (string msg) |]

                              return true
                          })
                  ) ]

        let hook = get reg "eventHook"

        let streamEnd ws parts =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box ws
                  "properties", box (createObj [ "parts", box parts ]) ]

        let textPart t = box {| ``type`` = "text"; text = t |}

        do!
            hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers [ "pending" ])
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        do!
            hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers [ "pending" ])
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "todo nudge dedupes from event log integral" (nudges.Count = 1)
        history <- Array.append history [| muxTextMessage "repeat-assistant-2" "assistant" "second" |]

        do!
            hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers [ "pending" ])
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "fresh assistant output re-allows todo nudge" (nudges.Count = 2)
        do! rmAsync tempDir
    }
