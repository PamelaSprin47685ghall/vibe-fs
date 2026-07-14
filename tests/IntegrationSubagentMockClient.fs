module Wanxiangshu.Tests.IntegrationSubagentMockClient

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildSessionMailbox

let makeMockClient (pObjRef: obj ref) (parentId: string) (responseText: string) =
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mutable sessionCounter = 0

    let mockClient =
        createObj
            [ "session",
              box (
                  createObj
                      [ "create",
                        box (
                            System.Func<obj, JS.Promise<obj>>(fun arg ->
                                (promise {
                                    createCalls.Add(arg)
                                    sessionCounter <- sessionCounter + 1
                                    let childId = parentId + "-session-" + string sessionCounter
                                    return box {| data = box {| id = childId |} |}
                                }))
                        )
                        "prompt",
                        box (
                            System.Func<obj, JS.Promise<unit>>(fun arg ->
                                (promise {
                                    promptCalls.Add(arg)

                                    if not (isNull pObjRef.Value) then
                                        let runtime = pObjRef.Value?__fallbackRuntime |> unbox<FallbackRuntimeState>

                                        let childId = str (get arg "path") "id"
                                        runtime.ClearSubsessionPending childId
                                        runtime.SetTaskComplete childId true

                                        // Post events to child session mailbox for event-driven flow
                                        match ChildSessionMailboxRegistry.TryGet childId with
                                        | Some mb ->
                                            do! mb.Post(Command.TaskComplete "")
                                            do! mb.Post(Command.SessionIdle)
                                        | None -> ()
                                }))
                        )
                        "messages",
                        box (
                            System.Func<obj, JS.Promise<obj>>(fun _ ->
                                (promise {
                                    return
                                        box
                                            {| data =
                                                [| box
                                                       {| info = box {| role = "assistant" |}
                                                          parts =
                                                           [| box
                                                                  {| ``type`` = "text"
                                                                     text = responseText |} |] |} |] |}
                                }))
                        )
                        "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ()))) ]
              ) ]

    createCalls, promptCalls, mockClient
