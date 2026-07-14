module Wanxiangshu.Tests.IntegrationSubagentMockClient

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Opencode.SubsessionHostAdapter
open Wanxiangshu.Kernel.Subsession.Types

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

                                        let childId = Dyn.str (Dyn.get arg "path") "id"
                                        runtime.SetTaskComplete childId true

                                        let nonce =
                                            let body = Dyn.get arg "body"

                                            if Dyn.isNullish body then
                                                ""
                                            else
                                                let parts = Dyn.get body "parts"

                                                if Dyn.isNullish parts || not (Dyn.isArray parts) then
                                                    ""
                                                else
                                                    let firstPart = Dyn.get parts "0"

                                                    if Dyn.isNullish firstPart then
                                                        ""
                                                    else
                                                        let meta = Dyn.get firstPart "metadata"
                                                        if Dyn.isNullish meta then "" else Dyn.str meta "nonce"

                                        if nonce <> "" then
                                            let msgId = childId + "-msg"
                                            let receipt = UserMessageObserved msgId
                                            let _ = PendingTurnReceipt.tryResolve nonce receipt
                                            ()

                                        // Drive the real "event" hook the same way the host would, but only
                                        // AFTER this prompt call resolves: Dispatch awaits this promise and
                                        // posts DispatchAccepted only once it settles, so firing session.idle
                                        // synchronously in-line here would race Dispatching -> DuplicateIdleBeforeTurnMarker
                                        // (the idle would arrive before Running is ever reached).
                                        let eventHook = Dyn.get pObjRef.Value "event"

                                        if not (Dyn.isNullish eventHook) then
                                            JS.setTimeout
                                                (fun () ->
                                                    promise {
                                                        let messageUpdatedEvent =
                                                            box
                                                                {| event =
                                                                    box
                                                                        {| ``type`` = "message.updated"
                                                                           properties =
                                                                            box
                                                                                {| sessionID = childId
                                                                                   info = box {| role = "assistant" |}
                                                                                   parts =
                                                                                    [| box
                                                                                           {| ``type`` = "text"
                                                                                              text = responseText |} |] |} |} |}

                                                        do!
                                                            (eventHook $ messageUpdatedEvent)
                                                            |> unbox<JS.Promise<unit>>

                                                        let idleEvent =
                                                            box
                                                                {| event =
                                                                    box
                                                                        {| ``type`` = "session.idle"
                                                                           properties = box {| sessionID = childId |} |} |}

                                                        do! (eventHook $ idleEvent) |> unbox<JS.Promise<unit>>
                                                    }
                                                    |> ignore)
                                                0
                                            |> ignore
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
                                                       {| info =
                                                           box
                                                               {| role = "assistant"
                                                                  model =
                                                                   box
                                                                       {| providerID = "mock"
                                                                          modelID = "mock-model" |} |}
                                                          parts =
                                                           [| box
                                                                  {| ``type`` = "text"
                                                                     text = responseText |} |] |} |] |}
                                }))
                        )
                        "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ()))) ]
              ) ]

    createCalls, promptCalls, mockClient
