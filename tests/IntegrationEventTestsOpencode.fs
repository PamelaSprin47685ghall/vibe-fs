module Wanxiangshu.Tests.IntegrationEventTestsOpencode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let private promptText (arg: obj) =
    getPartsText (get (get arg "body") "parts")

let private userTextMessage sessionID text =
    box (
        createObj
            [ "info", box (createObj [ "role", box "user"; "sessionID", box sessionID ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]
    )

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

let abortedRetrySpec () =
    promise {
        let promptCalls = ResizeArray<obj>()
        let mutable messages: obj array = [||]

        let mkClient () =
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
                                    (promise { return box {| data = messages |} }))
                            )
                            "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> (promise { promptCalls.Add(arg) }))) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "aborted-retry-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
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

        do!
            eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |}))
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "aborted retry with no completed assistant does not nudge" (promptCalls.Count = 0)

        do!
            eventHook
            $ (mkEvent
                "session.next.prompted"
                (box
                    {| sessionID = "resume-ws"
                       prompt = box {| text = "continue" |} |}))
            |> unbox<JS.Promise<unit>>

        messages <-
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "stop"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "working" |} |] |} |]

        do!
            eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |}))
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "new completed assistant history resumes todo nudge" (promptCalls.Count = 1)
        do! rmAsync workspaceDir
    }
