module Wanxiangshu.Tests.IntegrationEventTestsOpencode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec

let private promptText (arg: obj) =
    getPartsText (get (get arg "body") "parts")

let private userTextMessage sessionID text =
    box (
        createObj
            [ "info", box (createObj [ "role", box "user"; "sessionID", box sessionID ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]
    )

let private sleep (ms: int) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> JS.setTimeout (fun () -> resolve ()) ms |> ignore)

let toolExecuteAfterSpec (p: obj) =
    promise {
        let output = createObj [ "output", box "Todos updated" ]

        do!
            (get p "tool.execute.after")
            $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output)
            |> unbox<JS.Promise<unit>>

        check
            "tool.execute.after includes meditator hint"
            (hasExactHint (unbox<string> (get output "output")) hintTodosUpdated)
    }

let private buildMockClient (workspaceDir: string) (getMessages: unit -> obj array) (promptCalls: ResizeArray<obj>) =
    createObj
        [ "session",
          box (
              createObj
                  [ "todo",
                    box (
                        System.Func<unit, JS.Promise<obj>>(fun () ->
                            (promise {
                                return
                                    box
                                        {| data =
                                            [| box
                                                   {| id = "todo-1"
                                                      content = "task"
                                                      status = "in_progress" |} |] |}
                            }))
                    )
                    "messages",
                    box (
                        System.Func<unit, JS.Promise<obj>>(fun () ->
                            (promise { return box {| data = getMessages () |} }))
                    )
                    "prompt",
                    box (
                        System.Func<obj, JS.Promise<unit>>(fun arg ->
                            promise {
                                resolveNudgeReceiptFromPromptArg workspaceDir arg
                                promptCalls.Add(arg)
                            })
                    ) ]
          ) ]

let abortedRetrySpec () =
    promise {
        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "aborted-retry-"
        let mutable messages: obj array = [||]

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = buildMockClient workspaceDir (fun () -> messages) promptCalls |}
            )

        let eventHook = get p "event"

        let mkEvent typ props =
            box {| event = box {| ``type`` = typ; properties = props |} |}

        do!
            eventHook
            $ (mkEvent
                "session.next.step.failed"
                (box
                    {| sessionID = "resume-ws"
                       error =
                        box
                            {| ``type`` = "unknown"
                               message = "Aborted" |} |}))
            |> unbox<JS.Promise<unit>>

        do! sleep 2

        do!
            eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |}))
            |> unbox<JS.Promise<unit>>

        do! sleep 2

        check "aborted retry with no completed assistant does not nudge" (promptCalls.Count = 0)

        do!
            eventHook
            $ (mkEvent
                "session.next.prompted"
                (box
                    {| sessionID = "resume-ws"
                       prompt = box {| text = "continue" |} |}))
            |> unbox<JS.Promise<unit>>

        do! sleep 2

        messages <-
            [| box (
                   createObj
                       [ "info"
                         ==> createObj [ "role" ==> "user"; "id" ==> "msg-1"; "synthetic" ==> false ]
                         "parts" ==> [| createObj [ "type" ==> "text"; "text" ==> "hello" ] |] ]
               )
               box (
                   createObj
                       [ "info"
                         ==> createObj
                                 [ "role" ==> "assistant"
                                   "agent" ==> "manager"
                                   "finish" ==> "stop"
                                   "id" ==> "msg-2"
                                   "parentID" ==> "msg-1"
                                   "time" ==> createObj [ "completed" ==> 1 ] ]
                         "parts" ==> [| createObj [ "type" ==> "text"; "text" ==> "working" ] |] ]
               ) |]

        do!
            eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |}))
            |> unbox<JS.Promise<unit>>

        do! sleep 2

        check "new completed assistant history resumes todo nudge" (promptCalls.Count = 1)
        do! rmAsync workspaceDir
    }
